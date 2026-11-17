using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OasisStay.Pruebas.F0;

/// <summary>Endpoint solo de pruebas para comprobar [Authorize(Roles)] (403).</summary>
[ApiController]
[Route("api/prueba")]
public class ControladorDePruebaController : ControllerBase
{
    [HttpGet("solo-administrador")]
    [Authorize(Roles = "Administrador")]
    public IActionResult SoloAdministrador() => Ok();
}
