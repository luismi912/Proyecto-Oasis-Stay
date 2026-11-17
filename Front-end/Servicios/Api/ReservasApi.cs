using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Front_end.Servicios.Api;

public record HuespedDto(string TipoDocumento, string Documento, string Nombre, string Telefono, string Correo);
public record ReservaListadoDto(int IdReserva, string Cliente, int Habitacion, DateTime FechaInicio, DateTime FechaFin, int Huespedes, string Estado);
public record ReservaCreadaDto(int IdReserva, string Estado);

/// <summary>Error de la API con el mensaje acordado y, si aplica, los mensajes por campo.</summary>
public class ApiReservasException : ApiException
{
    public IReadOnlyDictionary<string, string[]> Campos { get; }
    public ApiReservasException(HttpStatusCode estado, string mensaje, IReadOnlyDictionary<string, string[]> campos) : base(estado, mensaje)
        => Campos = campos;
}

/// <summary>Cliente tipado de huéspedes y reservas [razor-integracion]; sin reglas de negocio.</summary>
public class ReservasApi
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    public ReservasApi(HttpClient http) => _http = http;

    public async Task<HuespedDto?> BuscarHuespedAsync(string tipoDocumento, string documento)
    {
        var r = await _http.GetAsync($"api/huespedes/buscar?tipoDocumento={Uri.EscapeDataString(tipoDocumento)}&documento={Uri.EscapeDataString(documento)}");
        if (r.StatusCode == HttpStatusCode.NotFound) return null;
        if (!r.IsSuccessStatusCode) throw await ErrorAsync(r);
        return await r.Content.ReadFromJsonAsync<HuespedDto>(Web);
    }

    public async Task<IReadOnlyList<ReservaListadoDto>> ListarAsync(string? texto = null)
    {
        var r = await _http.GetAsync("api/reservas" + (string.IsNullOrWhiteSpace(texto) ? "" : "?texto=" + Uri.EscapeDataString(texto)));
        if (!r.IsSuccessStatusCode) throw await ErrorAsync(r);
        return (await r.Content.ReadFromJsonAsync<List<ReservaListadoDto>>(Web))!;
    }

    public async Task<ReservaCreadaDto> CrearAsync(object solicitud)
    {
        var r = await _http.PostAsJsonAsync("api/reservas", solicitud);
        if (!r.IsSuccessStatusCode) throw await ErrorAsync(r);
        return (await r.Content.ReadFromJsonAsync<ReservaCreadaDto>(Web))!;
    }

    /// <summary>Disponibilidad real de F1 (US-01): devuelve el estado HTTP y el JSON tal cual para que el navegador lo pinte.</summary>
    public async Task<(int Estado, string Cuerpo)> DisponibilidadAsync(DateTime fechaInicio, DateTime fechaFin, int huespedes)
    {
        var url = $"api/habitaciones/disponibilidad?fechaInicio={fechaInicio:yyyy-MM-dd}&fechaFin={fechaFin:yyyy-MM-dd}&huespedes={huespedes}";
        var r = await _http.GetAsync(url);
        return ((int)r.StatusCode, await r.Content.ReadAsStringAsync());
    }

    public async Task CheckInAsync(int idReserva, string? documento)
    {
        var r = await _http.PostAsJsonAsync($"api/reservas/{idReserva}/check-in", new { documento });
        if (!r.IsSuccessStatusCode) throw await ErrorAsync(r);
    }

    private static async Task<ApiReservasException> ErrorAsync(HttpResponseMessage r)
    {
        string? detalle = null;
        var campos = new Dictionary<string, string[]>();
        try
        {
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("detail", out var d)) detalle = d.GetString();
            if (doc.RootElement.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Object)
                foreach (var p in e.EnumerateObject())
                    campos[p.Name] = p.Value.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
        }
        catch (JsonException) { }
        return new ApiReservasException(r.StatusCode, detalle ?? "No se pudo completar la operación.", campos);
    }
}
