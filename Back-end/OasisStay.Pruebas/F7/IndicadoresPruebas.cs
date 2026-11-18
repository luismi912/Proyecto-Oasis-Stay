using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OasisStay.Api.Contratos;
using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Pruebas.F0;
using OasisStay.Pruebas.F3;

namespace OasisStay.Pruebas.F7;

/// <summary>Indicadores reales de Facturación e Historial [D-48][JIRA US-11 RN2][D-45][D-34].</summary>
[Collection("SqlServer")]
public class IndicadoresPruebas : IClassFixture<FabricaApiF3>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static int _doc = 8100000;
    private readonly FabricaApiF3 _fabrica;
    public IndicadoresPruebas(FabricaApiF3 fabrica) => _fabrica = fabrica;

    private async Task<HttpClient> Cliente(string usuario, string clave)
    {
        var c = _fabrica.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new LoginSolicitud { Usuario = usuario, Contrasena = clave });
        var t = (await r.Content.ReadFromJsonAsync<LoginRespuesta>(Web))!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t.Token);
        return c;
    }

    private Task<HttpClient> Recepcion() => Cliente("recepcion@oasisstay.co", FabricaApi.ClaveRecepcion);
    private static async Task<JsonElement> Json(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private int ReservaEnCurso(int habitacion)
    {
        using var scope = _fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
        db.Habitaciones.Single(x => x.NumeroHabitacion == habitacion).Estado = EstadoHabitacion.OCUPADO;
        var r = new Reserva
        {
            Cliente = new Cliente { TipoDocumento = TipoDocumento.CC, Documento = (++_doc).ToString(), Nombre = "Laura Castillo", Telefono = "3101234567", Correo = "laura@email.com" },
            NumeroHabitacion = habitacion, Huespedes = 1, Estado = EstadoReserva.EnCurso,
            FechaInicio = DateTime.Today.AddDays(-1), FechaFin = DateTime.Today,
        };
        db.Reservas.Add(r);
        db.SaveChanges();
        return r.IdReserva;
    }

    private static async Task<(decimal Ingresos, int Facturas, decimal PorCobrar, int Cuentas, int Completadas)> Leer(HttpClient c)
    {
        var r = await c.GetAsync("/api/facturacion/indicadores");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var j = await Json(r);
        return (j.GetProperty("ingresosMes").GetDecimal(), j.GetProperty("facturasMes").GetInt32(), j.GetProperty("porCobrar").GetDecimal(),
            j.GetProperty("cuentasAbiertas").GetInt32(), j.GetProperty("reservasCompletadasMes").GetInt32());
    }

    [Fact] // [D-48][JIRA US-11 RN2]
    public async Task F7_Indicadores_SumaFacturasDelMes_CuadraConFacturas()
    {
        var c = await Recepcion();
        var id = ReservaEnCurso(204);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/reservas/{id}/checkout", new { metodoPago = "EFECTIVO", datosPago = new { montoRecibido = 900000m } })).StatusCode);

        using var scope = _fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
        var hoy = DateTime.Today;
        var mes = db.Facturas.AsEnumerable().Where(f => f.FechaEmision.Year == hoy.Year && f.FechaEmision.Month == hoy.Month).ToList();
        var i = await Leer(c);
        Assert.Equal(mes.Sum(f => f.ValorTotal), i.Ingresos);
        Assert.Equal(mes.Count, i.Facturas);
        Assert.Equal(mes.Count, i.Completadas);
    }

    [Fact] // [D-48]
    public async Task F7_Indicadores_PorCobrar_SumaSaldoDeReservasEnCurso()
    {
        var c = await Recepcion();
        var antes = await Leer(c);
        var id = ReservaEnCurso(202);
        var totales = await Json(await c.GetAsync($"/api/facturacion/{id}/totales"));
        var saldo = totales.GetProperty("saldo").GetDecimal();
        Assert.True(saldo > 0);

        var despues = await Leer(c);
        Assert.Equal(antes.PorCobrar + saldo, despues.PorCobrar);
        Assert.Equal(antes.Cuentas + 1, despues.Cuentas);
    }

    [Fact] // [D-48]
    public async Task F7_Indicadores_EmitirFactura_ActualizaIngresosYCantidad()
    {
        var c = await Recepcion();
        var id = ReservaEnCurso(203);
        var antes = await Leer(c);
        var cierre = await c.PostAsJsonAsync($"/api/reservas/{id}/checkout", new { metodoPago = "EFECTIVO", datosPago = new { montoRecibido = 900000m } });
        Assert.Equal(HttpStatusCode.OK, cierre.StatusCode);
        var total = (await Json(cierre)).GetProperty("factura").GetProperty("valorTotal").GetDecimal();

        var despues = await Leer(c);
        Assert.Equal(antes.Ingresos + total, despues.Ingresos);
        Assert.Equal(antes.Facturas + 1, despues.Facturas);
        Assert.Equal(antes.Completadas + 1, despues.Completadas);
        Assert.Equal(antes.Cuentas - 1, despues.Cuentas);
    }

    [Fact] // [D-48]: sin facturas del mes se muestran 0, no se oculta
    public void F7_Indicadores_SinFacturasDelMes_Devuelve0()
    {
        var servicio = new ServicioIndicadores(new RepoVacio(), new FacturacionVacia(), new RelojFijo());
        var i = servicio.Consultar();
        Assert.Equal(new IndicadoresFacturacion(0m, 0, 0m, 0, 0), i);
    }

    [Fact] // [D-48] el rango es el mes del reloj
    public void F7_Indicadores_UsaElMesDelReloj()
    {
        var repo = new RepoVacio();
        new ServicioIndicadores(repo, new FacturacionVacia(), new RelojFijo()).Consultar();
        Assert.Equal(new DateTime(2026, 11, 1), repo.Desde);
        Assert.Equal(new DateTime(2026, 11, 30), repo.Hasta);
    }

    [Fact] // [D-34]
    public async Task F7_Indicadores_AmaDeLlaves_403()
    {
        var ama = await Cliente("amadellaves@oasisstay.co", FabricaApi.ClaveAma);
        Assert.Equal(HttpStatusCode.Forbidden, (await ama.GetAsync("/api/facturacion/indicadores")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.CreateClient().GetAsync("/api/facturacion/indicadores")).StatusCode);
    }

    [Fact] // [D-34]
    public async Task F7_Indicadores_Recepcionista_200()
        => Assert.Equal(HttpStatusCode.OK, (await (await Recepcion()).GetAsync("/api/facturacion/indicadores")).StatusCode);

    [Fact] // [D-34]
    public async Task F7_Indicadores_Administrador_200()
        => Assert.Equal(HttpStatusCode.OK, (await (await Cliente("admin@oasisstay.co", FabricaApi.ClaveAdmin)).GetAsync("/api/facturacion/indicadores")).StatusCode);

    [Fact] // [Hallazgo][D-45]: ninguna cifra fija heredada del Figma
    public void F7_Pantallas_NoContienenCifrasFijas()
    {
        foreach (var nombre in new[] { "Facturacion.cshtml", "Historial.cshtml" })
        {
            var html = Pantalla(nombre);
            foreach (var fijo in new[] { "48,2M", "48.260.500", "128", "12,4", "14 cuentas", "6\n", "pendientes hoy", "2,4M" })
                Assert.DoesNotContain(fijo, html);
        }
    }

    [Fact] // [JIRA US-01 ESC2/ESC3][F7 obs. 2]: cada búsqueda limpia aviso y resumen anteriores; secuencia comprobada además en pantalla
    public void F7_ModalNuevaReserva_CadaBusquedaLimpiaAvisoYResumenAnteriores()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Front-end", "wwwroot", "js", "site.js"))) dir = dir.Parent;
        var js = File.ReadAllText(Path.Combine(dir!.FullName, "Front-end", "wwwroot", "js", "site.js"));
        var inicio = js.IndexOf("const cargarHabitaciones", StringComparison.Ordinal);
        var primeraPeticion = js.IndexOf("fetch(`?handler=Disponibilidad", inicio, StringComparison.Ordinal);
        var limpieza = js.IndexOf("limpiarAvisos();", inicio, StringComparison.Ordinal);
        Assert.True(inicio > 0 && limpieza > inicio && limpieza < primeraPeticion, "la búsqueda debe limpiar avisos antes de consultar");
        Assert.Contains("resumenSinHabitacion", js);
        Assert.Contains("alerta.textContent = \"\"", js);
    }

    [Fact] // [D-48]: singular con 1, plural en los demás casos (incluido 0)
    public void F7_Historial_Banner_ConcuerdaSingularYPlural()
    {
        var html = Pantalla("Historial.cshtml");
        Assert.Contains("== 1 ? \"reserva completada\" : \"reservas completadas\"", html);
        Assert.DoesNotContain("ReservasCompletadasMes reservas completadas", html);
    }

    private static string Pantalla(string nombre)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Front-end", "Pages", "Ventanas", nombre))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "Front-end", "Pages", "Ventanas", nombre));
    }

    private sealed class RelojFijo : IReloj { public DateTime Ahora => new(2026, 11, 17, 10, 0, 0); }

    private sealed class FacturacionVacia : IServicioFacturacion
    {
        public ResumenCobro CalcularTotales(int idReserva) => throw new NotSupportedException();
        public CotizacionResultado Cotizar(SolicitudCotizacion solicitud) => throw new NotSupportedException();
        public IReadOnlyList<PendienteCobro> ConsultarPendientes() => Array.Empty<PendienteCobro>();
        public ConsumoReserva AgregarConsumo(int idReserva, string concepto, int cantidad, decimal valorUnitario) => throw new NotSupportedException();
        public Factura CerrarCuenta(int idReserva, string? metodoPago, DatosPago datos) => throw new NotSupportedException();
        public byte[] ObtenerPdf(int idFactura) => throw new NotSupportedException();
    }

    private sealed class RepoVacio : IRepositorioReportes
    {
        public DateTime Desde, Hasta;
        public IReadOnlyList<FilaReporte> FacturasCerradas(DateTime desde, DateTime hasta) { Desde = desde; Hasta = hasta; return Array.Empty<FilaReporte>(); }
        public IReadOnlyList<ResumenMetodoPago> PagosPorMetodo(DateTime desde, DateTime hasta) => Array.Empty<ResumenMetodoPago>();
        public IReadOnlyList<IngresoDia> IngresoPorDia(DateTime desde, DateTime hasta) => Array.Empty<IngresoDia>();
        public decimal PenalidadesDelRango(DateTime desde, DateTime hasta) => 0m;
        public int NochesOcupadas(DateTime desde, DateTime hasta) => 0;
        public int TotalHabitaciones() => 0;
    }
}
