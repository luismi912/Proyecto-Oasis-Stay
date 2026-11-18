using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OasisStay.Api.Contratos;
using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Repositorios;
using OasisStay.Infraestructura.Seguridad;
using OasisStay.Infraestructura.Semilla;
using OasisStay.Pruebas.F0;
using OasisStay.Pruebas.F1;
using OasisStay.Pruebas.F2;

namespace OasisStay.Pruebas.F5;

/// <summary>Base propia OasisStay_PruebasF5 (se borra al terminar) con los tipos de la semilla inicial [D-29][D-39].</summary>
public sealed class BancoF5 : IAsyncLifetime
{
    public static readonly string Cadena = NombresBasePruebas.Cadena("F5");

    public static OasisStayDbContext Contexto() =>
        new(new DbContextOptionsBuilder<OasisStayDbContext>().UseSqlServer(Cadena).Options, new CifradorAes256(Banco.Clave));

    public async Task InitializeAsync()
    {
        await using var db = Contexto();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        db.TiposHabitacion.AddRange(
            new TipoHabitacion { Nombre = "Individual", CapacidadMax = 1, PrecioBase = 120000m },
            new TipoHabitacion { Nombre = "Doble Clásica", CapacidadMax = 2, PrecioBase = 185000m },
            new TipoHabitacion { Nombre = "Suite Ejecutiva", CapacidadMax = 4, PrecioBase = 320000m });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = Contexto();
        await db.Database.EnsureDeletedAsync();
    }

    public static GestionEstadoHabitacion Servicio(OasisStayDbContext db)
        => new(new RepositorioHabitaciones(db), new RepositorioTiposHabitacion(db), new UnidadDeTrabajoEf(db), new RepositorioReservas(db));
}

/// <summary>Datos de escenario de US-09 sembrados con --semilla-demo --escenario US09 [D-39][D-41][JIRA US-09 datos].</summary>
[Collection("SqlServer")]
public class SemillaF5Pruebas : IClassFixture<BancoF5>
{
    private static async Task Escenario()
    {
        await using var db = BancoF5.Contexto();
        await new EjecutorSemillaDemo(new ISemillaDemo[] { new SemillaDemoF5() }).EjecutarAsync(db, "US09");
    }

    [Fact] // [D-41][JIRA US-09 datos]
    public async Task Semilla_PorEscenario_US09()
    {
        await Escenario();
        await Escenario(); // reinicia sin duplicar
        using var db = BancoF5.Contexto();
        var mapa = BancoF5.Servicio(db).ObtenerDistribucionHabitaciones();

        Assert.Equal(30, mapa.Count);
        Assert.Equal(new[] { 1, 2, 3 }, mapa.Select(m => m.Piso).Distinct().Order().ToArray());
        Assert.Equal(10, mapa.Count(m => m.Estado == "Disponible"));
        Assert.Equal(15, mapa.Count(m => m.Estado == "Ocupada"));
        Assert.Equal(3, mapa.Count(m => m.Estado == "EnLimpieza"));
        Assert.Equal(2, mapa.Count(m => m.Estado == "Mantenimiento"));
        var h302 = mapa.Single(m => m.Numero == 302);
        Assert.Equal("Ocupada", h302.Estado);
        Assert.NotNull(h302.Reserva);
        Assert.False(string.IsNullOrWhiteSpace(h302.Reserva!.Huesped));
        Assert.True(h302.Reserva.FechaSalida > DateTime.Today.AddDays(-1));
        Assert.Equal(15, mapa.Count(m => m.Reserva != null));
    }

    [Fact] // [D-41][JIRA US-09 datos] partiendo de una base con datos de otros escenarios, quedan exactamente las 30
    public async Task Semilla_PorEscenario_US09_DesdeBaseConOtrosEscenarios_Exactamente30()
    {
        var todas = new ISemillaDemo[] { new SemillaDemoF1(), new SemillaDemoF2(), new SemillaDemoF3(), new SemillaDemoF5() };
        await using (var db = BancoF5.Contexto())
        {
            var ejecutor = new EjecutorSemillaDemo(todas);
            await ejecutor.EjecutarAsync(db);
            await ejecutor.EjecutarAsync(db, "US06");
            await ejecutor.EjecutarAsync(db, "US10");
            Assert.True(await db.Habitaciones.CountAsync() > 30);
            await ejecutor.EjecutarAsync(db, "US09");
        }
        using var v = BancoF5.Contexto();
        var mapa = BancoF5.Servicio(v).ObtenerDistribucionHabitaciones();
        Assert.Equal(30, mapa.Count);
        Assert.Equal(Enumerable.Range(1, 3).SelectMany(p => Enumerable.Range(1, 10).Select(i => p * 100 + i)).ToArray(),
            mapa.Select(m => m.Numero).Order().ToArray());
        Assert.Equal(new[] { 10, 15, 3, 2 }, new[]
        {
            mapa.Count(m => m.Estado == "Disponible"), mapa.Count(m => m.Estado == "Ocupada"),
            mapa.Count(m => m.Estado == "EnLimpieza"), mapa.Count(m => m.Estado == "Mantenimiento"),
        });
    }

    [Fact] // [JIRA US-09 datos][RN3] 10 habitaciones por piso; hay libres en el piso 2
    public async Task US09_Datos_Inventario30_10Verde15Rojo3Amarillo2Gris()
    {
        await Escenario();
        using var db = BancoF5.Contexto();
        var mapa = BancoF5.Servicio(db).ObtenerDistribucionHabitaciones();
        Assert.Equal(new[] { 10, 10, 10 }, mapa.GroupBy(m => m.Piso).OrderBy(g => g.Key).Select(g => g.Count()).ToArray());
        Assert.Equal(new[] { 10, 15, 3, 2 }, new[]
        {
            mapa.Count(m => m.Estado == "Disponible"), mapa.Count(m => m.Estado == "Ocupada"),
            mapa.Count(m => m.Estado == "EnLimpieza"), mapa.Count(m => m.Estado == "Mantenimiento"),
        });
        Assert.Contains(mapa, m => m.Piso == 2 && m.Estado == "Disponible");
    }
}

public class MapaApiPruebas : IClassFixture<FabricaApiF1>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly FabricaApiF1 _f;
    public MapaApiPruebas(FabricaApiF1 f) => _f = f;

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

    private void PrepararHuesped302()
        => _f.Preparar(db =>
        {
            if (db.Reservas.Any(r => r.NumeroHabitacion == 302)) return;
            var cli = new Cliente { Nombre = "Laura Castillo", Documento = "1010211", Telefono = "3100000000", Correo = "laura@email.com" };
            db.Clientes.Add(cli);
            db.SaveChanges();
            db.Reservas.Add(new Reserva
            {
                IdCliente = cli.IdCliente, NumeroHabitacion = 302, Estado = EstadoReserva.EnCurso, Huespedes = 1,
                FechaInicio = new DateTime(2026, 11, 15), FechaFin = new DateTime(2026, 11, 18),
            });
            db.Reservas.Add(new Reserva
            {
                IdCliente = cli.IdCliente, NumeroHabitacion = 301, Estado = EstadoReserva.Confirmada, Huespedes = 1,
                FechaInicio = new DateTime(2026, 12, 1), FechaFin = new DateTime(2026, 12, 3),
            });
        });

    [Fact] // [JIRA US-09 ESC1/RN1]
    public async Task US09_ESC1_Visualizacion_AgrupadaPorPisos_ConColoresDeEstado()
    {
        var http = await Cliente("Recepcionista");
        var r = await http.GetAsync("/api/habitaciones/distribucion");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var lista = (await r.Content.ReadFromJsonAsync<List<DistribucionHabitacionRespuesta>>(Web))!;
        Assert.Equal(new[] { 1, 2, 3 }, lista.Select(h => h.Piso).Distinct().Order().ToArray());
        Assert.All(lista, h => Assert.Contains(h.Estado, new[] { "Disponible", "Reservada", "Ocupada", "EnLimpieza", "Mantenimiento" }));
        var porPiso = lista.GroupBy(h => h.Piso).ToDictionary(g => g.Key, g => g.Select(h => h.Numero).ToArray());
        Assert.All(porPiso[2], n => Assert.InRange(n, 200, 299));
        Assert.Contains(lista, h => h.Numero == 101 && h.Estado == "Ocupada" && h.Tipo == "Suite Ejecutiva" && h.PrecioNoche == 320000m);
    }

    [Fact] // [JIRA US-09 ESC2/RN2]
    public async Task US09_ESC2_Hab302Ocupada_DevuelveHuespedYSalidaPrevista()
    {
        PrepararHuesped302();
        var http = await Cliente("Administrador");
        var lista = (await http.GetFromJsonAsync<List<DistribucionHabitacionRespuesta>>("/api/habitaciones/distribucion", Web))!;

        var h302 = lista.Single(h => h.Numero == 302);
        Assert.Equal("Ocupada", h302.Estado);
        Assert.Equal("Doble Clásica", h302.Tipo);
        Assert.NotNull(h302.Reserva);
        Assert.Equal("Laura Castillo", h302.Reserva!.Huesped);
        Assert.Equal(new DateTime(2026, 11, 15), h302.Reserva.FechaLlegada);
        Assert.Equal(new DateTime(2026, 11, 18), h302.Reserva.FechaSalida);
        Assert.True(h302.Reserva.IdReserva > 0);
        // Reservada: lleva la reserva confirmada; Disponible: sin reserva (el panel deshabilita Check-in/Check-out)
        Assert.NotNull(lista.Single(h => h.Numero == 301).Reserva);
        Assert.Null(lista.Single(h => h.Numero == 102).Reserva);
    }

    [Fact] // [JIRA US-09 ESC3][DS5] el filtro es local: la respuesta ya trae piso y estado de todas
    public async Task US09_ESC3_FiltroPiso2Disponible_SoloLibresDelPiso2()
    {
        var http = await Cliente("Recepcionista");
        var lista = (await http.GetFromJsonAsync<List<DistribucionHabitacionRespuesta>>("/api/habitaciones/distribucion", Web))!;

        var libres = lista.Where(h => h.Piso == 2 && h.Estado == "Disponible").Select(h => h.Numero).ToArray();
        Assert.NotEmpty(libres);
        Assert.All(libres, n => Assert.InRange(n, 200, 299));
        Assert.DoesNotContain(lista.Where(h => h.Piso == 2 && h.Estado != "Disponible"), h => libres.Contains(h.Numero));
        // la lista completa ya cubre los otros pisos y estados: no hace falta volver al servidor
        Assert.True(lista.Select(h => h.Piso).Distinct().Count() == 3);
    }

    [Fact] // [D-34]
    public async Task US09_Distribucion_Recepcionista_Ok_AmaDeLlaves_403()
    {
        Assert.Equal(HttpStatusCode.OK, (await (await Cliente("Recepcionista")).GetAsync("/api/habitaciones/distribucion")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await Cliente("Administrador")).GetAsync("/api/habitaciones/distribucion")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await Cliente("AmaDeLlaves")).GetAsync("/api/habitaciones/distribucion")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().GetAsync("/api/habitaciones/distribucion")).StatusCode);
    }
}

public class MapaUnidadPruebas
{
    private sealed class TiposVacios : IRepositorioTiposHabitacion
    {
        public IReadOnlyList<TipoHabitacion> ConsultarTodos() => Array.Empty<TipoHabitacion>();
        public TipoHabitacion? BuscarPorId(int idTipo) => null;
    }

    private sealed class HabitacionesEspia : IRepositorioHabitaciones
    {
        public int Todas, Otras;
        public List<Habitacion> Datos { get; } = new();
        public IReadOnlyList<Habitacion> ConsultarTodas() { Todas++; return Datos; }
        public IReadOnlyList<Habitacion> BuscarLibres(DateTime a, DateTime b, TipoHabitacion? t, int n) { Otras++; return Datos; }
        public Habitacion? BuscarPorNumero(int numero) { Otras++; return null; }
        public void Guardar(Habitacion habitacion) { Otras++; }
    }

    private sealed class ReservasEspia : IRepositorioReservas
    {
        public int Listados, Otras;
        public List<Reserva> Datos { get; } = new();
        public IReadOnlyList<Reserva> Listar(EstadoReserva? estado, string? texto) { Listados++; return Datos.Where(r => estado == null || r.Estado == estado).ToList(); }
        public IReadOnlyList<Reserva> BuscarPorHabitacionYFechas(Habitacion h, DateTime a, DateTime b) { Otras++; return Datos; }
        public void Guardar(Reserva reserva) { Otras++; }
        public Reserva? BuscarPorId(int idReserva) { Otras++; return null; }
        public IReadOnlyList<Reserva> ListarConfirmadasHasta(DateTime corte) { Otras++; return Array.Empty<Reserva>(); } // F4 [D-44]
    }

    [Fact] // [JIRA US-09 NFR] una consulta de habitaciones y una por estado de reserva, sin N+1
    public void US09_Distribucion_ConsultarTodas_UnaSolaConsulta()
    {
        var tipo = new TipoHabitacion { IdTipo = 1, Nombre = "Doble Clásica", CapacidadMax = 2, PrecioBase = 185000m };
        var hab = new HabitacionesEspia();
        var res = new ReservasEspia();
        for (var i = 1; i <= 30; i++)
        {
            var n = 100 + i;
            hab.Datos.Add(new Habitacion { NumeroHabitacion = n, Piso = 1 + (i - 1) / 10, IdTipo = 1, Tipo = tipo, Estado = EstadoHabitacion.OCUPADO });
            res.Datos.Add(new Reserva
            {
                IdReserva = i, NumeroHabitacion = n, Estado = EstadoReserva.EnCurso, Cliente = new Cliente { Nombre = "Huésped " + i },
                FechaInicio = new DateTime(2026, 11, 15), FechaFin = new DateTime(2026, 11, 18),
            });
        }
        var servicio = new GestionEstadoHabitacion(hab, new TiposVacios(), new UnidadDeTrabajoNula(), res);

        var mapa = servicio.ObtenerDistribucionHabitaciones();

        Assert.Equal(30, mapa.Count);
        Assert.All(mapa, m => Assert.NotNull(m.Reserva));
        Assert.Equal(1, hab.Todas);
        Assert.Equal(0, hab.Otras);
        Assert.InRange(res.Listados, 1, 2);
        Assert.Equal(0, res.Otras);
    }
}
