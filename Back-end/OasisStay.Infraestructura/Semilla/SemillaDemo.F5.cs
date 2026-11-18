using Microsoft.EntityFrameworkCore;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;

namespace OasisStay.Infraestructura.Semilla;

/// <summary>
/// Escenario US09 de la semilla demo [D-39][D-41][JIRA US-09 datos]: 30 habitaciones en los pisos 1 a 3 (101-110, 201-210, 301-310) con
/// 10 Disponible, 15 Ocupada, 3 En limpieza y 2 Mantenimiento; la 302 Ocupada con huésped y fecha de salida (ESC2); el piso 2 tiene
/// libres (ESC3). Cada Ocupada lleva su reserva En curso. Sin escenario no hace nada.
/// </summary>
public class SemillaDemoF5 : ISemillaDemo
{
    private static readonly int[] OcupadasP1 = { 101, 102, 103, 104, 105 };
    private static readonly int[] OcupadasP2 = { 202, 204, 206, 208, 209 };
    private static readonly int[] OcupadasP3 = { 302, 303, 304, 305, 306 };
    private static readonly int[] EnLimpieza = { 109, 210, 309 };
    private static readonly int[] Mantenimiento = { 110, 310 };

    // Salen hoy: alimentan "Actividad de hoy" [D-45]; la 302 sale en dos noches.
    private static readonly int[] SalenHoy = { 101, 202, 305 };

    private static readonly string[] Nombres =
    {
        "Laura Castillo", "Tomás Restrepo", "Valentina Ríos", "Camilo Herrera", "Ana Gómez", "Pedro Salazar", "Mariana Gómez", "Sofía Mejía",
        "Andrea Moreno", "Sebastián Gil", "Carlos Mendoza", "Juliana Arias", "Daniel Ortiz", "Natalia Vélez", "Felipe Cárdenas",
    };

    public IReadOnlyCollection<string> Escenarios => new[] { "US09" };

    public Task SembrarAsync(OasisStayDbContext db) => Task.CompletedTask;

    public async Task SembrarEscenarioAsync(OasisStayDbContext db, string escenario)
    {
        // El inventario de Jira son exactamente 30 habitaciones: se quitan las sembradas por otros escenarios (ya sin reservas tras el reinicio).
        var catalogo = Enumerable.Range(1, 3).SelectMany(p => Enumerable.Range(1, 10).Select(i => p * 100 + i)).ToArray();
        db.Habitaciones.RemoveRange(await db.Habitaciones.Where(h => !catalogo.Contains(h.NumeroHabitacion)).ToListAsync());
        await db.SaveChangesAsync();

        var tipos = await db.TiposHabitacion.OrderBy(t => t.IdTipo).ToListAsync();
        var ocupadas = OcupadasP1.Concat(OcupadasP2).Concat(OcupadasP3).ToArray();
        var hoy = DateTime.Today;
        var indice = 0;

        foreach (var n in Enumerable.Range(1, 3).SelectMany(p => Enumerable.Range(1, 10).Select(i => p * 100 + i)))
        {
            var h = await db.Habitaciones.FindAsync(n);
            if (h == null)
            {
                h = new Habitacion { NumeroHabitacion = n, Piso = n / 100, IdTipo = tipos[n % tipos.Count].IdTipo };
                db.Habitaciones.Add(h);
            }
            h.MotivoMantenimiento = null;
            if (ocupadas.Contains(n)) h.Estado = EstadoHabitacion.OCUPADO;
            else if (EnLimpieza.Contains(n)) h.Estado = EstadoHabitacion.EN_LIMPIEZA;
            else if (Mantenimiento.Contains(n))
            {
                h.Estado = EstadoHabitacion.MANTENIMIENTO;
                h.MotivoMantenimiento = "Reparación del aire acondicionado";
            }
            else h.Estado = EstadoHabitacion.DISPONIBLE;
        }
        await db.SaveChangesAsync();

        foreach (var n in ocupadas)
        {
            var nombre = n == 302 ? "Laura Castillo" : Nombres[(++indice) % Nombres.Length];
            var cliente = new Cliente
            {
                TipoDocumento = TipoDocumento.CC, Documento = $"1090{n}", Nombre = nombre,
                Telefono = "3100000000", Correo = $"huesped{n}@demo.oasisstay.co",
            };
            var salida = SalenHoy.Contains(n) ? hoy : hoy.AddDays(n == 302 ? 2 : 1 + n % 3);
            db.Reservas.Add(new Reserva
            {
                Cliente = cliente, NumeroHabitacion = n, Huespedes = 1, Estado = EstadoReserva.EnCurso,
                FechaInicio = hoy.AddDays(-1 - n % 2), FechaFin = salida,
            });
        }
        await db.SaveChangesAsync();
    }
}
