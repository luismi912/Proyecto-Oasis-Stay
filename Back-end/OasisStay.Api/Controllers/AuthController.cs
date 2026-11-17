using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OasisStay.Api.Contratos;
using OasisStay.Infraestructura.Identidad;

namespace OasisStay.Api.Controllers;

/// <summary>Login con Identity y JWT [D-02][E1 RNF-06].</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<UsuarioOasis> _usuarios;
    private readonly IConfiguration _config;

    public AuthController(UserManager<UsuarioOasis> usuarios, IConfiguration config)
    {
        _usuarios = usuarios;
        _config = config;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginRespuesta>> Login(LoginSolicitud solicitud)
    {
        var usuario = await _usuarios.FindByEmailAsync(solicitud.Usuario) ?? await _usuarios.FindByNameAsync(solicitud.Usuario);
        if (usuario == null || !await _usuarios.CheckPasswordAsync(usuario, solicitud.Contrasena))
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Las credenciales no son válidas.");

        var rol = (await _usuarios.GetRolesAsync(usuario)).FirstOrDefault() ?? "";
        var expira = DateTime.UtcNow.AddMinutes(_config.GetValue("Jwt:ExpiraMinutos", 60));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _config["Jwt:Emisor"],
            Audience = _config["Jwt:Audiencia"],
            Expires = expira,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", usuario.Id),
                new Claim("name", usuario.Nombre),
                new Claim("role", rol),
            }),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Clave"]!)), SecurityAlgorithms.HmacSha256),
        };
        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return Ok(new LoginRespuesta { Token = token, Rol = rol, Nombre = usuario.Nombre, ExpiraEn = expira });
    }

    [HttpGet("yo")]
    [Authorize]
    public ActionResult<YoRespuesta> Yo() => new YoRespuesta
    {
        Nombre = User.FindFirstValue("name") ?? "",
        Rol = User.FindFirstValue("role") ?? "",
    };
}
