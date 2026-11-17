using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OasisStay.Api.Contratos;
using OasisStay.Aplicacion;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Repositorios;
using OasisStay.Pruebas.F0;

namespace OasisStay.Pruebas.F2;

/// <summary>API con BD en memoria (sin transacciones) y estados reales de F1.</summary>
public class FabricaApiF2 : FabricaApi
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(d => d.ServiceType.IsGenericType &&
                         d.ServiceType.GenericTypeArguments.Contains(typeof(OasisStayDbContext))).ToList())
                services.Remove(d);
            var nombre = "OasisStayF2_" + Guid.NewGuid();
            services.AddDbContext<OasisStayDbContext>(o => o.UseInMemoryDatabase(nombre)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        });
    }
}

public class ApiReservasPruebas : IClassFixture<FabricaApiF2>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly FabricaApiF2 _fabrica;
    public ApiReservasPruebas(FabricaApiF2 fabrica) => _fabrica = fabrica;

    private async Task<HttpClient> Cliente(string usuario, string clave)
    {
        var c = _fabrica.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new LoginSolicitud { Usuario = usuario, Contrasena = clave });
        var t = (await r.Content.ReadFromJsonAsync<LoginRespuesta>(Web))!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t.Token);
        return c;
    }

    private Task<HttpClient> Recepcion() => Cliente("recepcion@oasisstay.co", FabricaApi.ClaveRecepcion);

    private static ReservaSolicitud Solicitud(string doc, int habitacion, string ini, string fin, int huespedes = 2) => new()
    {
        TipoDocumento = "CC", Documento = doc, Nombre = "Carlos Mendoza", Telefono = "3101234567", Correo = "carlos.mendoza@email.com",
        FechaInicio = DateTime.Parse(ini), FechaFin = DateTime.Parse(fin), Huespedes = huespedes, NumeroHabitacion = habitacion,
    };

    private static async Task<JsonElement> Json(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    [Fact] // [D-34][RNF-06]
    public async Task Api_Reservas_RolesYSinToken()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.CreateClient().GetAsync("/api/reservas")).StatusCode);
        var ama = await Cliente("amadellaves@oasisstay.co", FabricaApi.ClaveAma);
        Assert.Equal(HttpStatusCode.Forbidden, (await ama.GetAsync("/api/reservas")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ama.GetAsync("/api/huespedes/buscar?tipoDocumento=CC&documento=1")).StatusCode);
        var admin = await Cliente("admin@oasisstay.co", FabricaApi.ClaveAdmin);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/reservas")).StatusCode);
    }

    [Fact] // [JIRA US-03 ESC1][JIRA US-04 ESC1][DS1]
    public async Task Api_CrearReserva_201_Confirmada_Y_ListaYBitacora()
    {
        var c = await Recepcion();
        var r = await c.PostAsJsonAsync("/api/reservas", Solicitud("1020304050", 304, "2026-12-10", "2026-12-14"));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var j = await Json(r);
        Assert.Equal("Confirmada", j.GetProperty("estado").GetString());
        var id = j.GetProperty("idReserva").GetInt32();

        var lista = await Json(await c.GetAsync("/api/reservas?texto=Carlos"));
        Assert.Contains(lista.EnumerateArray(), e => e.GetProperty("idReserva").GetInt32() == id && e.GetProperty("habitacion").GetInt32() == 304);

        using var scope = _fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
        Assert.Equal(EstadoHab(db, 304), "RESERVADA");
        Assert.Contains(db.HistorialReservas, h => h.IdReserva == id && h.Usuario != "" && h.AccionRealizada == "Reserva creada");
    }

    private static string EstadoHab(OasisStayDbContext db, int n) => db.Habitaciones.Single(h => h.NumeroHabitacion == n).Estado.ToString();

    [Fact] // [JIRA US-03 ESC3]
    public async Task Api_CrearReserva_CamposInvalidos_400PorCampo()
    {
        var c = await Recepcion();
        var s = Solicitud("2000", 102, "2026-12-10", "2026-12-14");
        s.Correo = "carlos.mendoza.com"; s.Telefono = "310ABC";
        var r = await c.PostAsJsonAsync("/api/reservas", s);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var errores = (await Json(r)).GetProperty("errors");
        Assert.True(errores.TryGetProperty("correo", out _));
        Assert.True(errores.TryGetProperty("telefono", out _));
        Assert.False(errores.TryGetProperty("nombre", out _));
    }

    [Fact] // [DS1 alt][CP-R02]
    public async Task Api_CrearReserva_FechasCapacidadSolapamiento_MensajesExactos()
    {
        var c = await Recepcion();
        var f = await c.PostAsJsonAsync("/api/reservas", Solicitud("3000", 203, "2026-12-14", "2026-12-10", 1));
        Assert.Equal(HttpStatusCode.BadRequest, f.StatusCode);
        Assert.Equal("La fecha de salida debe ser posterior a la fecha de llegada", (await Json(f)).GetProperty("detail").GetString());

        var cap = await c.PostAsJsonAsync("/api/reservas", Solicitud("3000", 203, "2026-12-10", "2026-12-14", 5));
        Assert.Equal(HttpStatusCode.BadRequest, cap.StatusCode);
        Assert.Equal("Excede la capacidad de la habitación (RN-05)", (await Json(cap)).GetProperty("detail").GetString());

        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/reservas", Solicitud("3000", 203, "2026-12-10", "2026-12-12", 1))).StatusCode);
        var ocupada = await c.PostAsJsonAsync("/api/reservas", Solicitud("3001", 203, "2026-12-11", "2026-12-13", 1));
        Assert.Equal("La habitación no está disponible para reservar", (await Json(ocupada)).GetProperty("detail").GetString());

        using (var scope = _fabrica.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
            db.Habitaciones.Single(h => h.NumeroHabitacion == 203).Estado = Dominio.EstadoHabitacion.DISPONIBLE;
            db.SaveChanges();
        }
        var sol = await c.PostAsJsonAsync("/api/reservas", Solicitud("3001", 203, "2026-12-11", "2026-12-13", 1));
        Assert.Equal(HttpStatusCode.Conflict, sol.StatusCode);
        Assert.Equal("Las fechas se solapan con otra reserva (RN-01)", (await Json(sol)).GetProperty("detail").GetString());
    }

    [Fact] // [JIRA US-03 ESC2]
    public async Task Api_Huespedes_BuscarYGuardar()
    {
        var c = await Recepcion();
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/huespedes/buscar?tipoDocumento=CC&documento=777")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/huespedes/buscar?tipoDocumento=XX&documento=777")).StatusCode);
        var nuevo = new HuespedSolicitud { TipoDocumento = "CC", Documento = "777", Nombre = "Ana Gómez", Telefono = "+57 300 000 0000", Correo = "ana@email.com" };
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/huespedes", nuevo)).StatusCode);
        nuevo.Nombre = "Ana María Gómez";
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/huespedes", nuevo)).StatusCode);
        var b = await Json(await c.GetAsync("/api/huespedes/buscar?tipoDocumento=CC&documento=777"));
        Assert.Equal("Ana María Gómez", b.GetProperty("nombre").GetString());
        Assert.Equal("ana@email.com", b.GetProperty("correo").GetString());

        nuevo.Correo = "sin-arroba";
        var malo = await c.PostAsJsonAsync("/api/huespedes", nuevo);
        Assert.Equal(HttpStatusCode.BadRequest, malo.StatusCode);
        Assert.True((await Json(malo)).GetProperty("errors").TryGetProperty("correo", out _));
    }

    [Fact] // [D-21][JIRA US-04 ESC3/RN3]
    public async Task Api_Reasignar_UpgradeRequiereConfirmacion()
    {
        var c = await Recepcion();
        var id = (await Json(await c.PostAsJsonAsync("/api/reservas", Solicitud("4000", 102, "2026-12-10", "2026-12-14")))).GetProperty("idReserva").GetInt32();
        var sin = await c.PutAsJsonAsync($"/api/reservas/{id}/habitacion", new ReasignacionSolicitud { NumeroHabitacionNueva = 201 });
        Assert.Equal(HttpStatusCode.Conflict, sin.StatusCode);
        Assert.Equal("La habitación nueva es de categoría superior (Upgrade). Confirme para continuar.", (await Json(sin)).GetProperty("detail").GetString());
        var con = await c.PutAsJsonAsync($"/api/reservas/{id}/habitacion", new ReasignacionSolicitud { NumeroHabitacionNueva = 201, ConfirmarUpgrade = true });
        Assert.Equal(HttpStatusCode.OK, con.StatusCode);
        using var scope = _fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
        Assert.Equal("DISPONIBLE", EstadoHab(db, 102));
        Assert.Equal("RESERVADA", EstadoHab(db, 201));
        Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync("/api/reservas/99999/habitacion", new ReasignacionSolicitud { NumeroHabitacionNueva = 201 })).StatusCode);
    }

    [Fact] // [DE#4][D-31][CP-R06]
    public async Task Api_CheckIn_SinDocumento_409_ConDocumento_200()
    {
        var c = await Recepcion();
        var hoy = DateTime.Today.ToString("yyyy-MM-dd");
        using (var scope = _fabrica.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
            db.Habitaciones.Add(new Dominio.Habitacion { NumeroHabitacion = 105, Piso = 1, IdTipo = db.TiposHabitacion.First(t => t.Nombre == "Doble Clásica").IdTipo });
            db.SaveChanges();
        }
        var id = (await Json(await c.PostAsJsonAsync("/api/reservas", Solicitud("5000", 105, hoy, DateTime.Today.AddDays(2).ToString("yyyy-MM-dd")))))
            .GetProperty("idReserva").GetInt32();
        var sin = await c.PostAsJsonAsync($"/api/reservas/{id}/check-in", new CheckInSolicitud { Documento = "" });
        Assert.Equal(HttpStatusCode.Conflict, sin.StatusCode);
        Assert.Equal("Debe verificar el documento del huésped", (await Json(sin)).GetProperty("detail").GetString());
        var ok = await c.PostAsJsonAsync($"/api/reservas/{id}/check-in", new CheckInSolicitud { Documento = "5000" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var j = await Json(ok);
        Assert.Equal("EnCurso", j.GetProperty("estadoReserva").GetString());
        Assert.Equal("OCUPADO", j.GetProperty("estadoHabitacion").GetString());
    }
}
