using Microsoft.EntityFrameworkCore;
using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Repositorios;
using OasisStay.Infraestructura.Seguridad;

namespace OasisStay.Pruebas.F2;

/// <summary>
/// Atomicidad real contra SQL Server local, base propia OasisStay_PruebasF2 (se borra al terminar) [D-06][JIRA US-04 NFR][DS1].
/// La unidad de trabajo es la real (UnidadDeTrabajoEf): si falla un paso, se revierte todo.
/// </summary>
[Collection("SqlServer")]
public class AtomicidadPruebas : IAsyncLifetime
{
    private static readonly string Cadena = NombresBasePruebas.Cadena("F2");
    private readonly RelojFijo _reloj = new();

    private static OasisStayDbContext Contexto() =>
        new(new DbContextOptionsBuilder<OasisStayDbContext>().UseSqlServer(Cadena).Options, new CifradorAes256(Banco.Clave));

    public async Task InitializeAsync()
    {
        await using var db = Contexto();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        db.TiposHabitacion.Add(new TipoHabitacion { Nombre = "Doble Clásica", CapacidadMax = 2, PrecioBase = 185000m });
        await db.SaveChangesAsync();
        var tipo = db.TiposHabitacion.Single().IdTipo;
        db.Habitaciones.AddRange(
            new Habitacion { NumeroHabitacion = 105, Piso = 1, IdTipo = tipo, Estado = EstadoHabitacion.DISPONIBLE },
            new Habitacion { NumeroHabitacion = 107, Piso = 1, IdTipo = tipo, Estado = EstadoHabitacion.DISPONIBLE });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = Contexto();
        await db.Database.EnsureDeletedAsync();
    }

    private sealed class HistorialQueFalla : IRepositorioHistorial
    {
        public void RegistrarEvento(HistorialReserva evento) => throw new InvalidOperationException("Fallo forzado después de guardar reserva y habitaciones");
        public void Archivar(int idReserva, Factura factura) => throw new NotImplementedException();
    }

    private GestionReservas Servicio(OasisStayDbContext db, bool historialFalla) => new(
        new RepositorioHabitaciones(db), new RepositorioReservas(db), new RepositorioHuespedes(db),
        historialFalla ? new HistorialQueFalla() : new RepositorioHistorial(db), new NotificadorSimulado(db, _reloj), _reloj,
        new MotorDisponibilidadAsignacion(new UnidadDeTrabajoEf(db)));

    [Fact] // [DS1][JIRA US-04 NFR]
    public void US04_Atomicidad_FalloAMitad_NoDejaNadaGuardado()
    {
        using (var db = Contexto())
            Assert.Throws<InvalidOperationException>(() => Servicio(db, true).RegistrarReserva(Banco.Solicitud(105)));

        using var v = Contexto();
        Assert.Empty(v.Reservas);
        Assert.Empty(v.Clientes);
        Assert.Empty(v.Notificaciones);
        Assert.Equal(EstadoHabitacion.DISPONIBLE, v.Habitaciones.Single(h => h.NumeroHabitacion == 105).Estado);
    }

    [Fact] // [D-21][JIRA US-04 ESC3]
    public void US04_Reasignacion_FalloAMitad_RevierteAmbasHabitaciones()
    {
        int id;
        using (var db = Contexto()) id = Servicio(db, false).RegistrarReserva(Banco.Solicitud(105)).IdReserva;

        using (var db = Contexto())
            Assert.Throws<InvalidOperationException>(() => Servicio(db, true).Reasignar(id, 107, false, "Recepcionista Demo"));

        using var v = Contexto();
        Assert.Equal(105, v.Reservas.Single().NumeroHabitacion);
        Assert.Equal(EstadoHabitacion.RESERVADA, v.Habitaciones.Single(h => h.NumeroHabitacion == 105).Estado);
        Assert.Equal(EstadoHabitacion.DISPONIBLE, v.Habitaciones.Single(h => h.NumeroHabitacion == 107).Estado);
        Assert.DoesNotContain(v.HistorialReservas, h => h.AccionRealizada == "Habitación reasignada");
    }
}
