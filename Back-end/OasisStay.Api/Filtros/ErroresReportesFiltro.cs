using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OasisStay.Dominio;

namespace OasisStay.Api.Filtros;

/// <summary>
/// Errores de reportes como ProblemDetails con el mensaje acordado: 403 rol, 500 <c>errorGeneracion(motivo)</c>; el resto (400, 409)
/// los traduce el filtro de dominio [DS3 alt][JIRA US-11 RN1][dotnet-api].
/// </summary>
public class ErroresReportesFiltro : ErroresDominioReservasFiltro
{
    public override void OnException(ExceptionContext contexto)
    {
        switch (contexto.Exception)
        {
            case AccesoProhibidoException a:
                contexto.Result = Problema(StatusCodes.Status403Forbidden, a.Message);
                break;
            case ErrorGeneracionReporteException e:
                contexto.Result = Problema(StatusCodes.Status500InternalServerError, e.Message);
                break;
            default:
                base.OnException(contexto);
                return;
        }
        contexto.ExceptionHandled = true;
    }

    private static ObjectResult Problema(int estado, string detalle) =>
        new(new ProblemDetails { Status = estado, Detail = detalle }) { StatusCode = estado };
}
