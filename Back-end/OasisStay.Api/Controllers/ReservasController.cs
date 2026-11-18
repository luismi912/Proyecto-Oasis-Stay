using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OasisStay.Api.Contratos;
using OasisStay.Api.Filtros;
using OasisStay.Aplicacion;
using OasisStay.Dominio;

namespace OasisStay.Api.Controllers;

/// <summary>Huéspedes [JIRA US-03][D-34]: Recepcionista y Administrador.</summary>
[ApiController]
[Route("api/huespedes")]
[Authorize(Roles = "Administrador,Recepcionista")]
[ErroresDominioReservasFiltro]
public class HuespedesController : ControllerBase
{
    private readonly IRegistroReservas _servicio;
    public HuespedesController(IRegistroReservas servicio) => _servicio = servicio;

    internal static bool TryTipo(string? texto, out TipoDocumento tipo)
        => Enum.TryParse(texto, true, out tipo) && Enum.IsDefined(tipo);

    internal static HuespedRespuesta Mapear(Cliente c) => new()
    {
        TipoDocumento = c.TipoDocumento.ToString(), Documento = c.Documento, Nombre = c.Nombre, Telefono = c.Telefono, Correo = c.Correo,
    };

    /// <summary>Autocompletado por documento [JIRA US-03 ESC2].</summary>
    [HttpGet("buscar")]
    public ActionResult<HuespedRespuesta> Buscar([FromQuery] string tipoDocumento, [FromQuery] string documento)
    {
        if (!TryTipo(tipoDocumento, out var tipo) || string.IsNullOrWhiteSpace(documento))
            throw new ValidacionDominioException("Indique el tipo y el número de documento.");
        var c = _servicio.BuscarHuesped(tipo, documento) ?? throw new NoEncontradoException("No existe un huésped con ese documento.");
        return Mapear(c);
    }

    /// <summary>Alta o actualización de los datos del huésped [JIRA US-03 ESC1/ESC3][JIRA US-03 RN3].</summary>
    [HttpPost]
    public ActionResult<HuespedRespuesta> Guardar(HuespedSolicitud s)
    {
        var datos = new Cliente
        {
            TipoDocumento = TryTipo(s.TipoDocumento, out var t) ? t : (TipoDocumento)(-1),
            Documento = s.Documento ?? "", Nombre = s.Nombre ?? "", Telefono = s.Telefono ?? "", Correo = s.Correo ?? "",
        };
        var (cliente, creado) = _servicio.GuardarHuesped(datos);
        var cuerpo = Mapear(cliente);
        return creado ? Created($"/api/huespedes/buscar?tipoDocumento={cuerpo.TipoDocumento}&documento={cuerpo.Documento}", cuerpo) : Ok(cuerpo);
    }
}

/// <summary>Reservas, reasignación y check-in [DS1][D-09][D-21][D-31][D-34].</summary>
[ApiController]
[Route("api/reservas")]
[Authorize(Roles = "Administrador,Recepcionista")]
[ErroresDominioReservasFiltro]
public class ReservasController : ControllerBase
{
    private readonly IRegistroReservas _servicio;
    private readonly IGestionReservas _gestion;
    private readonly ICancelacionReservas _cancelacion;
    private readonly IReloj _reloj;
    public ReservasController(IRegistroReservas servicio, IGestionReservas gestion, ICancelacionReservas cancelacion, IReloj reloj)
    { _servicio = servicio; _gestion = gestion; _cancelacion = cancelacion; _reloj = reloj; }

    /// <summary>Vista previa del cargo antes de cancelar, sin efectos [JIRA US-08 UX][D-44].</summary>
    [HttpGet("{id:int}/penalidad")]
    public ActionResult<PenalidadRespuesta> Penalidad(int id)
    {
        var p = _cancelacion.CalcularPenalidad(id);
        return new PenalidadRespuesta { Gratis = p.Gratis, Penalidad = p.Penalidad, Limite = p.Limite };
    }

    /// <summary>Cancela con penalidad según el plazo de 24 h; exonerar solo el Administrador con motivo [DE#4][JIRA US-08 RN1-RN4][D-24][D-44].</summary>
    [HttpPost("{id:int}/cancelar")]
    public ActionResult<CancelacionRespuesta> Cancelar(int id, CancelacionSolicitud s)
    {
        if (s.Exonerar && !User.IsInRole("Administrador"))
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Status = StatusCodes.Status403Forbidden, Detail = MensajesReserva.SoloAdminExonera });
        var r = _cancelacion.CancelarReserva(id, s.Exonerar, s.Motivo, Usuario);
        return new CancelacionRespuesta { Estado = r.Estado.ToString(), Penalidad = r.Penalidad, Exonerada = r.PenalidadExonerada };
    }

    /// <summary>Acción manual del No-Show: mismo corte que la rutina automática (por defecto ayer, UTC-5; el día de llegada de hoy aún no terminó) [DE#4 CP-R05][D-44].</summary>
    [HttpPost("no-show/ejecutar")]
    [Authorize(Roles = "Administrador")]
    public ActionResult<NoShowRespuesta> EjecutarNoShow(NoShowSolicitud? s)
        => new NoShowRespuesta { Procesadas = _cancelacion.EjecutarNoShow(s?.FechaCorte ?? _reloj.Ahora.Date.AddDays(-1)) };

    private string Usuario => User.FindFirstValue("name") ?? "sistema";

    [HttpPost]
    public ActionResult<ReservaCreadaRespuesta> Crear(ReservaSolicitud s)
    {
        var solicitud = new SolicitudReserva
        {
            CheckIn = s.FechaInicio, CheckOut = s.FechaFin, Huespedes = s.Huespedes, Usuario = Usuario,
            Habitacion = new Habitacion { NumeroHabitacion = s.NumeroHabitacion },
            Cliente = new Cliente
            {
                TipoDocumento = HuespedesController.TryTipo(s.TipoDocumento, out var t) ? t : (TipoDocumento)(-1),
                Documento = s.Documento ?? "", Nombre = s.Nombre ?? "", Telefono = s.Telefono ?? "", Correo = s.Correo ?? "",
            },
        };
        var r = _gestion.RegistrarReserva(solicitud);
        return Created($"/api/reservas/{r.IdReserva}", new ReservaCreadaRespuesta { IdReserva = r.IdReserva, Estado = r.Estado.ToString() });
    }

    [HttpGet]
    public ActionResult<IEnumerable<ReservaListadoRespuesta>> Listar([FromQuery] string? estado, [FromQuery] string? texto)
    {
        EstadoReserva? e = null;
        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!Enum.TryParse<EstadoReserva>(estado, true, out var parsed)) throw new ValidacionDominioException("El estado de reserva no es válido.");
            e = parsed;
        }
        return _servicio.ConsultarReservas(e, texto).Select(r => new ReservaListadoRespuesta
        {
            IdReserva = r.IdReserva, Cliente = r.Cliente?.Nombre ?? "", Habitacion = r.NumeroHabitacion,
            FechaInicio = r.FechaInicio, FechaFin = r.FechaFin, Huespedes = r.Huespedes, Estado = r.Estado.ToString(),
            Penalidad = r.Penalidad, PenalidadExonerada = r.PenalidadExonerada,
        }).ToList();
    }

    [HttpPut("{id:int}/habitacion")]
    public IActionResult Reasignar(int id, ReasignacionSolicitud s)
    {
        var r = _servicio.Reasignar(id, s.NumeroHabitacionNueva, s.ConfirmarUpgrade, Usuario);
        return Ok(new ReservaCreadaRespuesta { IdReserva = r.IdReserva, Estado = r.Estado.ToString() });
    }

    [HttpPost("{id:int}/check-in")]
    public ActionResult<CheckInRespuesta> CheckIn(int id, CheckInSolicitud s)
    {
        var r = _servicio.HacerCheckIn(id, s.Documento, Usuario);
        return new CheckInRespuesta { EstadoReserva = r.Estado.ToString(), EstadoHabitacion = (r.Habitacion?.Estado ?? EstadoHabitacion.OCUPADO).ToString() };
    }
}
