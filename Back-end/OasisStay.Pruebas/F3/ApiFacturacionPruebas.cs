using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OasisStay.Api.Contratos;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Pruebas.F0;

namespace OasisStay.Pruebas.F3;

/// <summary>API sobre SQL Server real (OasisStay_PruebasF3Api, se borra al terminar) [D-06].</summary>
public class FabricaApiF3 : FabricaApi
{
    public const string CadenaApi = "Server=localhost;Database=OasisStay_PruebasF3Api;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(d => d.ServiceType.IsGenericType &&
                         d.ServiceType.GenericTypeArguments.Contains(typeof(OasisStayDbContext))).ToList())
                services.Remove(d);
            services.AddDbContext<OasisStayDbContext>(o => o.UseSqlServer(CadenaApi));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        using var db = new OasisStayDbContext(new DbContextOptionsBuilder<OasisStayDbContext>().UseSqlServer(CadenaApi).Options,
            new OasisStay.Infraestructura.Seguridad.CifradorAes256(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()));
        db.Database.EnsureDeleted();
    }
}

[Collection("SqlServer")]
public class ApiFacturacionPruebas : IClassFixture<FabricaApiF3>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static int _doc = 7000000;
    private readonly FabricaApiF3 _fabrica;
    public ApiFacturacionPruebas(FabricaApiF3 fabrica) => _fabrica = fabrica;

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

    /// <summary>Reserva EnCurso sobre la habitación sembrada (Individual 120000) con 1 noche; el IVA la lleva a 142800.</summary>
    private int ReservaEnCurso(int habitacion)
    {
        using var scope = _fabrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
        var h = db.Habitaciones.Single(x => x.NumeroHabitacion == habitacion);
        h.Estado = EstadoHabitacion.OCUPADO;
        var cliente = new Cliente
        {
            TipoDocumento = TipoDocumento.CC, Documento = (++_doc).ToString(), Nombre = "Laura Castillo",
            Telefono = "3101234567", Correo = "laura@email.com",
        };
        var r = new Reserva
        {
            Cliente = cliente, NumeroHabitacion = habitacion, Huespedes = 1, Estado = EstadoReserva.EnCurso,
            FechaInicio = DateTime.Today.AddDays(-1), FechaFin = DateTime.Today,
        };
        db.Reservas.Add(r);
        db.SaveChanges();
        return r.IdReserva;
    }

    [Fact] // [JIRA US-05 ESC1/ESC3][JIRA US-05 caso borde][D-34]
    public async Task Api_Cotizaciones_ReglasYRoles()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.CreateClient().PostAsJsonAsync("/api/cotizaciones", new { })).StatusCode);
        var ama = await Cliente("amadellaves@oasisstay.co", FabricaApi.ClaveAma);
        Assert.Equal(HttpStatusCode.Forbidden, (await ama.PostAsJsonAsync("/api/cotizaciones", new { })).StatusCode);

        var rec = await Recepcion();
        var ok = await rec.PostAsJsonAsync("/api/cotizaciones", new { numeroHabitacion = 102, fechaInicio = "2026-12-10", fechaFin = "2026-12-15" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var j = await Json(ok);
        Assert.Equal(5, j.GetProperty("noches").GetInt32());
        Assert.Equal(185000m * 5, j.GetProperty("subtotal").GetDecimal());
        Assert.Equal(1100750m, j.GetProperty("total").GetDecimal());

        var mal = await rec.PostAsJsonAsync("/api/cotizaciones", new { numeroHabitacion = 102, fechaInicio = "2026-12-10", fechaFin = "2026-12-10" });
        Assert.Equal(HttpStatusCode.BadRequest, mal.StatusCode);
        Assert.Equal("La fecha de salida debe ser posterior a la fecha de llegada", (await Json(mal)).GetProperty("detail").GetString());
    }

    [Fact] // [DS4][RF-10][D-33][RF-06][D-10][D-04]
    public async Task Api_FlujoCompleto_Consumo_Pago_Totales_Checkout_Pdf()
    {
        var rec = await Recepcion();
        var id = ReservaEnCurso(203); // Individual 120000

        var consumo = await rec.PostAsJsonAsync($"/api/reservas/{id}/consumos", new { concepto = "Minibar", cantidad = 1, monto = 30000m });
        Assert.Equal(HttpStatusCode.Created, consumo.StatusCode);

        var totales = await Json(await rec.GetAsync($"/api/facturacion/{id}/totales"));
        Assert.Equal(150000m, totales.GetProperty("subtotal").GetDecimal());
        Assert.Equal(178500m, totales.GetProperty("total").GetDecimal());
        Assert.Equal(178500m, totales.GetProperty("saldo").GetDecimal());

        var pendientes = await Json(await rec.GetAsync("/api/facturacion/pendientes"));
        Assert.Contains(pendientes.EnumerateArray(), x => x.GetProperty("idReserva").GetInt32() == id && x.GetProperty("estadoPago").GetString() == "Pendiente");

        // saldo pendiente: 409 con el mensaje acordado
        var bloqueo = await rec.PostAsJsonAsync($"/api/reservas/{id}/checkout", new { metodoPago = "", datosPago = new { } });
        Assert.Equal(HttpStatusCode.Conflict, bloqueo.StatusCode);
        Assert.Equal("La reserva tiene saldo pendiente de $178.500: registre el pago antes del check-out", (await Json(bloqueo)).GetProperty("detail").GetString());

        // pago rechazado: 400 con el motivo de la estrategia
        var rechazo = await rec.PostAsJsonAsync($"/api/reservas/{id}/pagos", new { metodoPago = "EFECTIVO", monto = 100000m, datosPago = new { montoRecibido = 50000m } });
        Assert.Equal(HttpStatusCode.BadRequest, rechazo.StatusCode);
        Assert.Equal("Monto insuficiente", (await Json(rechazo)).GetProperty("detail").GetString());

        // abono con tarjeta y recibo
        var abono = await rec.PostAsJsonAsync($"/api/reservas/{id}/pagos", new { metodoPago = "TARJETA_CREDITO", monto = 78500m, datosPago = new { numeroAutorizacion = "4892" } });
        Assert.Equal(HttpStatusCode.Created, abono.StatusCode);
        var recibo = await Json(abono);
        Assert.Equal(78500m, recibo.GetProperty("monto").GetDecimal());
        Assert.Equal(100000m, recibo.GetProperty("saldoRestante").GetDecimal());

        // check-out cobrando el saldo restante en efectivo con vuelto
        var cierre = await rec.PostAsJsonAsync($"/api/reservas/{id}/checkout", new { metodoPago = "EFECTIVO", datosPago = new { montoRecibido = 120000m } });
        Assert.Equal(HttpStatusCode.OK, cierre.StatusCode);
        var j = await Json(cierre);
        var factura = j.GetProperty("factura");
        Assert.True(factura.GetProperty("folio").GetInt32() > 0);
        Assert.Equal(178500m, factura.GetProperty("valorTotal").GetDecimal());
        var pdfUrl = j.GetProperty("pdfUrl").GetString()!;

        var pdf = await rec.GetAsync(pdfUrl);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync())[..4]));

        // segunda emisión: 409, sin segunda factura
        var doble = await rec.PostAsJsonAsync($"/api/reservas/{id}/checkout", new { metodoPago = "", datosPago = new { } });
        Assert.Equal(HttpStatusCode.Conflict, doble.StatusCode);
        // la reserva ya no acepta consumos
        Assert.Equal(HttpStatusCode.Conflict, (await rec.PostAsJsonAsync($"/api/reservas/{id}/consumos", new { concepto = "Minibar", cantidad = 1, monto = 1000m })).StatusCode);

        // el ama de llaves ve la habitación en limpieza
        var ama = await Cliente("amadellaves@oasisstay.co", FabricaApi.ClaveAma);
        var habs = await Json(await ama.GetAsync("/api/habitaciones?estado=EN_LIMPIEZA"));
        Assert.Contains(habs.EnumerateArray(), h => h.GetProperty("numero").GetInt32() == 203);
    }

    [Fact] // [D-34]
    public async Task Api_Facturacion_404_YSinPermisos()
    {
        var rec = await Recepcion();
        Assert.Equal(HttpStatusCode.NotFound, (await rec.GetAsync("/api/facturacion/999999/totales")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await rec.GetAsync("/api/facturas/999999/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await rec.PostAsJsonAsync("/api/reservas/999999/consumos", new { concepto = "Minibar", cantidad = 1, monto = 1000m })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.CreateClient().GetAsync("/api/facturacion/pendientes")).StatusCode);
        var ama = await Cliente("amadellaves@oasisstay.co", FabricaApi.ClaveAma);
        Assert.Equal(HttpStatusCode.Forbidden, (await ama.GetAsync("/api/facturacion/pendientes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ama.GetAsync("/api/facturas/1/pdf")).StatusCode);
        var admin = await Cliente("admin@oasisstay.co", FabricaApi.ClaveAdmin);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/facturacion/pendientes")).StatusCode);
    }
}
