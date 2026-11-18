using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OasisStay.Api.Filtros;
using OasisStay.Aplicacion;
using OasisStay.Dominio;

namespace OasisStay.Api.Controllers;

/// <summary>Reportes financieros solo para el Administrador: 403 a los demás roles [JIRA US-11 RN1][D-34][D-46][DS3].</summary>
[ApiController]
[Authorize(Roles = "Administrador")]
[ErroresReportesFiltro]
[Route("api/reportes")]
public class ReportesController : ControllerBase
{
    private readonly ServicioReportes _reportes;
    public ReportesController(ServicioReportes reportes) => _reportes = reportes;

    /// <summary>KPIs y consolidado previos a la descarga; sin movimientos responde 200 con <c>facturas = 0</c> [JIRA US-11 UX/ESC3].</summary>
    [HttpGet("resumen")]
    public ActionResult<ResumenFinanciero> Resumen([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        if (desde is null || hasta is null) throw new ValidacionDominioException(MensajesReportes.FaltanFechas);
        return _reportes.ResumenFinanciero(desde.Value, hasta.Value);
    }

    /// <summary>Descarga el .xlsx o .pdf; 400 formato o rango inválido, 409 sin movimientos, 500 errorGeneracion(motivo) [DS3][JIRA US-11 ESC3].</summary>
    [HttpGet("exportar")]
    public IActionResult Exportar([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] string? formato)
    {
        if (desde is null || hasta is null) throw new ValidacionDominioException(MensajesReportes.FaltanFechas);
        var usuario = User.FindFirst("name")?.Value ?? User.Identity?.Name ?? "";
        var rol = User.IsInRole("Administrador") ? "Administrador" : User.FindFirst("role")?.Value ?? "";
        var archivo = _reportes.GenerarReporte(desde.Value, hasta.Value, formato ?? "", usuario, rol);
        return File(archivo.Contenido, archivo.TipoContenido, archivo.NombreArchivo);
    }
}
