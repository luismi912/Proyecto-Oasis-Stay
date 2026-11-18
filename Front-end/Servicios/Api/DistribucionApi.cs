using System.Net.Http.Json;
using System.Text.Json;

namespace Front_end.Servicios.Api;

public record ReservaActivaDto(int IdReserva, string Huesped, DateTime FechaLlegada, DateTime FechaSalida);
public record DistribucionDto(int Numero, int Piso, string Tipo, string Estado, decimal PrecioNoche, string? MotivoMantenimiento, ReservaActivaDto? Reserva);

/// <summary>Cliente tipado del mapa de habitaciones [JIRA US-09][DS5]. Sin reglas: muestra lo que responde la API.</summary>
public class DistribucionApi
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    public DistribucionApi(HttpClient http) => _http = http;

    public async Task<List<DistribucionDto>> ObtenerAsync()
    {
        var r = await _http.GetAsync("api/habitaciones/distribucion");
        if (!r.IsSuccessStatusCode)
        {
            string? detalle = null;
            try
            {
                using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
                if (doc.RootElement.TryGetProperty("detail", out var d)) detalle = d.GetString();
            }
            catch (JsonException) { }
            throw new ApiException(r.StatusCode, detalle ?? "No se pudo cargar el mapa de habitaciones.");
        }
        return (await r.Content.ReadFromJsonAsync<List<DistribucionDto>>(Web))!;
    }
}
