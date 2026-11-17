using Microsoft.EntityFrameworkCore;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;

namespace OasisStay.Infraestructura.Semilla;

/// <summary>
/// Datos de los escenarios de Jira de F1, idempotente [D-39]: US-02 ESC1 (hab. 301 En limpieza),
/// ESC2 (hab. 105 Disponible) y ESC3 (hab. 204 Ocupada); US-01 ESC1 usa las Suite Ejecutiva ya sembradas.
/// </summary>
public class SemillaDemoF1 : ISemillaDemo
{
    public async Task SembrarAsync(OasisStayDbContext db)
    {
        var doble = await db.TiposHabitacion.FirstAsync(t => t.Nombre == "Doble Clásica");

        if (!await db.Habitaciones.AnyAsync(h => h.NumeroHabitacion == 105))
            db.Habitaciones.Add(new Habitacion { NumeroHabitacion = 105, Piso = 1, IdTipo = doble.IdTipo, Estado = EstadoHabitacion.DISPONIBLE });

        var h301 = await db.Habitaciones.FindAsync(301);
        // Solo si siguen en su estado base de la semilla inicial (RESERVADA) [D-29]; no pisa cambios posteriores
        if (h301 is { Estado: EstadoHabitacion.RESERVADA }) h301.Estado = EstadoHabitacion.EN_LIMPIEZA;
        var h204 = await db.Habitaciones.FindAsync(204);
        if (h204 is { Estado: EstadoHabitacion.RESERVADA }) h204.Estado = EstadoHabitacion.OCUPADO;

        await db.SaveChangesAsync();
    }
}
