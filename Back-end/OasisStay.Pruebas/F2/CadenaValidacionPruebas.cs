using OasisStay.Dominio;

namespace OasisStay.Pruebas.F2;

/// <summary>Chain of Responsibility de la reserva [GoF Chain][CTR §4].</summary>
public class CadenaValidacionPruebas
{
    private static Habitacion Hab(int cap = 4, EstadoHabitacion e = EstadoHabitacion.DISPONIBLE) => new()
    {
        NumeroHabitacion = 304, Estado = e, Tipo = new TipoHabitacion { Nombre = "Suite Ejecutiva", CapacidadMax = cap, PrecioBase = 320000m },
    };

    private static SolicitudReserva Sol(Habitacion h, int huespedes = 2, string ini = "2026-12-10", string fin = "2026-12-14") => new()
    {
        CheckIn = DateTime.Parse(ini), CheckOut = DateTime.Parse(fin), Huespedes = huespedes, Habitacion = h,
    };

    private static ValidadorReserva Cadena()
    {
        var f = new ValidadorFechas();
        f.Siguiente(new ValidadorCapacidad()).Siguiente(new ValidadorEstadoHabitacion()).Siguiente(new ValidadorSolapamiento());
        return f;
    }

    [Theory] // [GoF Chain][D-19][D-22]
    [InlineData("2026-12-14", "2026-12-10")]
    [InlineData("2026-12-10", "2026-12-10")]
    public void ValidadorFechas_SalidaIgualOAnterior_Error(string ini, string fin)
    {
        var r = new ValidadorFechas().Validar(Sol(Hab(), 2, ini, fin));
        Assert.False(r.EsValido);
        Assert.Equal("La fecha de salida debe ser posterior a la fecha de llegada", r.Mensaje);
    }

    [Fact] // [GoF Chain][RN-05]
    public void ValidadorCapacidad_Excede_Error()
    {
        var r = new ValidadorCapacidad().Validar(Sol(Hab(2), 5));
        Assert.False(r.EsValido);
        Assert.Equal("Excede la capacidad de la habitación (RN-05)", r.Mensaje);
        Assert.True(new ValidadorCapacidad().Validar(Sol(Hab(2), 2)).EsValido);
    }

    [Theory] // [GoF Chain]
    [InlineData(EstadoHabitacion.RESERVADA)]
    [InlineData(EstadoHabitacion.OCUPADO)]
    [InlineData(EstadoHabitacion.EN_LIMPIEZA)]
    [InlineData(EstadoHabitacion.MANTENIMIENTO)]
    public void ValidadorEstadoHabitacion_NoDisponible_Error(EstadoHabitacion estado)
    {
        var r = new ValidadorEstadoHabitacion().Validar(Sol(Hab(4, estado)));
        Assert.False(r.EsValido);
        Assert.Equal("La habitación no está disponible para reservar", r.Mensaje);
    }

    [Fact] // [GoF Chain][RN-01]
    public void ValidadorSolapamiento_Solapada_Error()
    {
        var s = Sol(Hab());
        s.ReservasExistentes = new[] { new Reserva { FechaInicio = new DateTime(2026, 12, 12), FechaFin = new DateTime(2026, 12, 16), Estado = EstadoReserva.Confirmada } };
        var r = new ValidadorSolapamiento().Validar(s);
        Assert.False(r.EsValido);
        Assert.Equal("Las fechas se solapan con otra reserva (RN-01)", r.Mensaje);
    }

    [Fact] // [RN-01]: salida y llegada el mismo día, o reservas canceladas, no solapan
    public void ValidadorSolapamiento_MismoDiaOCancelada_Ok()
    {
        var s = Sol(Hab());
        s.ReservasExistentes = new[]
        {
            new Reserva { FechaInicio = new DateTime(2026, 12, 6), FechaFin = new DateTime(2026, 12, 10), Estado = EstadoReserva.Confirmada },
            new Reserva { FechaInicio = new DateTime(2026, 12, 11), FechaFin = new DateTime(2026, 12, 13), Estado = EstadoReserva.Cancelada },
        };
        Assert.True(new ValidadorSolapamiento().Validar(s).EsValido);
    }

    [Fact] // [GoF Chain][E2 §3]
    public void Cadena_OrdenFijo_CortaEnLaPrimeraQueFalla()
    {
        // fechas, capacidad, estado y solapamiento fallan a la vez: gana el primero de la cadena
        var s = Sol(Hab(2, EstadoHabitacion.OCUPADO), 5, "2026-12-14", "2026-12-10");
        s.ReservasExistentes = new[] { new Reserva { FechaInicio = new DateTime(2026, 12, 1), FechaFin = new DateTime(2026, 12, 30), Estado = EstadoReserva.Confirmada } };
        Assert.Equal(MensajesReserva.FechasInvalidas, Cadena().Validar(s).Mensaje);

        s.CheckIn = DateTime.Parse("2026-12-10"); s.CheckOut = DateTime.Parse("2026-12-14");
        Assert.Equal(MensajesReserva.CapacidadExcedida, Cadena().Validar(s).Mensaje);

        s.Huespedes = 2;
        Assert.Equal(MensajesReserva.HabitacionNoDisponible, Cadena().Validar(s).Mensaje);

        s.Habitacion!.CambiarEstado(EstadoHabitacion.DISPONIBLE);
        Assert.Equal(MensajesReserva.Solapamiento, Cadena().Validar(s).Mensaje);

        s.ReservasExistentes = Array.Empty<Reserva>();
        Assert.True(Cadena().Validar(s).EsValido);
    }

    [Fact] // [GoF Chain] Siguiente devuelve el eslabón enlazado
    public void Siguiente_DevuelveElEnlazado()
    {
        var a = new ValidadorFechas(); var b = new ValidadorCapacidad();
        Assert.Same(b, a.Siguiente(b));
    }
}
