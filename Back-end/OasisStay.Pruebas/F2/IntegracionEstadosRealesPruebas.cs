using OasisStay.Dominio;

namespace OasisStay.Pruebas.F2;

/// <summary>Integración de F2 con las 5 clases de estado reales de F1 [GoF State][DE#4 §6][DE#1].</summary>
public class IntegracionEstadosRealesPruebas
{
    private static Banco Nuevo()
    {
        var b = new Banco();
        b.Sembrar((304, "Suite Ejecutiva", EstadoHabitacion.DISPONIBLE), (105, "Doble Clásica", EstadoHabitacion.DISPONIBLE),
                  (107, "Doble Clásica", EstadoHabitacion.DISPONIBLE), (102, "Doble Clásica", EstadoHabitacion.EN_LIMPIEZA));
        return b;
    }

    [Fact] // [DE#4 CP-R01][DE#1 CP-H01]: Disponible → Reservada con EstadoDisponible/EstadoReservada reales
    public void CP_R01_Real_CreaConfirmada_HabitacionDisponibleAReservada()
    {
        var b = Nuevo();
        var (s, db) = b.Nuevo();
        var r = s.RegistrarReserva(Banco.Solicitud(304));
        Assert.Equal(EstadoReserva.Confirmada, r.Estado);
        Assert.IsType<EstadoReservada>(r.Habitacion!.estadoActual);
        using var v = b.Contexto();
        Assert.Equal(EstadoHabitacion.RESERVADA, v.Habitaciones.Single(h => h.NumeroHabitacion == 304).Estado);
    }

    [Fact] // [DE#1 CP-H02]: excede capacidad → rechazo y la habitación sigue Disponible
    public void CP_H02_Real_ExcedeCapacidad_SigueDisponible()
    {
        var b = Nuevo();
        var ex = Assert.Throws<ValidacionDominioException>(() => b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(105, 5)));
        Assert.Equal("Excede la capacidad de la habitación (RN-05)", ex.Message);
        using var v = b.Contexto();
        Assert.Equal(EstadoHabitacion.DISPONIBLE, v.Habitaciones.Single(h => h.NumeroHabitacion == 105).Estado);
    }

    [Fact] // reservar una habitación en limpieza lo rechaza la cadena con el texto acordado
    public void Real_HabitacionEnLimpieza_RechazaPorEstado()
    {
        var b = Nuevo();
        var ex = Assert.Throws<ValidacionDominioException>(() => b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(102)));
        Assert.Equal("La habitación no está disponible para reservar", ex.Message);
    }

    [Fact] // [DE#1 CP-H03]: Reservada + check-in con documento → Ocupada; la reserva pasa a EnCurso
    public void CP_H03_Real_CheckIn_ReservadaPasaAOcupada()
    {
        var b = Nuevo();
        var id = b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(304)).IdReserva;
        var r = b.Nuevo().Servicio.HacerCheckIn(id, "1020304050", "Recepcionista Demo");
        Assert.Equal(EstadoReserva.EnCurso, r.Estado);
        Assert.IsType<EstadoOcupada>(r.Habitacion!.estadoActual);
        using var v = b.Contexto();
        Assert.Equal(EstadoHabitacion.OCUPADO, v.Habitaciones.Single(h => h.NumeroHabitacion == 304).Estado);
    }

    [Fact] // [D-21][JIRA US-04 ESC3]: 105 Reservada → Disponible y 107 Disponible → Reservada con estados reales
    public void US04_ESC3_Real_Reasigna105a107()
    {
        var b = Nuevo();
        var id = b.Nuevo().Servicio.RegistrarReserva(Banco.Solicitud(105)).IdReserva;
        b.Nuevo().Servicio.Reasignar(id, 107, false, "Recepcionista Demo");
        using var v = b.Contexto();
        Assert.Equal(EstadoHabitacion.DISPONIBLE, v.Habitaciones.Single(h => h.NumeroHabitacion == 105).Estado);
        Assert.Equal(EstadoHabitacion.RESERVADA, v.Habitaciones.Single(h => h.NumeroHabitacion == 107).Estado);
        Assert.Equal(107, v.Reservas.Single().NumeroHabitacion);
    }
}
