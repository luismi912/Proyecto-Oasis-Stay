using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OasisStay.Api.Contratos;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Semilla;
using OasisStay.Pruebas.F0;
using OasisStay.Pruebas.F2;

namespace OasisStay.Pruebas.F4a;

/// <summary>Consulta de la bitácora de auditoría: GET /api/historial, roles, solo lectura y semilla US07 [JIRA US-07][DS3][D-23][D-34][D-37][D-38][D-41].</summary>
public class HistorialPruebas : IClassFixture<FabricaApiF2>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly FabricaApiF2 _fabrica;
    public HistorialPruebas(FabricaApiF2 fabrica) => _fabrica = fabrica;

    private async Task<HttpClient> Cliente(string usuario, string clave)
    {
        var c = _fabrica.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new LoginSolicitud { Usuario = usuario, Contrasena = clave });
        var t = (await r.Content.ReadFromJsonAsync<LoginRespuesta>(Web))!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t.Token);
        return c;
    }

    private Task<HttpClient> Admin() => Cliente("admin@oasisstay.co", FabricaApi.ClaveAdmin);
    private Task<HttpClient> Recepcion() => Cliente("recepcion@oasisstay.co", FabricaApi.ClaveRecepcion);

    private async Task Escenario(string clave = "US07")
    {
        using var scope = _fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
        await scope.ServiceProvider.GetRequiredService<EjecutorSemillaDemo>().EjecutarAsync(db, clave);
    }

    private T Leer<T>(Func<OasisStayDbContext, T> f)
    {
        using var scope = _fabrica.Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<OasisStayDbContext>());
    }

    private static async Task<List<JsonElement>> Eventos(HttpResponseMessage r)
    {
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.EnumerateArray().ToList();
    }

    [Fact] // [JIRA US-07 ESC1/datos]: #RS-4011 creada por "mlopez" 09:15 y modificada por "admin" 11:30 (hab 101 a 102)
    public async Task US07_ESC1_HistorialReserva_RS4011_LineaDeTiempo()
    {
        await Escenario();
        var c = await Admin();
        var ev = await Eventos(await c.GetAsync("/api/historial?idReserva=4011"));

        Assert.Equal(2, ev.Count);
        var f0 = ev[0].GetProperty("fechaHora").GetDateTime();
        var f1 = ev[1].GetProperty("fechaHora").GetDateTime();
        Assert.Equal(("mlopez", "Reserva creada", 9, 15), (ev[0].GetProperty("usuario").GetString(), ev[0].GetProperty("accion").GetString(), f0.Hour, f0.Minute));
        Assert.Equal(("admin", "Habitación reasignada", "Hab. 101", "Hab. 102", 11, 30), (ev[1].GetProperty("usuario").GetString(),
            ev[1].GetProperty("accion").GetString(), ev[1].GetProperty("valorAnterior").GetString(), ev[1].GetProperty("valorNuevo").GetString(), f1.Hour, f1.Minute));
        Assert.True(f0 < f1);
        Assert.All(ev, e => Assert.Equal(4011, e.GetProperty("idReserva").GetInt32()));
    }

    [Fact] // [JIRA US-07 ESC2/datos]: "jrodriguez" del 20/10/2026
    public async Task US07_ESC2_FiltroUsuarioYFecha_jrodriguez_20Oct2026()
    {
        await Escenario();
        var c = await Admin();
        var ev = await Eventos(await c.GetAsync("/api/historial?usuario=jrodriguez&desde=2026-10-20&hasta=2026-10-20"));

        Assert.Equal(3, ev.Count);
        Assert.All(ev, e =>
        {
            Assert.Equal("jrodriguez", e.GetProperty("usuario").GetString());
            Assert.Equal(new DateTime(2026, 10, 20), e.GetProperty("fechaHora").GetDateTime().Date);
        });
        var fechas = ev.Select(e => e.GetProperty("fechaHora").GetDateTime()).ToList();
        Assert.Equal(fechas.OrderBy(f => f).ToList(), fechas);

        // sin el filtro de fecha aparece también su transacción del 21/10: el filtro realmente acota
        Assert.Equal(4, (await Eventos(await c.GetAsync("/api/historial?usuario=jrodriguez"))).Count);
        // solo la fecha trae también a otros usuarios del mismo día (mlopez, admin)
        var dia = await Eventos(await c.GetAsync("/api/historial?desde=2026-10-20&hasta=2026-10-20"));
        Assert.Contains(dia, e => e.GetProperty("usuario").GetString() == "mlopez");
    }

    [Fact] // [JIRA US-07 ESC3][JIRA US-07 RN2]: no existen PUT/PATCH/DELETE sobre la bitácora
    public async Task US07_ESC3_SinEditarNiBorrar_Administrador()
    {
        await Escenario();
        var c = await Admin();
        foreach (var ruta in new[] { "/api/historial", "/api/historial/1" })
        {
            foreach (var metodo in new[] { HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete, HttpMethod.Post })
            {
                var r = await c.SendAsync(new HttpRequestMessage(metodo, ruta) { Content = JsonContent.Create(new { }) });
                Assert.True(r.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"{metodo} {ruta} -> {(int)r.StatusCode}");
            }
        }
        Assert.Equal(6, Leer(db => db.HistorialReservas.Count())); // nada se borró ni se agregó

        // la pantalla no ofrece botones de edición ni borrado de eventos
        var html = File.ReadAllText(RutaPantalla());
        Assert.DoesNotMatch(new Regex(@"(Editar|Eliminar|Borrar|Modificar)\s*(evento|registro)?", RegexOptions.IgnoreCase), html);
        Assert.Contains("Ver historial", html);
    }

    private static string RutaPantalla()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Front-end", "Pages", "Ventanas", "Historial.cshtml"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "Front-end", "Pages", "Ventanas", "Historial.cshtml");
    }

    [Fact] // [JIRA US-07 RN3][D-23]: fecha/hora, ID de reserva, usuario, acción y valor anterior/nuevo
    public async Task US07_RN3_RegistroConTodosLosCampos()
    {
        await Escenario();
        var c = await Admin();
        var ev = await Eventos(await c.GetAsync("/api/historial?idReserva=4011"));
        foreach (var campo in new[] { "fechaHora", "idReserva", "usuario", "accion", "valorAnterior", "valorNuevo" })
        {
            Assert.True(ev[1].TryGetProperty(campo, out var v), campo);
            Assert.NotEqual(JsonValueKind.Null, v.ValueKind);
        }
        Assert.True(ev[0].TryGetProperty("valorAnterior", out _)); // la creación no tiene valor anterior, pero el campo existe
        Assert.True(ev[0].TryGetProperty("valorNuevo", out _));
    }

    [Fact] // [actividad-3] flujo alternativo [no hay resultados]
    public async Task Historial_SinResultados_ListaVacia()
    {
        await Escenario();
        var c = await Admin();
        Assert.Empty(await Eventos(await c.GetAsync("/api/historial?usuario=nadie")));
        Assert.Empty(await Eventos(await c.GetAsync("/api/historial?idReserva=999999")));
        Assert.Empty(await Eventos(await c.GetAsync("/api/historial?texto=zzzz-no-existe")));
        Assert.Empty(await Eventos(await c.GetAsync("/api/historial?desde=2030-01-01&hasta=2030-01-02")));
    }

    [Fact] // [D-34][RNF-06]
    public async Task Historial_Recepcionista_403()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.CreateClient().GetAsync("/api/historial")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Recepcion()).GetAsync("/api/historial")).StatusCode);
    }

    [Fact] // [D-34][RNF-06]
    public async Task Historial_AmaDeLlaves_403()
    {
        var ama = await Cliente("amadellaves@oasisstay.co", FabricaApi.ClaveAma);
        Assert.Equal(HttpStatusCode.Forbidden, (await ama.GetAsync("/api/historial")).StatusCode);
    }

    [Fact] // [JIRA US-07]: desde > hasta
    public async Task Historial_RangoInvalido_400()
    {
        var c = await Admin();
        var r = await c.GetAsync("/api/historial?desde=2026-10-21&hasta=2026-10-20");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var detalle = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("detail").GetString();
        Assert.Equal("La fecha inicial no puede ser posterior a la fecha final.", detalle);
    }

    [Fact] // [D-41][JIRA US-07 datos]: la semilla por escenario deja exactamente lo de US-07
    public async Task Semilla_PorEscenario_US07()
    {
        await Escenario("US08"); // se contamina antes con otro escenario para comprobar el reinicio
        await Escenario();
        await Escenario(); // repetirlo deja lo mismo

        Assert.Equal(3, Leer(db => db.Reservas.Count()));
        Assert.Equal(6, Leer(db => db.HistorialReservas.Count()));
        Assert.Equal(102, Leer(db => db.Reservas.Single(r => r.IdReserva == 4011).NumeroHabitacion));

        var h = Leer(db => db.HistorialReservas.Where(x => x.IdReserva == 4011).OrderBy(x => x.FechaRegistro).ToList());
        Assert.Equal(new DateTime(2026, 10, 20, 9, 15, 0), h[0].FechaRegistro);
        Assert.Equal("mlopez", h[0].Usuario);
        Assert.Equal(new DateTime(2026, 10, 20, 11, 30, 0), h[1].FechaRegistro);
        Assert.Equal(("admin", "Hab. 101", "Hab. 102"), (h[1].Usuario, h[1].ValorAnterior, h[1].ValorNuevo));
        Assert.Equal(3, Leer(db => db.HistorialReservas.Count(x => x.Usuario == "jrodriguez" && x.FechaRegistro.Date == new DateTime(2026, 10, 20))));
    }

    private static ReservaSolicitud Solicitud(string doc, int habitacion, DateTime ini, DateTime fin) => new()
    {
        TipoDocumento = "CC", Documento = doc, Nombre = "Carlos Mendoza", Telefono = "3101234567", Correo = "carlos.mendoza@email.com",
        FechaInicio = ini, FechaFin = fin, Huespedes = 1, NumeroHabitacion = habitacion,
    };

    [Fact] // [JIRA US-07 RN1]: crear, check-in y check-out aparecen con usuario, anterior y nuevo [D-23]
    public async Task Historial_Eventos_CrearReservaCheckInCheckOut_Aparecen()
    {
        var rec = await Recepcion();
        var adm = await Admin();
        var hoy = DateTime.Today;
        var crear = await rec.PostAsJsonAsync("/api/reservas", Solicitud("1029384756", 304, hoy, hoy.AddDays(1)));
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        var id = (await crear.Content.ReadFromJsonAsync<ReservaCreadaRespuesta>(Web))!.IdReserva;

        var ci = await rec.PostAsJsonAsync($"/api/reservas/{id}/check-in", new { documento = "1029384756" });
        Assert.Equal(HttpStatusCode.OK, ci.StatusCode);
        var co = await rec.PostAsJsonAsync($"/api/reservas/{id}/checkout", new { metodoPago = "EFECTIVO", datosPago = new { montoRecibido = 1000000m } });
        Assert.Equal(HttpStatusCode.OK, co.StatusCode);

        var ev = await Eventos(await adm.GetAsync($"/api/historial?idReserva={id}"));
        Assert.Equal(new[] { "Reserva creada", "Check-in", "Check-out" }, ev.Select(e => e.GetProperty("accion").GetString()).ToArray());
        Assert.All(ev, e => Assert.False(string.IsNullOrWhiteSpace(e.GetProperty("usuario").GetString())));
        Assert.Equal("EnCurso", ev[2].GetProperty("valorAnterior").GetString());
        Assert.StartsWith("Finalizada", ev[2].GetProperty("valorNuevo").GetString());
    }
}
