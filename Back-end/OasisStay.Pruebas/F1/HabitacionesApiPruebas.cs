using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OasisStay.Api.Contratos;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Pruebas.F0;

namespace OasisStay.Pruebas.F1;

/// <summary>Host de F1: BD en memoria que tolera la transacción de la unidad de trabajo.</summary>
public class FabricaApiF1 : FabricaApi
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(d => d.ServiceType.IsGenericType &&
                         d.ServiceType.GenericTypeArguments.Contains(typeof(OasisStayDbContext))).ToList())
                services.Remove(d);
            var nombre = "OasisStayF1_" + Guid.NewGuid();
            services.AddDbContext<OasisStayDbContext>(o => o.UseInMemoryDatabase(nombre)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        });
    }

    public void Preparar(Action<OasisStayDbContext> accion)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
        accion(db);
        db.SaveChanges();
    }

    public T Leer<T>(Func<OasisStayDbContext, T> consulta)
    {
        using var scope = Services.CreateScope();
        return consulta(scope.ServiceProvider.GetRequiredService<OasisStayDbContext>());
    }
}

public class HabitacionesApiPruebas : IClassFixture<FabricaApiF1>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly FabricaApiF1 _f;
    public HabitacionesApiPruebas(FabricaApiF1 f) => _f = f;

    private async Task<HttpClient> Cliente(string rol)
    {
        var (u, c) = rol switch
        {
            "Administrador" => ("admin@oasisstay.co", FabricaApi.ClaveAdmin),
            "Recepcionista" => ("recepcion@oasisstay.co", FabricaApi.ClaveRecepcion),
            _ => ("amadellaves@oasisstay.co", FabricaApi.ClaveAma),
        };
        var http = _f.CreateClient();
        var r = await http.PostAsJsonAsync("/api/auth/login", new LoginSolicitud { Usuario = u, Contrasena = c });
        var token = (await r.Content.ReadFromJsonAsync<LoginRespuesta>(Web))!.Token;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    private static async Task<string?> Detalle(HttpResponseMessage r)
    {
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("detail", out var d) ? d.GetString() : null;
    }

    private void PonerEstado(int numero, EstadoHabitacion e)
        => _f.Preparar(db => db.Habitaciones.Find(numero)!.Estado = e);

    private EstadoHabitacion EstadoEnBd(int numero) => _f.Leer(db => db.Habitaciones.Find(numero)!.Estado);

    // ---------- US-01 ----------

    [Fact] // [JIRA US-01 ESC1/RN1/RN3]
    public async Task US01_ESC1_ConsultaSuite_10al12Nov2026_ListaConNumeroCapacidadYPrecio()
    {
        // Suites: 101 (Ocupada, no sale), 201 y 304 (Disponibles), 301 (Reservada, no sale).
        // 201 tiene una reserva solapada (no sale); se añade la 305 en Mantenimiento (no sale).
        _f.Preparar(db =>
        {
            var suite = db.TiposHabitacion.First(t => t.Nombre == "Suite Ejecutiva");
            if (db.Habitaciones.Find(305) is null)
                db.Habitaciones.Add(new Habitacion { NumeroHabitacion = 305, Piso = 3, IdTipo = suite.IdTipo, Estado = EstadoHabitacion.MANTENIMIENTO });
            if (db.Habitaciones.Find(306) is null)
                db.Habitaciones.Add(new Habitacion { NumeroHabitacion = 306, Piso = 3, IdTipo = suite.IdTipo, Estado = EstadoHabitacion.DISPONIBLE });
            var cli = new Cliente { Nombre = "Prueba", Documento = "1", Telefono = "1", Correo = "a@b.co" };
            db.Clientes.Add(cli);
            db.SaveChanges();
            db.Reservas.Add(new Reserva { FechaInicio = new DateTime(2026, 11, 11), FechaFin = new DateTime(2026, 11, 13), NumeroHabitacion = 201, IdCliente = cli.IdCliente, Estado = EstadoReserva.Confirmada });
            // Cancelada no bloquea: 306 sigue libre
            db.Reservas.Add(new Reserva { FechaInicio = new DateTime(2026, 11, 10), FechaFin = new DateTime(2026, 11, 12), NumeroHabitacion = 306, IdCliente = cli.IdCliente, Estado = EstadoReserva.Cancelada });
        });
        var http = await Cliente("Recepcionista");

        var r = await http.GetAsync("/api/habitaciones/disponibilidad?fechaInicio=2026-11-10&fechaFin=2026-11-12&tipo=Suite&huespedes=2");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var res = (await r.Content.ReadFromJsonAsync<DisponibilidadRespuesta>(Web))!;
        Assert.Equal(new[] { 304, 306 }, res.Habitaciones.Select(h => h.Numero).ToArray());
        Assert.All(res.Habitaciones, h => { Assert.Equal(4, h.Capacidad); Assert.Equal(320000m, h.PrecioNoche); Assert.Equal("Suite Ejecutiva", h.Tipo); });
        Assert.Null(res.Mensaje);
    }

    [Fact] // [JIRA US-01 RN3]
    public async Task US01_ESC1_CapacidadInsuficiente_ExcluyeHabitaciones()
    {
        var http = await Cliente("Recepcionista");
        var res = (await http.GetFromJsonAsync<DisponibilidadRespuesta>("/api/habitaciones/disponibilidad?fechaInicio=2027-03-01&fechaFin=2027-03-03&tipo=Individual&huespedes=2", Web))!;
        Assert.Empty(res.Habitaciones);
    }

    [Fact] // [JIRA US-01 ESC2]
    public async Task US01_ESC2_EstandarAgotada_AlertaYSugerencias()
    {
        // Doble Clásica sin cupo en el rango; Suite e Individual con cupo.
        _f.Preparar(db =>
        {
            foreach (var h in db.Habitaciones.Include(x => x.Tipo).Where(x => x.Tipo!.Nombre == "Doble Clásica")) h.Estado = EstadoHabitacion.MANTENIMIENTO;
            foreach (var n in new[] { 203, 304, 201 }) db.Habitaciones.Find(n)!.Estado = EstadoHabitacion.DISPONIBLE;
        });
        var http = await Cliente("Recepcionista");

        var r = await http.GetAsync("/api/habitaciones/disponibilidad?fechaInicio=2027-04-01&fechaFin=2027-04-03&tipo=Doble&huespedes=2");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var res = (await r.Content.ReadFromJsonAsync<DisponibilidadRespuesta>(Web))!;
        Assert.Empty(res.Habitaciones);
        Assert.Equal("No hay habitaciones disponibles para ese criterio. Pruebe otras fechas u otro tipo de habitación.", res.Mensaje);
        Assert.Contains(res.Alternativas, a => a.Nombre == "Suite Ejecutiva");
        Assert.DoesNotContain(res.Alternativas, a => a.Nombre == "Doble Clásica");
    }

    [Theory] // [JIRA US-01 ESC3][D-19]
    [InlineData("2026-11-15", "2026-11-14")]
    [InlineData("2026-11-14", "2026-11-14")]
    public async Task US01_ESC3_FechasInvalidas_15y14Nov2026_Bloquea(string inicio, string fin)
    {
        var http = await Cliente("Recepcionista");
        var r = await http.GetAsync($"/api/habitaciones/disponibilidad?fechaInicio={inicio}&fechaFin={fin}&huespedes=2");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("La fecha de salida debe ser posterior a la fecha de llegada", await Detalle(r));
    }

    [Fact] // [D-34]
    public async Task US01_Disponibilidad_AmaDeLlaves_403_YSinToken_401()
    {
        var ama = await Cliente("AmaDeLlaves");
        Assert.Equal(HttpStatusCode.Forbidden, (await ama.GetAsync("/api/habitaciones/disponibilidad?fechaInicio=2027-01-01&fechaFin=2027-01-02&huespedes=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().GetAsync("/api/habitaciones")).StatusCode);
    }

    // ---------- US-02 ----------

    [Fact] // [JIRA US-02 ESC1]
    public async Task US02_ESC1_Hab301EnLimpiezaCompleta_PasaADisponibleVerde()
    {
        PonerEstado(301, EstadoHabitacion.EN_LIMPIEZA);
        var http = await Cliente("AmaDeLlaves");

        var r = await http.PutAsJsonAsync("/api/habitaciones/301/estado", new CambiarEstadoSolicitud { NuevoEstado = "Disponible" });

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var h = (await r.Content.ReadFromJsonAsync<HabitacionRespuesta>(Web))!;
        Assert.Equal("Disponible", h.Estado);
        Assert.Equal(EstadoHabitacion.DISPONIBLE, EstadoEnBd(301));
        var lista = (await http.GetFromJsonAsync<List<HabitacionRespuesta>>("/api/habitaciones?estado=Disponible", Web))!;
        Assert.Contains(lista, x => x.Numero == 301);
        Assert.All(lista, x => Assert.Equal("Disponible", x.Estado));
    }

    [Fact] // [JIRA US-02 ESC2/datos]
    public async Task US02_ESC2_MantenimientoSinMotivo_Bloquea()
    {
        _f.Preparar(db =>
        {
            if (db.Habitaciones.Find(105) is null)
                db.Habitaciones.Add(new Habitacion { NumeroHabitacion = 105, Piso = 1, IdTipo = db.TiposHabitacion.First().IdTipo, Estado = EstadoHabitacion.DISPONIBLE });
        });
        var http = await Cliente("AmaDeLlaves");

        foreach (var motivo in new string?[] { null, "", "corto" })
        {
            var r = await http.PutAsJsonAsync("/api/habitaciones/105/estado", new CambiarEstadoSolicitud { NuevoEstado = "Mantenimiento", Motivo = motivo });
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.Equal("Debe ingresar el motivo del mantenimiento (mínimo 10 caracteres)", await Detalle(r));
            Assert.Equal(EstadoHabitacion.DISPONIBLE, EstadoEnBd(105));
        }

        var ok = await http.PutAsJsonAsync("/api/habitaciones/105/estado", new CambiarEstadoSolicitud { NuevoEstado = "Mantenimiento", Motivo = "Fuga en grifería de baño principal" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(EstadoHabitacion.MANTENIMIENTO, EstadoEnBd(105));
        Assert.Equal("Fuga en grifería de baño principal", _f.Leer(db => db.Habitaciones.Find(105)!.MotivoMantenimiento));
    }

    [Fact] // [JIRA US-02 ESC3]
    public async Task US02_ESC3_Hab204Ocupada_ForzarDisponible_ImpideYAvisaCheckOut()
    {
        PonerEstado(204, EstadoHabitacion.OCUPADO);
        var http = await Cliente("AmaDeLlaves");

        var r = await http.PutAsJsonAsync("/api/habitaciones/204/estado", new CambiarEstadoSolicitud { NuevoEstado = "Disponible" });

        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal("La habitación está ocupada: requiere check-out previo", await Detalle(r));
        Assert.Equal(EstadoHabitacion.OCUPADO, EstadoEnBd(204));
    }

    [Fact] // [DE#1 §5][D-34]
    public async Task US02_Recepcionista_NoPuedeCompletarLimpieza()
    {
        PonerEstado(303, EstadoHabitacion.EN_LIMPIEZA);
        var rec = await Cliente("Recepcionista");
        var r = await rec.PutAsJsonAsync("/api/habitaciones/303/estado", new CambiarEstadoSolicitud { NuevoEstado = "Disponible" });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Equal(EstadoHabitacion.EN_LIMPIEZA, EstadoEnBd(303));

        var ama = await Cliente("AmaDeLlaves");
        Assert.Equal(HttpStatusCode.OK, (await ama.PutAsJsonAsync("/api/habitaciones/303/estado", new CambiarEstadoSolicitud { NuevoEstado = "Disponible" })).StatusCode);
    }

    [Fact] // [DS2][H-02]
    public async Task US02_TransicionNoPermitida_409_YNoExiste_404()
    {
        PonerEstado(102, EstadoHabitacion.DISPONIBLE);
        var http = await Cliente("Recepcionista");
        var r = await http.PutAsJsonAsync("/api/habitaciones/102/estado", new CambiarEstadoSolicitud { NuevoEstado = "Ocupada" });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal("La operación no está permitida en estado Disponible", await Detalle(r));
        Assert.Equal(EstadoHabitacion.DISPONIBLE, EstadoEnBd(102));

        Assert.Equal(HttpStatusCode.NotFound, (await http.PutAsJsonAsync("/api/habitaciones/999/estado", new CambiarEstadoSolicitud { NuevoEstado = "Disponible" })).StatusCode);
    }

    [Fact] // [E2 §4 US-01]
    public async Task US02_Listado_FiltraPorEstado_YTiposLosVeCualquierRol()
    {
        var http = await Cliente("AmaDeLlaves");
        var todas = (await http.GetFromJsonAsync<List<HabitacionRespuesta>>("/api/habitaciones", Web))!;
        Assert.True(todas.Count >= 12);
        var tipos = (await http.GetFromJsonAsync<List<TipoAlternativoRespuesta>>("/api/tipos-habitacion", Web))!;
        Assert.Equal(3, tipos.Count);
    }

    // ---------- D-16 ----------

    [Theory] // [D-28][D-34]
    [InlineData("Recepcionista")]
    [InlineData("AmaDeLlaves")]
    public async Task D16_CrearHabitacion_RecepcionistaYAmaDeLlaves_403(string rol)
    {
        var http = await Cliente(rol);
        var r = await http.PostAsJsonAsync("/api/habitaciones", new CrearHabitacionSolicitud { Numero = 401, Tipo = 1, Piso = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact] // [D-16][actividad-1][D-28]
    public async Task D16_CrearHabitacion_Valida_Guarda()
    {
        var http = await Cliente("Administrador");
        var idTipo = _f.Leer(db => db.TiposHabitacion.First(t => t.Nombre == "Doble Clásica").IdTipo);

        var r = await http.PostAsJsonAsync("/api/habitaciones", new CrearHabitacionSolicitud { Numero = 120, Tipo = idTipo, Piso = 1 });

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var h = (await r.Content.ReadFromJsonAsync<HabitacionRespuesta>(Web))!;
        Assert.Equal(185000m, h.PrecioNoche);
        Assert.Equal("Disponible", h.Estado);
        Assert.Equal(EstadoHabitacion.DISPONIBLE, EstadoEnBd(120));

        var repetida = await http.PostAsJsonAsync("/api/habitaciones", new CrearHabitacionSolicitud { Numero = 120, Tipo = idTipo, Piso = 1 });
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
    }

    [Theory] // [D-16][actividad-1][D-28]
    [InlineData(null, 1, 1)]
    [InlineData(130, null, 1)]
    [InlineData(130, 1, null)]
    [InlineData(130, 1, 4)]
    [InlineData(130, 1, 0)]
    [InlineData(130, 9999, 1)]
    public async Task D16_CrearHabitacion_DatosIncompletos_Errores(int? numero, int? tipo, int? piso)
    {
        var http = await Cliente("Administrador");
        var r = await http.PostAsJsonAsync("/api/habitaciones", new CrearHabitacionSolicitud { Numero = numero, Tipo = tipo, Piso = piso });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("Completa todos los datos de la habitación.", await Detalle(r));
    }
}
