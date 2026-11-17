using Microsoft.EntityFrameworkCore;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;

namespace OasisStay.Infraestructura.Semilla;

/// <summary>
/// Datos de los escenarios de Jira de F2 [D-39], idempotente:
/// huésped CC 1020304050 "Carlos Mendoza" ya existente (US-03 ESC2, autocompletado), habitación 304 Suite Disponible
/// (US-04 ESC1, ya está en la semilla base) y las habitaciones 106 (Reservada) y 107 (Disponible) con una reserva
/// Confirmada en la 106 para la reasignación 106 → 107 (US-04 ESC3; la 105 queda Disponible para la demo de F1). El huésped nuevo de US-03 ESC1 se demuestra con otro documento.
/// </summary>
public class SemillaDemoF2 : ISemillaDemo
{
    public async Task SembrarAsync(OasisStayDbContext db)
    {
        var doble = await db.TiposHabitacion.FirstAsync(t => t.Nombre == "Doble Clásica");

        foreach (var (n, estado) in new[] { (106, EstadoHabitacion.RESERVADA), (107, EstadoHabitacion.DISPONIBLE) })
            if (!await db.Habitaciones.AnyAsync(h => h.NumeroHabitacion == n))
                db.Habitaciones.Add(new Habitacion { NumeroHabitacion = n, Piso = n / 100, IdTipo = doble.IdTipo, Estado = estado });

        var carlos = await db.Clientes.FirstOrDefaultAsync(c => c.TipoDocumento == TipoDocumento.CC && c.Documento == "1020304050");
        if (carlos == null)
        {
            carlos = new Cliente
            {
                TipoDocumento = TipoDocumento.CC, Documento = "1020304050", Nombre = "Carlos Mendoza",
                Telefono = "3101234567", Correo = "carlos.mendoza@email.com",
            };
            db.Clientes.Add(carlos);
        }
        await db.SaveChangesAsync();

        if (!await db.Reservas.AnyAsync(r => r.NumeroHabitacion == 106))
        {
            db.Reservas.Add(new Reserva
            {
                Cliente = carlos, NumeroHabitacion = 106, Huespedes = 2, Estado = EstadoReserva.Confirmada,
                FechaInicio = DateTime.Today, FechaFin = DateTime.Today.AddDays(3),
            });
            await db.SaveChangesAsync();
        }
    }
}
