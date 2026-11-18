using OasisStay.Aplicacion;

namespace OasisStay.Api;

/// <summary>Nombre del token del HttpContext; "sistema" si no hay usuario autenticado [D-47].</summary>
public class UsuarioActualHttp : IUsuarioActual
{
    private readonly IHttpContextAccessor _http;
    public UsuarioActualHttp(IHttpContextAccessor http) => _http = http;
    public string Nombre => _http.HttpContext?.User.FindFirst("name")?.Value is { Length: > 0 } n ? n : "sistema";
}
