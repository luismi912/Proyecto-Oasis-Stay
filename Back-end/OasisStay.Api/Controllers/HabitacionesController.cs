using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OasisStay.Api.Contratos;
using OasisStay.Aplicacion;
using OasisStay.Dominio;

namespace OasisStay.Api.Controllers;

/// <summary>Habitaciones: disponibilidad, estados y alta [DS1][DS2][D-16][D-34]. Sin reglas: traduce HTTP y servicio.</summary>
[ApiController]
[Route("api/habitaciones")]
public class HabitacionesController : ControllerBase
{
    private const string TodosLosRoles = "Administrador,Recepcionista,AmaDeLlaves";
    private readonly IConsultaDisponibilidad _consulta;
    private readonly IGestionEstadoHabitacion _gestion;

    public HabitacionesController(IConsultaDisponibilidad consulta, IGestionEstadoHabitacion gestion)
    {
        _consulta = consulta;
        _gestion = gestion;
    }

    /// <summary>[DS1][JIRA US-01][D-19][D-35]</summary>
    [HttpGet("disponibilidad")]
    [Authorize(Roles = "Administrador,Recepcionista")]
    public ActionResult<DisponibilidadRespuesta> Disponibilidad(
        [FromQuery] DateTime fechaInicio, [FromQuery] DateTime fechaFin, [FromQuery] string? tipo, [FromQuery] int huespedes)
    {
        try
        {
            var r = _consulta.ConsultarConAlternativas(fechaInicio, fechaFin, tipo, huespedes);
            return new DisponibilidadRespuesta
            {
                Habitaciones = r.Habitaciones.Select(h => new HabitacionLibreRespuesta
                {
                    Numero = h.NumeroHabitacion, Tipo = h.Tipo!.Nombre, Capacidad = h.Tipo.CapacidadMax, PrecioNoche = h.Tipo.PrecioBase,
                }).ToList(),
                Mensaje = r.Mensaje,
                Alternativas = r.Alternativas.Select(ATipo).ToList(),
            };
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
        }
    }

    /// <summary>[DS2 obtenerHabitaciones][actividad-2]</summary>
    [HttpGet]
    [Authorize(Roles = TodosLosRoles)]
    public ActionResult<List<HabitacionRespuesta>> Listar([FromQuery] string? estado)
    {
        EstadoHabitacion? filtro = null;
        if (!string.IsNullOrWhiteSpace(estado))
        {
            filtro = AEstado(estado);
            if (filtro is null) return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "El estado indicado no es válido.");
        }
        return _gestion.ObtenerHabitaciones(filtro).Select(AHabitacion).ToList();
    }

    /// <summary>Mapa por pisos [DS5][JIRA US-09][D-34]: solo Recepcionista y Administrador.</summary>
    [HttpGet("distribucion")]
    [Authorize(Roles = "Administrador,Recepcionista")]
    public ActionResult<List<DistribucionHabitacionRespuesta>> Distribucion()
        => _gestion.ObtenerDistribucionHabitaciones().Select(m => new DistribucionHabitacionRespuesta
        {
            Numero = m.Numero, Piso = m.Piso, Tipo = m.Tipo, Estado = m.Estado, PrecioNoche = m.PrecioNoche,
            MotivoMantenimiento = m.MotivoMantenimiento,
            Reserva = m.Reserva is null ? null : new ReservaActivaRespuesta
            { IdReserva = m.Reserva.IdReserva, Huesped = m.Reserva.Huesped, FechaLlegada = m.Reserva.FechaLlegada, FechaSalida = m.Reserva.FechaSalida },
        }).ToList();

    /// <summary>[DS2][JIRA US-02][DE#1 §5][D-34]</summary>
    [HttpPut("{numero:int}/estado")]
    [Authorize(Roles = TodosLosRoles)]
    public ActionResult<HabitacionRespuesta> CambiarEstado(int numero, CambiarEstadoSolicitud solicitud)
    {
        var nuevo = AEstado(solicitud.NuevoEstado);
        if (nuevo is null) return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "El estado indicado no es válido.");

        var r = _gestion.CambiarEstado(numero, nuevo.Value, solicitud.Motivo, User.FindFirstValue("role"));
        return r.Desenlace switch
        {
            DesenlaceCambioEstado.Exitoso => AHabitacion(r.Habitacion!),
            DesenlaceCambioEstado.NoExiste => Problem(statusCode: StatusCodes.Status404NotFound, detail: r.Mensaje),
            DesenlaceCambioEstado.DatosInvalidos => Problem(statusCode: StatusCodes.Status400BadRequest, detail: r.Mensaje),
            DesenlaceCambioEstado.SinPermiso => Problem(statusCode: StatusCodes.Status403Forbidden, detail: r.Mensaje),
            _ => Problem(statusCode: StatusCodes.Status409Conflict, detail: r.Mensaje),
        };
    }

    /// <summary>[D-16][actividad-1][D-28]</summary>
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public ActionResult<HabitacionRespuesta> Crear(CrearHabitacionSolicitud solicitud)
    {
        var r = _gestion.CrearHabitacion(solicitud.Numero ?? 0, solicitud.Tipo ?? 0, solicitud.Piso ?? 0);
        return r.Desenlace switch
        {
            DesenlaceAlta.Creada => Created($"/api/habitaciones/{r.Habitacion!.NumeroHabitacion}", AHabitacion(r.Habitacion)),
            DesenlaceAlta.NumeroRepetido => Problem(statusCode: StatusCodes.Status409Conflict, detail: r.Mensaje),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, detail: r.Mensaje),
        };
    }

    private static HabitacionRespuesta AHabitacion(Habitacion h) => new()
    {
        Numero = h.NumeroHabitacion,
        Tipo = h.Tipo?.Nombre ?? "",
        Piso = h.Piso,
        PrecioNoche = h.Tipo?.PrecioBase ?? 0,
        Estado = h.estadoActual?.Nombre ?? Habitacion.EstadoDesdeTexto(h.Estado.ToString()).Nombre,
        MotivoMantenimiento = h.MotivoMantenimiento,
    };

    private static TipoAlternativoRespuesta ATipo(TipoHabitacion t)
        => new() { Id = t.IdTipo, Nombre = t.Nombre, CapacidadMax = t.CapacidadMax, PrecioBase = t.PrecioBase };

    /// <summary>Acepta los nombres del contrato (Disponible, EnLimpieza...) o el enum persistido [CTR §3].</summary>
    internal static EstadoHabitacion? AEstado(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim().Replace("_", "").Replace(" ", "");
        return t.ToUpperInvariant() switch
        {
            "DISPONIBLE" => EstadoHabitacion.DISPONIBLE,
            "RESERVADA" or "RESERVADO" => EstadoHabitacion.RESERVADA,
            "OCUPADA" or "OCUPADO" => EstadoHabitacion.OCUPADO,
            "ENLIMPIEZA" => EstadoHabitacion.EN_LIMPIEZA,
            "MANTENIMIENTO" => EstadoHabitacion.MANTENIMIENTO,
            _ => null,
        };
    }
}

/// <summary>[CTR §2 TipoHabitacion]</summary>
[ApiController]
[Route("api/tipos-habitacion")]
[Authorize]
public class TiposHabitacionController : ControllerBase
{
    private readonly IGestionEstadoHabitacion _gestion;
    public TiposHabitacionController(IGestionEstadoHabitacion gestion) => _gestion = gestion;

    [HttpGet]
    public ActionResult<List<TipoAlternativoRespuesta>> Listar()
        => _gestion.ObtenerTipos().Select(t => new TipoAlternativoRespuesta
        { Id = t.IdTipo, Nombre = t.Nombre, CapacidadMax = t.CapacidadMax, PrecioBase = t.PrecioBase }).ToList();
}
