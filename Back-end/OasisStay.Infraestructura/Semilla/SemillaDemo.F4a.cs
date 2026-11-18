using Microsoft.EntityFrameworkCore;
using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;

namespace OasisStay.Infraestructura.Semilla;

/// <summary>
/// Datos del escenario US-07 [D-39][D-41], con las fechas literales de Jira como eventos de historial [JIRA US-07 datos]:
/// reserva #RS-4011 (Id 4011) creada por "mlopez" a las 09:15 y modificada por "admin" a las 11:30 (hab. 101 a 102), y las
/// transacciones del recepcionista "jrodriguez" el 20/10/2026 (más una del 21/10 para comprobar que el filtro por fecha acota).
/// Las reservas son coherentes con sus eventos; el día de los eventos no depende del reloj, las fechas de estadía sí (para que el
/// No-Show no las toque).
/// </summary>
public class SemillaDemoF4a : ISemillaDemo
{
    private const int IdRs4011 = 4011;
    private static readonly DateTime Dia = new(2026, 10, 20);
    private readonly IReloj _reloj;
    public SemillaDemoF4a(IReloj reloj) => _reloj = reloj;

    public IReadOnlyCollection<string> Escenarios => new[] { "US07" };

    public Task SembrarEscenarioAsync(OasisStayDbContext db, string escenario) => Sembrar(db);

    public Task SembrarAsync(OasisStayDbContext db) => Sembrar(db);

    private static async Task<Reserva> Crear(OasisStayDbContext db, int? id, string doc, string nombre, int habitacion,
        EstadoReserva estado, EstadoHabitacion estadoHab, DateTime inicio)
    {
        var hab = await db.Habitaciones.FindAsync(habitacion) ?? throw new InvalidOperationException($"Falta la habitación {habitacion} de la semilla base.");
        hab.Estado = estadoHab;
        var r = new Reserva
        {
            Cliente = new Cliente
            {
                TipoDocumento = TipoDocumento.CC, Documento = doc, Nombre = nombre,
                Telefono = "3100000000", Correo = $"{doc}@demo.oasisstay.co",
            },
            NumeroHabitacion = habitacion, Huespedes = 1, Estado = estado, FechaInicio = inicio.Date, FechaFin = inicio.Date.AddDays(2),
        };
        if (id.HasValue) r.IdReserva = id.Value;
        db.Reservas.Add(r);
        await db.SaveChangesAsync();
        return r;
    }

    private static HistorialReserva Evento(int idReserva, DateTime fecha, string usuario, string accion, string? anterior, string? nuevo) => new()
    {
        IdReserva = idReserva, FechaRegistro = fecha, Usuario = usuario, AccionRealizada = accion, ValorAnterior = anterior, ValorNuevo = nuevo,
    };

    private async Task Sembrar(OasisStayDbContext db)
    {
        if (await db.Clientes.AnyAsync(c => c.Documento == "1004011")) return; // idempotente [D-39]

        var futuro = _reloj.Ahora.Date.AddDays(10);
        int? id = IdRs4011;
        if (db.Database.IsSqlServer())
        {
            // La identidad arranca en 4011 para que la reserva sea literalmente #RS-4011; solo si aún no hay ids mayores
            id = null;
            var max = await db.Reservas.Select(r => (int?)r.IdReserva).MaxAsync() ?? 0;
            if (max < IdRs4011)
            {
                // Si la tabla nunca tuvo filas, el siguiente valor es el que se fija; si ya tuvo, es el siguiente al fijado
                var ultimo = (await db.Database.SqlQueryRaw<long?>(
                    "SELECT CAST(last_value AS bigint) AS Value FROM sys.identity_columns WHERE object_id = OBJECT_ID('dbo.Reservas')").ToListAsync()).SingleOrDefault();
                await db.Database.ExecuteSqlRawAsync(ultimo == null ? "DBCC CHECKIDENT ('dbo.Reservas', RESEED, 4011)" : "DBCC CHECKIDENT ('dbo.Reservas', RESEED, 4010)");
            }
        }
        var rs = await Crear(db, id, "1004011", "Marta Quintero", 102, EstadoReserva.Confirmada, EstadoHabitacion.RESERVADA, futuro);
        var a = await Crear(db, null, "1004012", "Andrés Londoño", 201, EstadoReserva.EnCurso, EstadoHabitacion.OCUPADO, Dia);
        var b = await Crear(db, null, "1004013", "Paula Giraldo", 203, EstadoReserva.Confirmada, EstadoHabitacion.RESERVADA, futuro);

        db.HistorialReservas.AddRange(
            Evento(rs.IdReserva, Dia.AddHours(9).AddMinutes(15), "mlopez", "Reserva creada", null, "Hab. 101 · Confirmada"),
            Evento(rs.IdReserva, Dia.AddHours(11).AddMinutes(30), "admin", "Habitación reasignada", "Hab. 101", "Hab. 102"),
            Evento(a.IdReserva, Dia.AddHours(8).AddMinutes(40), "jrodriguez", "Reserva creada", null, "Hab. 201 · Confirmada"),
            Evento(a.IdReserva, Dia.AddHours(15).AddMinutes(5), "jrodriguez", "Check-in", "Confirmada", "EnCurso"),
            Evento(b.IdReserva, Dia.AddHours(10).AddMinutes(20), "jrodriguez", "Reserva creada", null, "Hab. 203 · Confirmada"),
            Evento(b.IdReserva, Dia.AddDays(1).AddHours(9), "jrodriguez", "Habitación reasignada", "Hab. 204", "Hab. 203"));
        await db.SaveChangesAsync();
    }
}
