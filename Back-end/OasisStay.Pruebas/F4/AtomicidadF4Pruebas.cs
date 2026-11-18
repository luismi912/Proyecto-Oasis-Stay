using Microsoft.EntityFrameworkCore;
using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Repositorios;
using OasisStay.Infraestructura.Seguridad;
using OasisStay.Pruebas.F2;

namespace OasisStay.Pruebas.F4;

/// <summary>Atomicidad real contra SQL Server, base propia OasisStay_PruebasF4 (se borra al terminar) [D-06][DE#4].</summary>
[Collection("SqlServer")]
public class AtomicidadF4Pruebas : IAsyncLifetime
{
    private static readonly string Cadena = NombresBasePruebas.Cadena("F4");
    private readonly RelojFijo _reloj = new() { Ahora = new DateTime(2026, 11, 24, 20, 0, 0) };

    private static OasisStayDbContext Contexto() =>
        new(new DbContextOptionsBuilder<OasisStayDbContext>().UseSqlServer(Cadena).Options, new CifradorAes256(Banco.Clave));

    public async Task InitializeAsync()
    {
        await using var db = Contexto();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        db.TiposHabitacion.Add(new TipoHabitacion { Nombre = "Tarifa 220", CapacidadMax = 2, PrecioBase = 220000m });
        await db.SaveChangesAsync();
        db.Habitaciones.Add(new Habitacion { NumeroHabitacion = 105, Piso = 1, IdTipo = db.TiposHabitacion.Single().IdTipo, Estado = EstadoHabitacion.RESERVADA });
        db.Reservas.Add(new Reserva
        {
            Cliente = new Cliente { TipoDocumento = TipoDocumento.CC, Documento = "1020304050", Nombre = "Carlos Mendoza", Telefono = "3101234567", Correo = "c@email.com" },
            NumeroHabitacion = 105, Huespedes = 2, FechaInicio = new DateTime(2026, 11, 25), FechaFin = new DateTime(2026, 11, 27),
        });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = Contexto();
        await db.Database.EnsureDeletedAsync();
    }

    private sealed class HistorialQueFalla : IRepositorioHistorial
    {
        public void RegistrarEvento(HistorialReserva evento) => throw new InvalidOperationException("Fallo forzado después de guardar reserva y habitación");
        public void Archivar(int idReserva, Factura factura) => throw new NotImplementedException();
    }

    private GestionReservas Servicio(OasisStayDbContext db, bool historialFalla) => new(
        new RepositorioHabitaciones(db), new RepositorioReservas(db), new RepositorioHuespedes(db),
        historialFalla ? new HistorialQueFalla() : new RepositorioHistorial(db), new NotificadorSimulado(db, _reloj), _reloj,
        new MotorDisponibilidadAsignacion(new UnidadDeTrabajoEf(db)), new PoliticasOpciones());

    [Fact] // [DE#4]
    public void US08_Cancelar_Atomicidad_FalloAMitad_NoDejaNadaGuardado()
    {
        int id;
        using (var db = Contexto()) id = db.Reservas.Single().IdReserva;
        using (var db = Contexto())
            Assert.Throws<InvalidOperationException>(() => Servicio(db, true).CancelarReserva(id, false, null, "Recepcionista Demo"));

        using var v = Contexto();
        var r = v.Reservas.Single();
        Assert.Equal(EstadoReserva.Confirmada, r.Estado);
        Assert.Equal(0m, r.Penalidad);
        Assert.Equal(EstadoHabitacion.RESERVADA, v.Habitaciones.Single().Estado);
        Assert.Empty(v.HistorialReservas);
    }

    [Fact] // [DE#4]
    public void US08_Cancelar_Real_ConfirmaTodoEnUnaTransaccion()
    {
        int id;
        using (var db = Contexto()) id = db.Reservas.Single().IdReserva;
        using (var db = Contexto()) Servicio(db, false).CancelarReserva(id, false, null, "Recepcionista Demo");

        using var v = Contexto();
        var r = v.Reservas.Single();
        Assert.Equal((EstadoReserva.Cancelada, 220000m), (r.Estado, r.Penalidad));
        Assert.Equal(EstadoHabitacion.DISPONIBLE, v.Habitaciones.Single().Estado);
        Assert.Single(v.HistorialReservas);
    }
}
