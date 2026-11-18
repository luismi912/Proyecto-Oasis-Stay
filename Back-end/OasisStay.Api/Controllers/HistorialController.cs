using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OasisStay.Api.Contratos;
using OasisStay.Api.Filtros;
using OasisStay.Aplicacion;

namespace OasisStay.Api.Controllers;

/// <summary>
/// Consulta de la bitácora de auditoría [DS3][JIRA US-07][D-34]: solo Administrador. Solo existe GET: la bitácora es de solo
/// inserción y no se edita ni se borra [JIRA US-07 RN2].
/// </summary>
[ApiController]
[Route("api/historial")]
[Authorize(Roles = "Administrador")]
[ErroresDominioReservasFiltro]
public class HistorialController : ControllerBase
{
    private readonly IAuditoriaHistorial _auditoria;
    public HistorialController(IAuditoriaHistorial auditoria) => _auditoria = auditoria;

    /// <summary>Lista cronológica de eventos; vacía si no hay resultados [actividad-3]; 400 si desde &gt; hasta.</summary>
    [HttpGet]
    public ActionResult<IEnumerable<EventoHistorialRespuesta>> Consultar(
        [FromQuery] int? idReserva, [FromQuery] string? usuario, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] string? texto)
    {
        var eventos = _auditoria.ObtenerRegistros(new FiltrosHistorial { IdReserva = idReserva, Usuario = usuario, Desde = desde, Hasta = hasta, Texto = texto });
        return eventos.Select(e => new EventoHistorialRespuesta
        {
            FechaHora = e.FechaRegistro, IdReserva = e.IdReserva, Usuario = e.Usuario, Accion = e.AccionRealizada,
            ValorAnterior = e.ValorAnterior, ValorNuevo = e.ValorNuevo,
        }).ToList();
    }
}
