using OasisStay.Aplicacion;
using OasisStay.Dominio;

namespace OasisStay.Pruebas.F4;

/// <summary>US-08: cancelación, penalidad de la primera noche, exoneración y No-Show [JIRA US-08][DE#4][D-01][D-44].</summary>
public class CancelacionPruebas
{
    private readonly BancoF4 _b = new();

    private Reserva Cancelar(int id, bool exonerar = false, string? motivo = null, string usuario = "Recepcionista Demo")
    {
        using var db = _b.Banco.Contexto();
        return _b.Servicio(db).CancelarReserva(id, exonerar, motivo, usuario);
    }

    // ---- dominio: CP-R08 [DE#4 §5]
    [Theory]
    [InlineData(EstadoReserva.Finalizada)]
    [InlineData(EstadoReserva.Cancelada)]
    [InlineData(EstadoReserva.NoShow)]
    public void CP_R08_Finalizada_Cancelar_Rechazado(EstadoReserva estado)
    {
        var r = new Reserva { Estado = estado };
        var ex = Assert.Throws<ConflictoDominioException>(() => r.CancelarReserva());
        Assert.Equal("La reserva ya está cerrada y no puede cambiar de estado", ex.Message);
        Assert.Equal(estado, r.Estado);
    }

    [Fact] // [DE#4 §5]
    public void Cancelar_EnCurso_Rechazado()
    {
        var r = new Reserva { Estado = EstadoReserva.EnCurso };
        var ex = Assert.Throws<ConflictoDominioException>(() => r.CancelarReserva());
        Assert.Equal("Una reserva en curso no puede cancelarse", ex.Message);
        Assert.Equal(EstadoReserva.EnCurso, r.Estado);
    }

    [Fact] // [DE#4 §5]
    public void Cancelar_EnCurso_Servicio_Rechazado_YNoCambiaNada()
    {
        var id = _b.Crear(BancoF4.CheckInBase.AddDays(-1), estado: EstadoReserva.EnCurso);
        _b.Reloj.Ahora = new DateTime(2026, 11, 25, 20, 0, 0);
        var ex = Assert.Throws<ConflictoDominioException>(() => Cancelar(id));
        Assert.Equal("Una reserva en curso no puede cancelarse", ex.Message);
        Assert.Equal(EstadoReserva.EnCurso, _b.Reserva(id).Estado);
        Assert.Equal(EstadoHabitacion.OCUPADO, _b.EstadoHabitacionDe(id));
    }

    [Fact] // [DE#4 CP-R03]
    public void CP_R03_Cancelar_DentroDelPlazo_SinCobro()
    {
        var id = _b.Crear(BancoF4.CheckInBase);
        _b.Reloj.Ahora = new DateTime(2026, 11, 20, 10, 0, 0);
        var r = Cancelar(id);
        Assert.Equal(EstadoReserva.Cancelada, r.Estado);
        Assert.Equal(0m, _b.Reserva(id).Penalidad);
        Assert.Equal(EstadoHabitacion.DISPONIBLE, _b.EstadoHabitacionDe(id));
    }

    [Fact] // [DE#4 CP-R04]
    public void CP_R04_Cancelar_FueraDelPlazo_CobraPrimeraNoche()
    {
        var id = _b.Crear(BancoF4.CheckInBase);
        _b.Reloj.Ahora = new DateTime(2026, 11, 24, 20, 0, 0);
        Cancelar(id);
        var r = _b.Reserva(id);
        Assert.Equal(EstadoReserva.Cancelada, r.Estado);
        Assert.Equal(220000m, r.Penalidad);
        Assert.False(r.PenalidadExonerada);
        Assert.Equal(EstadoHabitacion.DISPONIBLE, _b.EstadoHabitacionDe(id));
    }

    [Fact] // [DE#4 CP-R05]
    public void CP_R05_NoShow_FinDelDia_CobraPrimeraNoche()
    {
        var id = _b.Crear(BancoF4.CheckInBase);
        var enCurso = _b.Crear(BancoF4.CheckInBase, estado: EstadoReserva.EnCurso);
        var futura = _b.Crear(BancoF4.CheckInBase.AddDays(1));
        _b.Reloj.Ahora = new DateTime(2026, 11, 25, 23, 59, 0);
        int procesadas;
        using (var db = _b.Banco.Contexto()) procesadas = _b.Servicio(db).EjecutarNoShow(BancoF4.CheckInBase);

        Assert.Equal(1, procesadas);
        var r = _b.Reserva(id);
        Assert.Equal(EstadoReserva.NoShow, r.Estado);
        Assert.Equal(220000m, r.Penalidad);
        Assert.Equal(EstadoHabitacion.DISPONIBLE, _b.EstadoHabitacionDe(id));
        Assert.Equal(EstadoReserva.EnCurso, _b.Reserva(enCurso).Estado);
        Assert.Equal(EstadoReserva.Confirmada, _b.Reserva(futura).Estado);
        Assert.Contains(_b.Leer(db => db.HistorialReservas.Where(h => h.IdReserva == id).ToList()), h => h.ValorNuevo!.Contains("NoShow"));
    }

    [Fact] // [DE#4 CP-R05]
    public void CP_R05_NoShow_AntesDelCorte_NoProcesa()
    {
        var id = _b.Crear(BancoF4.CheckInBase);
        using (var db = _b.Banco.Contexto()) Assert.Equal(0, _b.Servicio(db).EjecutarNoShow(BancoF4.CheckInBase.AddDays(-1)));
        Assert.Equal(EstadoReserva.Confirmada, _b.Reserva(id).Estado);
    }

    // ---- escenarios de Jira
    [Fact] // [JIRA US-08 ESC1]
    public void US08_ESC1_CancelacionGratuita_48h_PenalidadCero_HabitacionDisponible()
    {
        var id = _b.Crear(BancoF4.CheckInBase);
        _b.Reloj.Ahora = new DateTime(2026, 11, 23, 15, 0, 0); // 48 h antes del check-in
        Cancelar(id);
        Assert.Equal(EstadoReserva.Cancelada, _b.Reserva(id).Estado);
        Assert.Equal(0m, _b.Reserva(id).Penalidad);
        Assert.Equal(EstadoHabitacion.DISPONIBLE, _b.EstadoHabitacionDe(id));
    }

    [Fact] // [JIRA US-08 ESC2]
    public void US08_ESC2_CancelacionTardia_12h_Penalidad180000()
    {
        var id = _b.Crear(BancoF4.CheckInBase, "Tarifa 180");
        _b.Reloj.Ahora = new DateTime(2026, 11, 25, 3, 0, 0); // 12 h antes
        Cancelar(id);
        Assert.Equal(180000m, _b.Reserva(id).Penalidad);
        Assert.Equal(EstadoHabitacion.DISPONIBLE, _b.EstadoHabitacionDe(id));
    }

    [Fact] // [JIRA US-08 ESC3][D-24]
    public void US08_ESC3_Exoneracion_Administrador_CobroCeroYJustificacionEnLog()
    {
        var id = _b.Crear(BancoF4.CheckInBase);
        _b.Reloj.Ahora = new DateTime(2026, 11, 24, 20, 0, 0);
        Cancelar(id, true, "Emergencia médica demostrada", "Administrador Demo");
        var r = _b.Reserva(id);
        Assert.Equal(EstadoReserva.Cancelada, r.Estado);
        Assert.Equal(0m, r.Penalidad);
        Assert.True(r.PenalidadExonerada);
        Assert.Equal("Emergencia médica demostrada", r.MotivoExoneracion);
        var evento = _b.Leer(db => db.HistorialReservas.Single(h => h.IdReserva == id));
        Assert.Contains("Emergencia médica demostrada", evento.ValorNuevo);
        Assert.Equal("Administrador Demo", evento.Usuario);
    }

    [Fact] // [JIRA US-08 datos Caso A]
    public void US08_Datos_CasoA_20Nov_Gratis()
    {
        var id = _b.Crear(BancoF4.CheckInBase);
        _b.Reloj.Ahora = new DateTime(2026, 11, 20, 10, 0, 0);
        Cancelar(id);
        Assert.Equal(0m, _b.Reserva(id).Penalidad);
    }

    [Fact] // [JIRA US-08 datos Caso B]
    public void US08_Datos_CasoB_24Nov2000_19h_Penalidad220000()
    {
        var id = _b.Crear(BancoF4.CheckInBase);
        _b.Reloj.Ahora = new DateTime(2026, 11, 24, 20, 0, 0);
        Cancelar(id);
        Assert.Equal(220000m, _b.Reserva(id).Penalidad);
    }

    [Fact] // [JIRA US-08 datos Caso C]
    public void US08_Datos_CasoC_Exonerada_EmergenciaMedicaDemostrada()
    {
        var id = _b.Crear(BancoF4.CheckInBase);
        _b.Reloj.Ahora = new DateTime(2026, 11, 24, 20, 0, 0);
        Cancelar(id, true, "Emergencia médica demostrada", "Administrador Demo");
        var r = _b.Reserva(id);
        Assert.Equal((0m, true, "Emergencia médica demostrada"), (r.Penalidad, r.PenalidadExonerada, r.MotivoExoneracion));
    }

    [Fact] // [JIRA US-08 RN1]
    public void US08_Limite_Exacto24h_EsGratis()
    {
        var id = _b.Crear(BancoF4.CheckInBase);
        _b.Reloj.Ahora = new DateTime(2026, 11, 24, 15, 0, 0);
        using (var db = _b.Banco.Contexto())
        {
            var p = _b.Servicio(db).CalcularPenalidad(id);
            Assert.True(p.Gratis);
            Assert.Equal(new DateTime(2026, 11, 24, 15, 0, 0), p.Limite);
        }
        Cancelar(id);
        Assert.Equal(0m, _b.Reserva(id).Penalidad);
    }

    [Fact] // [JIRA US-08 RN1]
    public void US08_23h59_CobraPenalidad()
    {
        var id = _b.Crear(BancoF4.CheckInBase);
        _b.Reloj.Ahora = new DateTime(2026, 11, 24, 15, 1, 0);
        Cancelar(id);
        Assert.Equal(220000m, _b.Reserva(id).Penalidad);
    }

    [Fact] // [JIRA US-08 UX]: consulta sin efectos
    public void US08_CalcularPenalidad_VistaPrevia_SinEfectos()
    {
        var id = _b.Crear(BancoF4.CheckInBase);
        _b.Reloj.Ahora = new DateTime(2026, 11, 24, 20, 0, 0);
        using (var db = _b.Banco.Contexto())
        {
            var p = _b.Servicio(db).CalcularPenalidad(id);
            Assert.False(p.Gratis);
            Assert.Equal(220000m, p.Penalidad);
        }
        Assert.Equal(EstadoReserva.Confirmada, _b.Reserva(id).Estado);
        Assert.Equal(EstadoHabitacion.RESERVADA, _b.EstadoHabitacionDe(id));
        Assert.Equal(0m, _b.Reserva(id).Penalidad);
    }

    [Fact] // [JIRA US-08 RN3]
    public void US08_Exonerar_SinMotivo_400()
    {
        var id = _b.Crear(BancoF4.CheckInBase);
        _b.Reloj.Ahora = new DateTime(2026, 11, 24, 20, 0, 0);
        var ex = Assert.Throws<ValidacionDominioException>(() => Cancelar(id, true, "  ", "Administrador Demo"));
        Assert.Equal("Debe ingresar la justificación de la exoneración", ex.Message);
        Assert.Equal(EstadoReserva.Confirmada, _b.Reserva(id).Estado);
    }

    [Fact] // [D-23]
    public void US08_Cancelar_RegistraEventoEnHistorial_ConUsuarioYValores()
    {
        var id = _b.Crear(BancoF4.CheckInBase);
        _b.Reloj.Ahora = new DateTime(2026, 11, 24, 20, 0, 0);
        Cancelar(id, usuario: "Recepcionista Demo");
        var e = _b.Leer(db => db.HistorialReservas.Single(h => h.IdReserva == id));
        Assert.Equal("Recepcionista Demo", e.Usuario);
        Assert.Contains("Confirmada", e.ValorAnterior);
        Assert.Contains("Cancelada", e.ValorNuevo);
        Assert.Contains("220.000", e.ValorNuevo);
        Assert.Equal(_b.Reloj.Ahora, e.FechaRegistro);
    }

    [Fact]
    public void US08_Cancelar_ReservaInexistente_404()
        => Assert.Throws<NoEncontradoException>(() => Cancelar(9999));
}
