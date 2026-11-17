using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Front_end.Servicios.Api;

public record LoginResultado(string Token, string Rol, string Nombre, DateTime ExpiraEn);

/// <summary>Error devuelto por la API como ProblemDetails; Detail trae el mensaje acordado.</summary>
public class ApiException : Exception
{
    public HttpStatusCode Estado { get; }
    public ApiException(HttpStatusCode estado, string mensaje) : base(mensaje) => Estado = estado;
}

/// <summary>Cliente HTTP tipado de la API [razor-integracion]. El front no contiene reglas de negocio.</summary>
public class OasisApiClient
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    public OasisApiClient(HttpClient http) => _http = http;

    public async Task<LoginResultado> LoginAsync(string usuario, string contrasena)
    {
        var r = await _http.PostAsJsonAsync("api/auth/login", new { usuario, contrasena });
        if (!r.IsSuccessStatusCode) throw await ErrorAsync(r);
        return (await r.Content.ReadFromJsonAsync<LoginResultado>(Web))!;
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

/// <summary>Añade el JWT guardado en la cookie de autenticación a cada llamada a la API.</summary>
public class JwtDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accesor;
    public JwtDelegatingHandler(IHttpContextAccessor accesor) => _accesor = accesor;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = _accesor.HttpContext?.User.FindFirst("jwt")?.Value;
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, ct);
    }
}
