using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OasisStay.Dominio;

namespace OasisStay.Api.Filtros;

/// <summary>
/// Traduce las excepciones de dominio a ProblemDetails con el mensaje acordado en `detail`, sin trazas:
/// 400 validación, 404 no existe, 409 conflicto [dotnet-api][CTR §4][D-32].
/// </summary>
public class ErroresDominioReservasFiltro : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext contexto)
    {
        switch (contexto.Exception)
        {
            case ValidacionDominioException v when v.Errores.Count > 0:
                contexto.Result = new ObjectResult(new ValidationProblemDetails(v.Errores.ToDictionary(e => e.Key, e => e.Value))
                {
                    Status = StatusCodes.Status400BadRequest,
                    Detail = v.Message,
                }) { StatusCode = StatusCodes.Status400BadRequest };
                break;
            case ValidacionDominioException v:
                contexto.Result = Problema(StatusCodes.Status400BadRequest, v.Message);
                break;
            case NoEncontradoException n:
                contexto.Result = Problema(StatusCodes.Status404NotFound, n.Message);
                break;
            case ConflictoDominioException c:
                contexto.Result = Problema(StatusCodes.Status409Conflict, c.Message);
                break;
            default:
                return;
        }
        contexto.ExceptionHandled = true;
    }

    private static ObjectResult Problema(int estado, string detalle) =>
        new(new ProblemDetails { Status = estado, Detail = detalle }) { StatusCode = estado };
}
