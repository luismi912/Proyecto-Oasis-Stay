using System.Net.Http.Json;
using System.Text.Json;

namespace Front_end.Servicios.Api;

public record MetodoPagoDto(string Metodo, decimal Monto, int Pagos);

public record ResumenReporteDto(decimal IngresoTotal, decimal IngresoBruto, decimal IngresoNeto, decimal TicketPromedio,
    decimal OcupacionPorcentaje, decimal Cancelaciones, List<MetodoPagoDto> PorMetodoPago, int Facturas);

public record ArchivoDescargado(byte[] Contenido, string TipoContenido, string NombreArchivo);

/// <summary>Cliente tipado de reportes financieros [JIRA US-11][DS3][D-46][razor-integracion]; sin reglas de negocio.</summary>
public class ReportesApi
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    public ReportesApi(HttpClient http) => _http = http;

    private static string Consulta(DateTime? desde, DateTime? hasta, string? formato = null)
    {
        var pares = new List<string>();
        if (desde.HasValue) pares.Add("desde=" + desde.Value.ToString("yyyy-MM-dd"));
        if (hasta.HasValue) pares.Add("hasta=" + hasta.Value.ToString("yyyy-MM-dd"));
        if (formato != null) pares.Add("formato=" + Uri.EscapeDataString(formato));
        return string.Join("&", pares);
    }

    /// <summary>KPIs previos a la descarga [JIRA US-11 UX]; 400 si el rango es inválido o excede 365 días.</summary>
    public async Task<ResumenReporteDto> ResumenAsync(DateTime? desde, DateTime? hasta)
    {
        var r = await _http.GetAsync("api/reportes/resumen?" + Consulta(desde, hasta));
        if (!r.IsSuccessStatusCode) throw await ErrorAsync(r);
        return (await r.Content.ReadFromJsonAsync<ResumenReporteDto>(Web))!;
    }

    /// <summary>Descarga el .xlsx o .pdf [DS3 descargarReporte]; los errores llegan con el mensaje acordado.</summary>
    public async Task<ArchivoDescargado> ExportarAsync(DateTime? desde, DateTime? hasta, string formato)
    {
        var r = await _http.GetAsync("api/reportes/exportar?" + Consulta(desde, hasta, formato));
        if (!r.IsSuccessStatusCode) throw await ErrorAsync(r);
        var nombre = r.Content.Headers.ContentDisposition?.FileNameStar ?? r.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"reporte.{formato}";
        return new ArchivoDescargado(await r.Content.ReadAsByteArrayAsync(), r.Content.Headers.ContentType?.MediaType ?? "application/octet-stream", nombre);
    }

    private static async Task<ApiException> ErrorAsync(HttpResponseMessage r)
    {
        string? detalle = null;
        try
        {
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("detail", out var d)) detalle = d.GetString();
        }
        catch (JsonException) { }
        return new ApiException(r.StatusCode, detalle ?? "No se pudo generar el reporte.");
    }
}
