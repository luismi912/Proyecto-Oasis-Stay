using System.Net.Http.Json;
using System.Text.Json;

namespace Front_end.Servicios.Api;

public record EventoHistorialDto(DateTime FechaHora, int IdReserva, string Usuario, string Accion, string? ValorAnterior, string? ValorNuevo);

/// <summary>Cliente tipado de la bitácora de auditoría (solo lectura) [JIRA US-07][razor-integracion]; sin reglas de negocio.</summary>
public class HistorialApi
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    public HistorialApi(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<EventoHistorialDto>> ConsultarAsync(int? idReserva = null, string? usuario = null,
        DateTime? desde = null, DateTime? hasta = null, string? texto = null)
    {
        var pares = new List<string>();
        if (idReserva.HasValue) pares.Add("idReserva=" + idReserva.Value);
        if (!string.IsNullOrWhiteSpace(usuario)) pares.Add("usuario=" + Uri.EscapeDataString(usuario.Trim()));
        if (desde.HasValue) pares.Add("desde=" + desde.Value.ToString("yyyy-MM-dd"));
        if (hasta.HasValue) pares.Add("hasta=" + hasta.Value.ToString("yyyy-MM-dd"));
        if (!string.IsNullOrWhiteSpace(texto)) pares.Add("texto=" + Uri.EscapeDataString(texto.Trim()));
        var r = await _http.GetAsync("api/historial" + (pares.Count > 0 ? "?" + string.Join("&", pares) : ""));
        if (!r.IsSuccessStatusCode) throw await ErrorAsync(r);
        return (await r.Content.ReadFromJsonAsync<List<EventoHistorialDto>>(Web))!;
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
        return new ApiException(r.StatusCode, detalle ?? "No se pudo consultar el historial.");
    }
}
