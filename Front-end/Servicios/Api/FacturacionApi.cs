using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Front_end.Servicios.Api;

public record PendienteDto(int IdReserva, string Huesped, int NumeroHabitacion, DateTime FechaFin, string EstadoPago, decimal Total, decimal Saldo);
public record LineaConsumoDto(string Concepto, int Cantidad, decimal ValorUnitario, decimal SubTotal);
public record LineaPagoDto(int IdPago, DateTime Fecha, string Metodo, decimal Monto, decimal? Vuelto);
public record ResumenCobroDto(int IdReserva, string Huesped, int NumeroHabitacion, string TipoHabitacion, DateTime FechaInicio, DateTime FechaFin,
    string EstadoReserva, int Noches, decimal Tarifa, List<LineaConsumoDto> Consumos, bool SalidaTardia, bool SalidaTardiaSugerida,
    decimal Subtotal, decimal Iva, decimal Total, decimal Pagado, decimal Saldo, List<LineaPagoDto> Pagos, int? IdFactura);
public record ReciboDto(int IdPago, DateTime Fecha, decimal Monto, string Metodo, decimal? Vuelto, decimal SaldoRestante);
public record FacturaDto(int IdFactura, int Folio, DateTime FechaEmision, decimal ImpuestoIVA, decimal ValorTotal, int IdReserva, int IdHabitacion);
public record CheckoutDto(FacturaDto Factura, string PdfUrl);
public record CotizacionDto(int Noches, decimal Tarifa, decimal Subtotal, decimal Iva, decimal Total);

/// <summary>Cliente tipado de facturación, pagos y check-out [razor-integracion][D-10]; sin reglas de negocio.</summary>
public class FacturacionApi
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    public FacturacionApi(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<PendienteDto>> PendientesAsync()
        => await LeerAsync<List<PendienteDto>>(await _http.GetAsync("api/facturacion/pendientes"));

    public async Task<ResumenCobroDto> TotalesAsync(int idReserva)
        => await LeerAsync<ResumenCobroDto>(await _http.GetAsync($"api/facturacion/{idReserva}/totales"));

    public async Task AgregarConsumoAsync(int idReserva, string concepto, int cantidad, decimal monto)
        => await VerificarAsync(await _http.PostAsJsonAsync($"api/reservas/{idReserva}/consumos", new { concepto, cantidad, monto }));

    public async Task<ReciboDto> PagarAsync(int idReserva, string metodoPago, decimal monto, object datosPago)
        => await LeerAsync<ReciboDto>(await _http.PostAsJsonAsync($"api/reservas/{idReserva}/pagos", new { metodoPago, monto, datosPago }));

    public async Task<CheckoutDto> CheckoutAsync(int idReserva, string? metodoPago, object datosPago)
        => await LeerAsync<CheckoutDto>(await _http.PostAsJsonAsync($"api/reservas/{idReserva}/checkout", new { metodoPago, datosPago }));

    public async Task<CotizacionDto> CotizarAsync(object solicitud)
        => await LeerAsync<CotizacionDto>(await _http.PostAsJsonAsync("api/cotizaciones", solicitud));

    public async Task<byte[]> PdfAsync(int idFactura)
    {
        var r = await _http.GetAsync($"api/facturas/{idFactura}/pdf");
        await VerificarAsync(r);
        return await r.Content.ReadAsByteArrayAsync();
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage r)
    {
        await VerificarAsync(r);
        return (await r.Content.ReadFromJsonAsync<T>(Web))!;
    }

    private static async Task VerificarAsync(HttpResponseMessage r)
    {
        if (r.IsSuccessStatusCode) return;
        string? detalle = null;
        try
        {
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("detail", out var d)) detalle = d.GetString();
        }
        catch (JsonException) { }
        throw new ApiException(r.StatusCode, detalle ?? (r.StatusCode == HttpStatusCode.Forbidden ? "No tiene permiso para esta operación." : "No se pudo completar la operación."));
    }
}
