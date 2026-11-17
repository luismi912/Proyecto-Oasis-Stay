using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Repositorios;
using OasisStay.Infraestructura.Seguridad;

namespace OasisStay.Pruebas.F2;

/// <summary>Unidad de trabajo vacía: EF InMemory no tiene transacciones.</summary>
public class UnidadDeTrabajoNula : IUnidadDeTrabajo
{
    public ITransaccion Iniciar() => new Tx();
    private sealed class Tx : ITransaccion { public void Confirmar() { } public void Dispose() { } }
}

public class RelojFijo : IReloj
{
    public DateTime Ahora { get; set; } = new(2026, 12, 10, 16, 0, 0);
}

/// <summary>Banco de pruebas: BD en memoria compartida con contextos y servicio nuevos por cada llamada a Nuevo().</summary>
public class Banco
{
    public static readonly byte[] Clave = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    public string Nombre { get; } = "F2_" + Guid.NewGuid();
    public RelojFijo Reloj { get; } = new();

    public OasisStayDbContext Contexto() => new(new DbContextOptionsBuilder<OasisStayDbContext>()
        .UseInMemoryDatabase(Nombre)
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options, new CifradorAes256(Clave));

    public (GestionReservas Servicio, OasisStayDbContext Db) Nuevo()
    {
        var db = Contexto();
        var servicio = new GestionReservas(
            new RepositorioHabitaciones(db), new RepositorioReservas(db),
            new RepositorioHuespedes(db), new RepositorioHistorial(db), new NotificadorSimulado(db, Reloj), Reloj,
            new MotorDisponibilidadAsignacion(new UnidadDeTrabajoNula()));
        return (servicio, db);
    }

    /// <summary>Tipos y habitaciones de los escenarios (D-29 y Jira): 304 Suite CapMax 4, 105/107 Doble CapMax 2, 201 Suite.</summary>
    public void Sembrar(params (int n, string tipo, EstadoHabitacion e)[] habitaciones)
    {
        using var db = Contexto();
        if (!db.TiposHabitacion.Any())
        {
            db.TiposHabitacion.AddRange(
                new TipoHabitacion { Nombre = "Doble Clásica", CapacidadMax = 2, PrecioBase = 185000m },
                new TipoHabitacion { Nombre = "Suite Ejecutiva", CapacidadMax = 4, PrecioBase = 320000m });
            db.SaveChanges();
        }
        var t = db.TiposHabitacion.ToDictionary(x => x.Nombre, x => x.IdTipo);
        foreach (var (n, tipo, e) in habitaciones)
            db.Habitaciones.Add(new Habitacion { NumeroHabitacion = n, Piso = n / 100, IdTipo = t[tipo], Estado = e });
        db.SaveChanges();
    }

    public static Cliente Carlos() => new()
    {
        TipoDocumento = TipoDocumento.CC, Documento = "1020304050", Nombre = "Carlos Mendoza",
        Telefono = "3101234567", Correo = "carlos.mendoza@email.com",
    };

    public static SolicitudReserva Solicitud(int habitacion, int huespedes = 2, string ini = "2026-12-10", string fin = "2026-12-14") => new()
    {
        CheckIn = DateTime.Parse(ini), CheckOut = DateTime.Parse(fin), Huespedes = huespedes, Usuario = "Recepcionista Demo",
        Habitacion = new Habitacion { NumeroHabitacion = habitacion }, Cliente = Carlos(),
    };
}
