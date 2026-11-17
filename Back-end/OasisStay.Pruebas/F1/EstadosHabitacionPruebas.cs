using Microsoft.Extensions.DependencyInjection;
using OasisStay.Dominio;

namespace OasisStay.Pruebas.F1;

/// <summary>State de la habitación [DE#1][GoF State][E2 §3 State].</summary>
public class EstadosHabitacionPruebas
{
    private static Habitacion Hab(EstadoHabitacion e) { var h = new Habitacion { NumeroHabitacion = 101, Estado = e }; h.ReconstruirEstado(); return h; }

    [Fact] // [D-39][D-29]
    public async Task F1_SemillaDemo_Idempotente_NoPisaCambios()
    {
        using var f = new FabricaApiF1();
        var semilla = new OasisStay.Infraestructura.Semilla.SemillaDemoF1();
        using (var s = f.Services.CreateScope())
            await semilla.SembrarAsync(s.ServiceProvider.GetRequiredService<OasisStay.Infraestructura.Persistencia.OasisStayDbContext>());
        Assert.Equal(EstadoHabitacion.EN_LIMPIEZA, f.Leer(db => db.Habitaciones.Find(301)!.Estado));
        Assert.Equal(EstadoHabitacion.OCUPADO, f.Leer(db => db.Habitaciones.Find(204)!.Estado));

        f.Preparar(db => db.Habitaciones.Find(301)!.Estado = EstadoHabitacion.DISPONIBLE);
        using (var s = f.Services.CreateScope())
            await semilla.SembrarAsync(s.ServiceProvider.GetRequiredService<OasisStay.Infraestructura.Persistencia.OasisStayDbContext>());
        Assert.Equal(EstadoHabitacion.DISPONIBLE, f.Leer(db => db.Habitaciones.Find(301)!.Estado));
        Assert.Equal(EstadoHabitacion.OCUPADO, f.Leer(db => db.Habitaciones.Find(204)!.Estado));
        Assert.Equal(1, f.Leer(db => db.Habitaciones.Count(h => h.NumeroHabitacion == 105)));
    }

    [Fact] // [DE#1 CP-H01]
    public void CP_H01_DisponibleReservar_PasaAReservada()
    {
        var h = Hab(EstadoHabitacion.DISPONIBLE);
        Assert.True(h.Reservar().EsExitosa);
        Assert.Equal(EstadoHabitacion.RESERVADA, h.Estado);
        Assert.IsType<EstadoReservada>(h.estadoActual);
    }

    [Fact] // [DE#1 CP-H03]
    public void CP_H03_ReservadaCheckIn_PasaAOcupada()
    {
        var h = Hab(EstadoHabitacion.RESERVADA);
        Assert.True(h.HacerCheckIn().EsExitosa);
        Assert.Equal(EstadoHabitacion.OCUPADO, h.Estado);
    }

    [Fact] // [DE#1 CP-H05]
    public void CP_H05_EnLimpiezaMarcarDisponible_PasaADisponible()
    {
        var h = Hab(EstadoHabitacion.EN_LIMPIEZA);
        Assert.True(h.MarcarDisponible().EsExitosa);
        Assert.Equal(EstadoHabitacion.DISPONIBLE, h.Estado);
    }

    [Fact] // [DE#1 CP-H06]
    public void CP_H06_EnLimpiezaCheckIn_Rechazado()
    {
        var h = Hab(EstadoHabitacion.EN_LIMPIEZA);
        var r = h.HacerCheckIn();
        Assert.False(r.EsExitosa);
        Assert.Equal(EstadoHabitacion.EN_LIMPIEZA, h.Estado);
    }

    [Fact] // [DE#1 CP-H07]
    public void CP_H07_DisponibleMantenimientoSinMotivo_Rechazado()
    {
        var h = Hab(EstadoHabitacion.DISPONIBLE);
        Assert.False(h.MarcarMantenimiento("").EsExitosa);
        Assert.False(h.MarcarMantenimiento("corto").EsExitosa);
        Assert.Equal(EstadoHabitacion.DISPONIBLE, h.Estado);
        Assert.True(h.MarcarMantenimiento("Fuga en grifería de baño principal").EsExitosa);
        Assert.Equal(EstadoHabitacion.MANTENIMIENTO, h.Estado);
        Assert.Equal("Fuga en grifería de baño principal", h.MotivoMantenimiento);
    }

    [Fact] // [DE#1 CP-H08]
    public void CP_H08_MantenimientoReservar_Rechazado()
    {
        var h = Hab(EstadoHabitacion.MANTENIMIENTO);
        Assert.False(h.Reservar().EsExitosa);
        Assert.Equal(EstadoHabitacion.MANTENIMIENTO, h.Estado);
    }

    [Fact] // [GoF State][CTR §3]
    public void GoF_EstadoEnLimpieza_Reservar_MensajeAcordado()
    {
        var r = Hab(EstadoHabitacion.EN_LIMPIEZA).Reservar();
        Assert.False(r.EsExitosa);
        Assert.Equal("La habitación está en limpieza", r.Motivo);
    }

    [Fact] // [E2 §3 State]
    public void State_ReconstruyeDesdeTexto_CincoEstados()
    {
        Assert.IsType<EstadoDisponible>(Habitacion.EstadoDesdeTexto("DISPONIBLE"));
        Assert.IsType<EstadoReservada>(Habitacion.EstadoDesdeTexto("RESERVADA"));
        Assert.IsType<EstadoOcupada>(Habitacion.EstadoDesdeTexto("OCUPADO"));
        Assert.IsType<EstadoEnLimpieza>(Habitacion.EstadoDesdeTexto("EN_LIMPIEZA"));
        Assert.IsType<EstadoMantenimiento>(Habitacion.EstadoDesdeTexto("MANTENIMIENTO"));
        foreach (var e in Enum.GetValues<EstadoHabitacion>())
        {
            var h = new Habitacion { Estado = e };
            h.ReconstruirEstado();
            Assert.NotNull(h.estadoActual);
        }
    }

    [Fact] // [DE#1 §7 Esc. 3][JIRA US-02 RN3]
    public void State_OcupadaMarcarDisponible_Rechazada()
    {
        var h = Hab(EstadoHabitacion.OCUPADO);
        var r = h.MarcarDisponible();
        Assert.False(r.EsExitosa);
        Assert.Equal("La habitación está ocupada: requiere check-out previo", r.Motivo);
        Assert.Equal(EstadoHabitacion.OCUPADO, h.Estado);
    }

    [Fact] // [DE#1][D-21]
    public void State_ReservadaMarcarDisponible_Disponible()
    {
        var h = Hab(EstadoHabitacion.RESERVADA);
        Assert.True(h.MarcarDisponible().EsExitosa);
        Assert.Equal(EstadoHabitacion.DISPONIBLE, h.Estado);
    }

    [Fact] // [DE#1 §4 Guardas][CTR §3]
    public void State_PuedeReservarse_SoloDesdeDisponible_YCheckOutDesdeOcupada()
    {
        Assert.True(Hab(EstadoHabitacion.DISPONIBLE).PuedeReservarse());
        Assert.False(Hab(EstadoHabitacion.RESERVADA).PuedeReservarse());
        var h = Hab(EstadoHabitacion.OCUPADO);
        Assert.True(h.HacerCheckOut().EsExitosa);
        Assert.Equal(EstadoHabitacion.EN_LIMPIEZA, h.Estado);
        Assert.Equal("La operación no está permitida en estado Disponible", Hab(EstadoHabitacion.DISPONIBLE).HacerCheckIn().Motivo);
    }
}
