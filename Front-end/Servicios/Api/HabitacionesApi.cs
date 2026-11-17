using System.Net.Http.Json;
using System.Text.Json;

namespace Front_end.Servicios.Api;

public record HabitacionDto(int Numero, string Tipo, int Piso, decimal PrecioNoche, string Estado, string? MotivoMantenimiento);
public record TipoHabitacionDto(int Id, string Nombre, int CapacidadMax, decimal PrecioBase);

/// <summary>Cliente tipado del módulo Habitaciones [razor-integracion]. Sin reglas de negocio: muestra lo que responde la API.</summary>
public class HabitacionesApi
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    public HabitacionesApi(HttpClient http) => _http = http;

    public async Task<List<HabitacionDto>> ListarAsync(string? estado)
    {
        var url = string.IsNullOrWhiteSpace(estado) ? "api/habitaciones" : $"api/habitaciones?estado={Uri.EscapeDataString(estado)}";
        var r = await _http.GetAsync(url);
        if (!r.IsSuccessStatusCode) throw await ErrorAsync(r);
        return (await r.Content.ReadFromJsonAsync<List<HabitacionDto>>(Web))!;
    }

    public async Task<List<TipoHabitacionDto>> TiposAsync()
    {
        var r = await _http.GetAsync("api/tipos-habitacion");
        if (!r.IsSuccessStatusCode) throw await ErrorAsync(r);
        return (await r.Content.ReadFromJsonAsync<List<TipoHabitacionDto>>(Web))!;
    }

    public async Task CambiarEstadoAsync(int numero, string nuevoEstado, string? motivo)
    {
        var r = await _http.PutAsJsonAsync($"api/habitaciones/{numero}/estado", new { nuevoEstado, motivo });
        if (!r.IsSuccessStatusCode) throw await ErrorAsync(r);
    }

    public async Task CrearAsync(int? numero, int? tipo, int? piso)
    {
        var r = await _http.PostAsJsonAsync("api/habitaciones", new { numero, tipo, piso });
        if (!r.IsSuccessStatusCode) throw await ErrorAsync(r);
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
        return new ApiException(r.StatusCode, detalle ?? "No se pudo completar la operación.");
    }
}
