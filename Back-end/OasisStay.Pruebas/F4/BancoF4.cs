using Microsoft.EntityFrameworkCore;
using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Repositorios;
using OasisStay.Pruebas.F2;

namespace OasisStay.Pruebas.F4;

/// <summary>Banco en memoria de F4 con las tarifas de Jira US-08: noche $220.000 (reserva base) y $180.000 (ESC2) [JIRA US-08 datos].</summary>
public class BancoF4
{
    public Banco Banco { get; } = new();
    public RelojFijo Reloj => Banco.Reloj;
    private static int _doc = 7000000;

    public BancoF4()
    {
        using var db = Banco.Contexto();
        db.TiposHabitacion.AddRange(
            new TipoHabitacion { Nombre = "Tarifa 220", CapacidadMax = 2, PrecioBase = 220000m },
            new TipoHabitacion { Nombre = "Tarifa 180", CapacidadMax = 2, PrecioBase = 180000m });
        db.SaveChanges();
    }

    public GestionReservas Servicio(OasisStayDbContext db) => new(
        new RepositorioHabitaciones(db), new RepositorioReservas(db), new RepositorioHuespedes(db),
        new RepositorioHistorial(db), new NotificadorSimulado(db, Reloj), Reloj,
        new MotorDisponibilidadAsignacion(new UnidadDeTrabajoNula()), new PoliticasOpciones());

    /// <summary>Reserva en el estado dado, con su habitación en el estado que le corresponde y la noche del tipo indicado.</summary>
    public int Crear(DateTime fechaInicio, string tipo = "Tarifa 220", EstadoReserva estado = EstadoReserva.Confirmada, int noches = 2)
    {
        using var db = Banco.Contexto();
        var n = 500 + db.Habitaciones.Count() + 1;
        var idTipo = db.TiposHabitacion.Single(t => t.Nombre == tipo).IdTipo;
        var hab = estado == EstadoReserva.Confirmada ? EstadoHabitacion.RESERVADA
            : estado == EstadoReserva.EnCurso ? EstadoHabitacion.OCUPADO : EstadoHabitacion.DISPONIBLE;
        db.Habitaciones.Add(new Habitacion { NumeroHabitacion = n, Piso = 1, IdTipo = idTipo, Estado = hab });
        var cliente = new Cliente
        {
            TipoDocumento = TipoDocumento.CC, Documento = (++_doc).ToString(), Nombre = "Carlos Mendoza",
            Telefono = "3101234567", Correo = "carlos.mendoza@email.com",
        };
        var r = new Reserva { Cliente = cliente, NumeroHabitacion = n, Huespedes = 2, Estado = estado, FechaInicio = fechaInicio.Date, FechaFin = fechaInicio.Date.AddDays(noches) };
        db.Reservas.Add(r);
        db.SaveChanges();
        return r.IdReserva;
    }

    public T Leer<T>(Func<OasisStayDbContext, T> f)
    {
        using var db = Banco.Contexto();
        return f(db);
    }

    public EstadoHabitacion EstadoHabitacionDe(int idReserva)
    {
        using var db = Banco.Contexto();
        var numero = db.Reservas.Single(r => r.IdReserva == idReserva).NumeroHabitacion;
        return db.Habitaciones.Single(h => h.NumeroHabitacion == numero).Estado;
    }

    public Reserva Reserva(int id) => Leer(db => db.Reservas.Single(r => r.IdReserva == id));

    public static readonly DateTime CheckInBase = new(2026, 11, 25);
}
