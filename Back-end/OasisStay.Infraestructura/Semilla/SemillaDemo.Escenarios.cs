using Microsoft.EntityFrameworkCore;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;

namespace OasisStay.Infraestructura.Semilla;

/// <summary>
/// Reinicio previo de --semilla-demo --escenario USnn [D-39][D-41]: borra reservas, huéspedes, historial y
/// notificaciones y devuelve las habitaciones a su estado de la semilla inicial [D-29]; las no listadas (105-107) quedan Disponibles.
/// </summary>
public static class ReinicioEscenario
{
    private static readonly Dictionary<int, EstadoHabitacion> Base = new()
    {
        [101] = EstadoHabitacion.OCUPADO, [102] = EstadoHabitacion.DISPONIBLE, [103] = EstadoHabitacion.RESERVADA, [104] = EstadoHabitacion.EN_LIMPIEZA,
        [201] = EstadoHabitacion.DISPONIBLE, [202] = EstadoHabitacion.OCUPADO, [203] = EstadoHabitacion.DISPONIBLE, [204] = EstadoHabitacion.RESERVADA,
        [301] = EstadoHabitacion.RESERVADA, [302] = EstadoHabitacion.OCUPADO, [303] = EstadoHabitacion.EN_LIMPIEZA, [304] = EstadoHabitacion.DISPONIBLE,
    };

    public static async Task ReiniciarAsync(OasisStayDbContext db)
    {
        // Las FK son Restrict: primero lo que cuelga de la reserva (F3: detalles, pagos, facturas y consumos) [D-41]
        db.DetallesFactura.RemoveRange(await db.DetallesFactura.ToListAsync());
        db.Pagos.RemoveRange(await db.Pagos.ToListAsync());
        await db.SaveChangesAsync();
        db.Facturas.RemoveRange(await db.Facturas.ToListAsync());
        db.ConsumosReserva.RemoveRange(await db.ConsumosReserva.ToListAsync());
        await db.SaveChangesAsync();
        db.HistorialReservas.RemoveRange(await db.HistorialReservas.ToListAsync());
        db.Notificaciones.RemoveRange(await db.Notificaciones.ToListAsync());
        db.Reservas.RemoveRange(await db.Reservas.ToListAsync());
        db.Clientes.RemoveRange(await db.Clientes.ToListAsync());
        await db.SaveChangesAsync();

        foreach (var h in await db.Habitaciones.ToListAsync())
            h.Estado = Base.TryGetValue(h.NumeroHabitacion, out var e) ? e : EstadoHabitacion.DISPONIBLE;
        await db.SaveChangesAsync();
    }

    /// <summary>Crea la habitación Doble Clásica si no existe y fija su estado.</summary>
    public static async Task AsegurarDobleAsync(OasisStayDbContext db, int numero, EstadoHabitacion estado)
    {
        var h = await db.Habitaciones.FindAsync(numero);
        if (h == null)
        {
            var doble = await db.TiposHabitacion.FirstAsync(t => t.Nombre == "Doble Clásica");
            h = new Habitacion { NumeroHabitacion = numero, Piso = numero / 100, IdTipo = doble.IdTipo };
            db.Habitaciones.Add(h);
        }
        h.Estado = estado;
        await db.SaveChangesAsync();
    }

    public static async Task<Cliente> AsegurarCarlosAsync(OasisStayDbContext db)
    {
        var c = await db.Clientes.FirstOrDefaultAsync(x => x.TipoDocumento == TipoDocumento.CC && x.Documento == "1020304050");
        if (c == null)
        {
            c = new Cliente { TipoDocumento = TipoDocumento.CC, Documento = "1020304050", Nombre = "Carlos Mendoza", Telefono = "3101234567", Correo = "carlos.mendoza@email.com" };
            db.Clientes.Add(c);
            await db.SaveChangesAsync();
        }
        return c;
    }
}
