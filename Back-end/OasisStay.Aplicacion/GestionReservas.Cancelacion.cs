using System.Globalization;
using OasisStay.Dominio;

namespace OasisStay.Aplicacion;

/// <summary>Vista previa de la penalidad antes de cancelar [JIRA US-08 UX][D-44].</summary>
public record PenalidadCalculada(bool Gratis, decimal Penalidad, DateTime Limite);

/// <summary>Cancelación, No-Show y penalidad [JIRA US-08][DE#4][D-44].</summary>
public interface ICancelacionReservas
{
    /// <summary>Consulta sin efectos: gratis o penalidad y límite del plazo [JIRA US-08 UX].</summary>
    PenalidadCalculada CalcularPenalidad(int idReserva);
    /// <summary>Confirmada → Cancelada con penalidad, habitación Disponible y evento de historial en una transacción [JIRA US-08 RN1-RN4][D-23][D-44].</summary>
    Reserva CancelarReserva(int idReserva, bool exonerar, string? motivo, string usuario);
    /// <summary>Confirmadas con FechaInicio ≤ fechaCorte sin check-in → NoShow con penalidad; devuelve cuántas [DE#4 CP-R05][D-44].</summary>
    int EjecutarNoShow(DateTime fechaCorte);
}

public partial class GestionReservas : ICancelacionReservas
{
    private PoliticasOpciones _politicas = new();

    public GestionReservas(IRepositorioHabitaciones habitaciones, IRepositorioReservas reservas,
        IRepositorioHuespedes huespedes, IRepositorioHistorial historial, INotificador notificador,
        IReloj reloj, MotorDisponibilidadAsignacion motor, PoliticasOpciones politicas)
        : this(habitaciones, reservas, huespedes, historial, notificador, reloj, motor)
        => _politicas = politicas;

    public PenalidadCalculada CalcularPenalidad(int idReserva)
    {
        var r = _reservas.BuscarPorId(idReserva) ?? throw new NoEncontradoException("La reserva no existe.");
        r.ValidarCancelable();
        var limite = r.LimiteCancelacionGratuita(_politicas.HoraCheckIn, _politicas.PlazoCancelacionHoras);
        var penalidad = r.CalcularPenalidad(_reloj.Ahora, _politicas.HoraCheckIn, _politicas.PlazoCancelacionHoras);
        return new PenalidadCalculada(penalidad == 0m, penalidad, limite);
    }

    public Reserva CancelarReserva(int idReserva, bool exonerar, string? motivo, string usuario)
    {
        if (exonerar && string.IsNullOrWhiteSpace(motivo))
            throw new ValidacionDominioException(MensajesReserva.FaltaJustificacion);
        var previa = _reservas.BuscarPorId(idReserva) ?? throw new NoEncontradoException("La reserva no existe.");
        return _motor.EjecutarBloqueado(new[] { previa.NumeroHabitacion },
            () => Cerrar(idReserva, EstadoReserva.Cancelada, exonerar, motivo?.Trim(), usuario));
    }

    public int EjecutarNoShow(DateTime fechaCorte)
    {
        var procesadas = 0;
        foreach (var pendiente in _reservas.ListarConfirmadasHasta(fechaCorte.Date))
        {
            _motor.EjecutarBloqueado(new[] { pendiente.NumeroHabitacion },
                () => Cerrar(pendiente.IdReserva, EstadoReserva.NoShow, false, null, "sistema"));
            procesadas++;
        }
        return procesadas;
    }

    /// <summary>Cierra la reserva (Cancelada o NoShow): penalidad, habitación Disponible y evento en el historial [DE#4 §6][D-21][D-23].</summary>
    private Reserva Cerrar(int idReserva, EstadoReserva destino, bool exonerar, string? motivo, string usuario)
    {
        var reserva = _reservas.BuscarPorId(idReserva) ?? throw new NoEncontradoException("La reserva no existe.");
        var anterior = reserva.Estado;
        var penalidad = destino == EstadoReserva.NoShow
            ? reserva.PrimeraNoche()
            : reserva.CalcularPenalidad(_reloj.Ahora, _politicas.HoraCheckIn, _politicas.PlazoCancelacionHoras);

        if (destino == EstadoReserva.NoShow) reserva.MarcarNoShow(); else reserva.CancelarReserva();

        var exonerada = exonerar && penalidad > 0m; // solo se exonera lo que habría que cobrar [JIRA US-08 ESC3]
        reserva.Penalidad = exonerada ? 0m : penalidad;
        reserva.PenalidadExonerada = exonerada;
        reserva.MotivoExoneracion = exonerada ? motivo : null;

        var habitacion = _habitaciones.BuscarPorNumero(reserva.NumeroHabitacion)
            ?? throw new NoEncontradoException("La habitación no existe.");
        if (habitacion.Estado != EstadoHabitacion.DISPONIBLE)
        {
            var libera = habitacion.MarcarDisponible();
            if (!libera.EsExitosa) throw new ConflictoDominioException(libera.Motivo!);
            _habitaciones.Guardar(habitacion);
        }
        _reservas.Guardar(reserva);

        var cargo = exonerada ? $"Penalidad exonerada · Motivo: {motivo}"
            : $"Penalidad ${penalidad.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.')}";
        Historial(reserva.IdReserva, usuario, destino == EstadoReserva.NoShow ? "No-Show" : "Cancelación",
            anterior.ToString(), $"{destino} · {cargo}");
        return reserva;
    }
}
