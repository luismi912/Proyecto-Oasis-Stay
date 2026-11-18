using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OasisStay.Api.Contratos;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Semilla;
using OasisStay.Pruebas.F0;
using OasisStay.Pruebas.F2;

namespace OasisStay.Pruebas.F4;

/// <summary>API de cancelación, penalidad y No-Show con roles y errores acordados [JIRA US-08][DE#4][D-44][D-34]; semilla por escenario [D-39][D-41].</summary>
public class ApiCancelacionPruebas : IClassFixture<FabricaApiF2>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly FabricaApiF2 _fabrica;
    public ApiCancelacionPruebas(FabricaApiF2 fabrica) => _fabrica = fabrica;

    private async Task<HttpClient> Cliente(string usuario, string clave)
    {
        var c = _fabrica.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new LoginSolicitud { Usuario = usuario, Contrasena = clave });
        var t = (await r.Content.ReadFromJsonAsync<LoginRespuesta>(Web))!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t.Token);
        return c;
    }

    private Task<HttpClient> Recepcion() => Cliente("recepcion@oasisstay.co", FabricaApi.ClaveRecepcion);
    private Task<HttpClient> Admin() => Cliente("admin@oasisstay.co", FabricaApi.ClaveAdmin);

    private async Task Escenario()
    {
        using var scope = _fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
        await scope.ServiceProvider.GetRequiredService<EjecutorSemillaDemo>().EjecutarAsync(db, "US08");
    }

    private T Leer<T>(Func<OasisStayDbContext, T> f)
    {
        using var scope = _fabrica.Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<OasisStayDbContext>());
    }

    private int IdDe(int habitacion) => Leer(db => db.Reservas.Single(r => r.NumeroHabitacion == habitacion).IdReserva);

    private static async Task<JsonElement> Json(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    [Fact] // [JIRA US-08 ESC2] 222: noche $180.000, check-in hoy a las 15:00 -> fuera de plazo
    public async Task US08_ESC2_Api_CancelacionTardia_Penalidad180000_HabitacionDisponible()
    {
        await Escenario();
        var id = IdDe(222);
        var c = await Recepcion();
        var previa = (await c.GetFromJsonAsync<PenalidadRespuesta>($"/api/reservas/{id}/penalidad", Web))!;
        Assert.False(previa.Gratis);
        Assert.Equal(180000m, previa.Penalidad);

        var r = await c.PostAsJsonAsync($"/api/reservas/{id}/cancelar", new CancelacionSolicitud());
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var cuerpo = (await r.Content.ReadFromJsonAsync<CancelacionRespuesta>(Web))!;
        Assert.Equal(("Cancelada", 180000m, false), (cuerpo.Estado, cuerpo.Penalidad, cuerpo.Exonerada));
        Assert.Equal(EstadoHabitacion.DISPONIBLE, Leer(db => db.Habitaciones.Single(h => h.NumeroHabitacion == 222).Estado));

        var lista = (await c.GetFromJsonAsync<List<ReservaListadoRespuesta>>("/api/reservas", Web))!;
        Assert.Equal(180000m, lista.Single(x => x.IdReserva == id).Penalidad);
    }

    [Fact] // [JIRA US-08 ESC1] gratis: más de 24 h antes del check-in
    public async Task US08_ESC1_Api_CancelacionGratuita_PenalidadCero()
    {
        await Escenario();
        var id = IdDe(221); // llegada hoy + 3 días, noche $220.000
        var c = await Recepcion();
        Assert.True((await c.GetFromJsonAsync<PenalidadRespuesta>($"/api/reservas/{id}/penalidad", Web))!.Gratis);
        var r = await c.PostAsJsonAsync($"/api/reservas/{id}/cancelar", new CancelacionSolicitud());
        var cuerpo = (await r.Content.ReadFromJsonAsync<CancelacionRespuesta>(Web))!;
        Assert.Equal(("Cancelada", 0m), (cuerpo.Estado, cuerpo.Penalidad));
    }

    [Fact] // [JIRA US-08 ESC3][D-24]
    public async Task US08_ESC3_Api_Exoneracion_Administrador_CobroCeroYMotivoEnHistorial()
    {
        await Escenario();
        var id = IdDe(223);
        var c = await Admin();
        var r = await c.PostAsJsonAsync($"/api/reservas/{id}/cancelar", new CancelacionSolicitud { Exonerar = true, Motivo = "Emergencia médica demostrada" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var cuerpo = (await r.Content.ReadFromJsonAsync<CancelacionRespuesta>(Web))!;
        Assert.Equal(("Cancelada", 0m, true), (cuerpo.Estado, cuerpo.Penalidad, cuerpo.Exonerada));
        var evento = Leer(db => db.HistorialReservas.Single(h => h.IdReserva == id));
        Assert.Contains("Emergencia médica demostrada", evento.ValorNuevo);
        Assert.False(string.IsNullOrWhiteSpace(evento.Usuario));
    }

    [Fact] // [JIRA US-08 RN3]
    public async Task US08_Exonerar_Recepcionista_403()
    {
        await Escenario();
        var id = IdDe(223);
        var c = await Recepcion();
        var r = await c.PostAsJsonAsync($"/api/reservas/{id}/cancelar", new CancelacionSolicitud { Exonerar = true, Motivo = "Emergencia" });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Equal("Solo el Administrador puede exonerar la penalidad", (await Json(r)).GetProperty("detail").GetString());
        Assert.Equal(EstadoReserva.Confirmada, Leer(db => db.Reservas.Single(x => x.IdReserva == id).Estado));
    }

    [Fact] // [JIRA US-08 RN3]
    public async Task US08_Exonerar_SinMotivo_400()
    {
        await Escenario();
        var id = IdDe(223);
        var c = await Admin();
        var r = await c.PostAsJsonAsync($"/api/reservas/{id}/cancelar", new CancelacionSolicitud { Exonerar = true, Motivo = " " });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("Debe ingresar la justificación de la exoneración", (await Json(r)).GetProperty("detail").GetString());
    }

    [Fact] // [DE#4 §5][CP-R08]
    public async Task Api_Cancelar_EnCurso_409_YNoCambia()
    {
        await Escenario();
        var id = IdDe(225);
        var c = await Recepcion();
        var previa = await c.GetAsync($"/api/reservas/{id}/penalidad");
        Assert.Equal(HttpStatusCode.Conflict, previa.StatusCode);
        var r = await c.PostAsJsonAsync($"/api/reservas/{id}/cancelar", new CancelacionSolicitud());
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal("Una reserva en curso no puede cancelarse", (await Json(r)).GetProperty("detail").GetString());
        Assert.Equal(EstadoReserva.EnCurso, Leer(db => db.Reservas.Single(x => x.IdReserva == id).Estado));
    }

    [Fact]
    public async Task Api_Cancelar_Inexistente_404_YSinToken_401_YAma_403()
    {
        var c = await Recepcion();
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync("/api/reservas/99999/cancelar", new CancelacionSolicitud())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/reservas/99999/penalidad")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.CreateClient().PostAsJsonAsync("/api/reservas/1/cancelar", new CancelacionSolicitud())).StatusCode);
        var ama = await Cliente("amadellaves@oasisstay.co", FabricaApi.ClaveAma);
        Assert.Equal(HttpStatusCode.Forbidden, (await ama.PostAsJsonAsync("/api/reservas/1/cancelar", new CancelacionSolicitud())).StatusCode);
    }

    [Fact] // [DE#4 CP-R05][D-44]
    public async Task CP_R05_Api_NoShow_Manual_SoloAdministrador()
    {
        await Escenario();
        var id = IdDe(224);
        var rec = await Recepcion();
        Assert.Equal(HttpStatusCode.Forbidden, (await rec.PostAsJsonAsync("/api/reservas/no-show/ejecutar", new NoShowSolicitud())).StatusCode);
        Assert.Equal(EstadoReserva.Confirmada, Leer(db => db.Reservas.Single(x => x.IdReserva == id).Estado));

        var admin = await Admin();
        var r = await admin.PostAsJsonAsync("/api/reservas/no-show/ejecutar", new NoShowSolicitud());
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.True((await r.Content.ReadFromJsonAsync<NoShowRespuesta>(Web))!.Procesadas >= 1);
        var reserva = Leer(db => db.Reservas.Single(x => x.IdReserva == id));
        Assert.Equal((EstadoReserva.NoShow, 220000m), (reserva.Estado, reserva.Penalidad));
        Assert.Equal(EstadoHabitacion.DISPONIBLE, Leer(db => db.Habitaciones.Single(h => h.NumeroHabitacion == 224).Estado));
        Assert.Equal(EstadoReserva.EnCurso, Leer(db => db.Reservas.Single(x => x.NumeroHabitacion == 225).Estado));
    }

    [Fact] // [DE#4 CP-R05]: el día de llegada aún no terminó, no es No-Show
    public async Task CP_R05_NoShow_Manual_NoMarcaLlegadasDeHoy()
    {
        await Escenario();
        var admin = await Admin();
        var r = await admin.PostAsJsonAsync("/api/reservas/no-show/ejecutar", new NoShowSolicitud());
        Assert.Equal(1, (await r.Content.ReadFromJsonAsync<NoShowRespuesta>(Web))!.Procesadas);
        Assert.Equal(EstadoReserva.Confirmada, Leer(db => db.Reservas.Single(x => x.NumeroHabitacion == 222).Estado));
        Assert.Equal(EstadoReserva.Confirmada, Leer(db => db.Reservas.Single(x => x.NumeroHabitacion == 223).Estado));
        Assert.Equal(EstadoReserva.NoShow, Leer(db => db.Reservas.Single(x => x.NumeroHabitacion == 224).Estado));
    }

    [Fact] // [D-39][D-41]: fechas relativas a hoy (UTC-5) para reproducir los escenarios cualquier día
    public async Task Semilla_PorEscenario_US08_FechasRelativasAHoy_Idempotente()
    {
        await Escenario();
        await Escenario(); // reinicia sin duplicar
        var hoy = _fabrica.Services.GetRequiredService<OasisStay.Aplicacion.IReloj>().Ahora.Date;
        var esperado = new (int Hab, decimal Noche, DateTime Inicio, EstadoReserva Estado)[]
        {
            (221, 220000m, hoy.AddDays(3), EstadoReserva.Confirmada),
            (222, 180000m, hoy, EstadoReserva.Confirmada),
            (223, 220000m, hoy, EstadoReserva.Confirmada),
            (224, 220000m, hoy.AddDays(-1), EstadoReserva.Confirmada),
            (225, 220000m, hoy.AddDays(-1), EstadoReserva.EnCurso),
        };
        foreach (var e in esperado)
        {
            var r = Leer(db => db.Reservas.Single(x => x.NumeroHabitacion == e.Hab));
            Assert.Equal((e.Estado, e.Inicio), (r.Estado, r.FechaInicio));
            Assert.Equal(e.Noche, Leer(db => db.Habitaciones.Where(x => x.NumeroHabitacion == e.Hab).Select(x => x.Tipo!.PrecioBase).Single()));
            Assert.Equal(e.Estado == EstadoReserva.EnCurso ? EstadoHabitacion.OCUPADO : EstadoHabitacion.RESERVADA,
                Leer(db => db.Habitaciones.Single(x => x.NumeroHabitacion == e.Hab).Estado));
        }
        Assert.Equal(5, Leer(db => db.Reservas.Count()));
    }
}
