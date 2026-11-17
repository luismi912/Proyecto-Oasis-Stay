using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OasisStay.Api.Contratos;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Semilla;
using OasisStay.Pruebas.F0;

namespace OasisStay.Pruebas.F2;

/// <summary>F2b: botón Reasignar (US-04 ESC3, D-21, D-32) y semilla demo por escenario (D-39, D-41).</summary>
public class ReasignarYSemillaPruebas : IClassFixture<FabricaApiF2>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly FabricaApiF2 _fabrica;
    public ReasignarYSemillaPruebas(FabricaApiF2 fabrica) => _fabrica = fabrica;

    private async Task<HttpClient> Recepcion()
    {
        var c = _fabrica.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new LoginSolicitud { Usuario = "recepcion@oasisstay.co", Contrasena = FabricaApi.ClaveRecepcion });
        var t = (await r.Content.ReadFromJsonAsync<LoginRespuesta>(Web))!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t.Token);
        return c;
    }

    private async Task Escenario(string escenario)
    {
        using var scope = _fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
        await scope.ServiceProvider.GetRequiredService<EjecutorSemillaDemo>().EjecutarAsync(db, escenario);
    }

    private T Leer<T>(Func<OasisStayDbContext, T> f)
    {
        using var scope = _fabrica.Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<OasisStayDbContext>());
    }

    private static string Fecha(DateTime d) => d.ToString("yyyy-MM-dd");

    private async Task<DisponibilidadRespuesta> Disponibilidad(HttpClient c, string query)
        => (await c.GetFromJsonAsync<DisponibilidadRespuesta>("/api/habitaciones/disponibilidad?" + query, Web))!;

    [Fact] // [JIRA US-04 ESC3][D-21]
    public async Task US04_ESC3_Reasignar_Pantalla_105a107_ListaModalYEstados()
    {
        await Escenario("US04");
        var c = await Recepcion();
        var reserva = Leer(db => db.Reservas.Single());
        var q = $"fechaInicio={Fecha(reserva.FechaInicio)}&fechaFin={Fecha(reserva.FechaFin)}&huespedes={reserva.Huespedes}";

        // lo que lista el modal: 107 sí, la actual (105, Reservada) no
        var antes = (await Disponibilidad(c, q)).Habitaciones.Select(h => h.Numero).ToList();
        Assert.Contains(107, antes);
        Assert.DoesNotContain(105, antes);

        var r = await c.PutAsJsonAsync($"/api/reservas/{reserva.IdReserva}/habitacion", new ReasignacionSolicitud { NumeroHabitacionNueva = 107 });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        Assert.Equal(EstadoHabitacion.DISPONIBLE, Leer(db => db.Habitaciones.Single(h => h.NumeroHabitacion == 105).Estado));
        Assert.Equal(EstadoHabitacion.RESERVADA, Leer(db => db.Habitaciones.Single(h => h.NumeroHabitacion == 107).Estado));
        var despues = (await Disponibilidad(c, q)).Habitaciones.Select(h => h.Numero).ToList();
        Assert.Contains(105, despues);
        Assert.DoesNotContain(107, despues);
    }

    [Fact] // [JIRA US-04 RN3][D-21][D-32]
    public async Task US04_ESC3_Reasignar_Pantalla_Upgrade_PideConfirmacionConTextoExacto()
    {
        await Escenario("US04");
        var c = await Recepcion();
        var id = Leer(db => db.Reservas.Single().IdReserva);

        var sin = await c.PutAsJsonAsync($"/api/reservas/{id}/habitacion", new ReasignacionSolicitud { NumeroHabitacionNueva = 304 });
        Assert.Equal(HttpStatusCode.Conflict, sin.StatusCode);
        var detalle = JsonDocument.Parse(await sin.Content.ReadAsStringAsync()).RootElement.GetProperty("detail").GetString();
        Assert.Equal("La habitación nueva es de categoría superior (Upgrade). Confirme para continuar.", detalle);
        Assert.Equal(EstadoHabitacion.RESERVADA, Leer(db => db.Habitaciones.Single(h => h.NumeroHabitacion == 105).Estado));

        var con = await c.PutAsJsonAsync($"/api/reservas/{id}/habitacion", new ReasignacionSolicitud { NumeroHabitacionNueva = 304, ConfirmarUpgrade = true });
        Assert.Equal(HttpStatusCode.OK, con.StatusCode);
        Assert.Equal(EstadoHabitacion.DISPONIBLE, Leer(db => db.Habitaciones.Single(h => h.NumeroHabitacion == 105).Estado));
        Assert.Equal(EstadoHabitacion.RESERVADA, Leer(db => db.Habitaciones.Single(h => h.NumeroHabitacion == 304).Estado));
    }

    [Fact] // [JIRA US-04 ESC2][D-21]: lista vieja del modal (otro recepcionista tomó la 107 antes)
    public async Task US04_ESC2_Reasignar_Pantalla_HabitacionYaTomada_RechazaConMotivo()
    {
        await Escenario("US04");
        var c = await Recepcion();
        var id = Leer(db => db.Reservas.Single().IdReserva);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/reservas/{id}/habitacion", new ReasignacionSolicitud { NumeroHabitacionNueva = 107 })).StatusCode);

        var otra = await c.PutAsJsonAsync($"/api/reservas/{id}/habitacion", new ReasignacionSolicitud { NumeroHabitacionNueva = 107 });
        Assert.False(otra.IsSuccessStatusCode);
        var detalle = JsonDocument.Parse(await otra.Content.ReadAsStringAsync()).RootElement.GetProperty("detail").GetString();
        Assert.Equal("La habitación no está disponible para reservar", detalle);
    }

    [Fact] // [D-39]
    public async Task Semilla_PorEscenario_US04_105Reservada_107Y304Disponibles_ReinicioIdempotente()
    {
        await Escenario("US04");
        var id = Leer(db => db.Reservas.Single().IdReserva);
        var c = await Recepcion();
        await c.PutAsJsonAsync($"/api/reservas/{id}/habitacion", new ReasignacionSolicitud { NumeroHabitacionNueva = 107 });
        Assert.Equal(EstadoHabitacion.RESERVADA, Leer(db => db.Habitaciones.Single(h => h.NumeroHabitacion == 107).Estado));

        await Escenario("US04"); // reinicia
        await Escenario("US04"); // sin duplicar
        Assert.Equal(1, Leer(db => db.Reservas.Count()));
        var r = Leer(db => db.Reservas.Single());
        Assert.Equal((105, 2, EstadoReserva.Confirmada), (r.NumeroHabitacion, r.Huespedes, r.Estado));
        Assert.Equal(EstadoHabitacion.RESERVADA, Leer(db => db.Habitaciones.Single(h => h.NumeroHabitacion == 105).Estado));
        Assert.Equal(EstadoHabitacion.DISPONIBLE, Leer(db => db.Habitaciones.Single(h => h.NumeroHabitacion == 107).Estado));
        Assert.Equal(EstadoHabitacion.DISPONIBLE, Leer(db => db.Habitaciones.Single(h => h.NumeroHabitacion == 304).Estado));
        Assert.Equal(1, Leer(db => db.Clientes.Count(x => x.Documento == "1020304050")));
    }

    [Fact] // [D-39][JIRA US-03 ESC2]
    public async Task Semilla_PorEscenario_US03_CarlosMendozaExiste_SinReservas()
    {
        await Escenario("US03");
        await Escenario("US03");
        var carlos = Leer(db => db.Clientes.Single());
        Assert.Equal(("1020304050", "Carlos Mendoza", "3101234567", "carlos.mendoza@email.com"), (carlos.Documento, carlos.Nombre, carlos.Telefono, carlos.Correo));
        Assert.Equal(0, Leer(db => db.Reservas.Count()));
        var c = await Recepcion();
        var h = await c.GetFromJsonAsync<HuespedRespuesta>("/api/huespedes/buscar?tipoDocumento=CC&documento=1020304050", Web);
        Assert.Equal("Carlos Mendoza", h!.Nombre);
    }

    [Fact] // [D-39][JIRA US-02 ESC1-3]
    public async Task Semilla_PorEscenario_US02_301EnLimpieza_105Disponible_204Ocupada()
    {
        await Escenario("US02");
        await Escenario("US02");
        Assert.Equal(EstadoHabitacion.EN_LIMPIEZA, Leer(db => db.Habitaciones.Single(h => h.NumeroHabitacion == 301).Estado));
        Assert.Equal(EstadoHabitacion.DISPONIBLE, Leer(db => db.Habitaciones.Single(h => h.NumeroHabitacion == 105).Estado));
        Assert.Equal(EstadoHabitacion.OCUPADO, Leer(db => db.Habitaciones.Single(h => h.NumeroHabitacion == 204).Estado));
        Assert.Equal(1, Leer(db => db.Habitaciones.Count(h => h.NumeroHabitacion == 105)));
    }

    [Fact] // [D-39][JIRA US-01 ESC1]
    public async Task Semilla_PorEscenario_US01_SuitesLibres_10al12Nov2026()
    {
        await Escenario("US01");
        var c = await Recepcion();
        var r = await Disponibilidad(c, "fechaInicio=2026-11-10&fechaFin=2026-11-12&tipo=Suite&huespedes=2");
        Assert.Equal(new[] { 201, 304 }, r.Habitaciones.Select(h => h.Numero).OrderBy(n => n).ToArray());
    }

    [Fact] // [JIRA US-01 ESC2][D-32]: la alerta de sin cupo trae alternativas
    public async Task US01_ESC2_Semilla_EstandarAgotada_AlertaTraeAlternativas()
    {
        await Escenario("US01");
        var c = await Recepcion();
        var r = await Disponibilidad(c, "fechaInicio=2026-11-10&fechaFin=2026-11-12&tipo=Doble&huespedes=2");
        Assert.Empty(r.Habitaciones);
        Assert.Equal("No hay habitaciones disponibles para ese criterio. Pruebe otras fechas u otro tipo de habitación.", r.Mensaje);
        Assert.NotEmpty(r.Alternativas);
        Assert.Contains(r.Alternativas, a => a.Nombre == "Suite Ejecutiva");
    }

    [Fact] // [D-39]
    public async Task Semilla_PorEscenario_Desconocido_Lanza()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Escenario("US99"));
    }

    [Fact] // [D-39]: sin argumento sigue siendo la semilla de siempre
    public async Task Semilla_SinEscenario_SigueIgual()
    {
        using var scope = _fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
        await scope.ServiceProvider.GetRequiredService<EjecutorSemillaDemo>().EjecutarAsync(db);
        Assert.True(await db.Habitaciones.AnyAsync(h => h.NumeroHabitacion == 106));
        Assert.True(await db.Reservas.AnyAsync(r => r.NumeroHabitacion == 106));
    }
}
