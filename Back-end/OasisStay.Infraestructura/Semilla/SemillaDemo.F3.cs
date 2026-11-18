using Microsoft.EntityFrameworkCore;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;

namespace OasisStay.Infraestructura.Semilla;

/// <summary>
/// Datos de los escenarios de Jira de F3, idempotentes [D-39][D-41]. Cada fila es una reserva EnCurso en habitación Ocupada:
/// 208 (#RS-1025 Ana Gómez, total y pagado $350.000: US-06 ESC1/ESC3), 209 (#RS-1026, saldo $80.000 con minibar y late check-out: US-06 ESC2),
/// 210/211/212 (deudas $120.000, $250.000 y $150.000: US-10 ESC1-ESC3, con un abono previo) y 213/214 (US-05: 3 noches a $150.000 y
/// 1 noche a $200.000 con minibar $30.000 y late check-out; los tipos "demo US-05" existen solo porque Jira usa esas tarifas).
/// </summary>
public class SemillaDemoF3 : ISemillaDemo
{
    private record Escenario(string Historia, int Habitacion, string Tipo, int Noches, string Huesped, string Documento,
        decimal Minibar, bool Late, decimal Abono, MetodoPago Metodo);

    private static readonly Escenario[] Lista =
    {
        new("US06", 208, "Doble Clásica", 1, "Ana Gómez", "1025208", 109117.65m, false, 350000m, MetodoPago.TARJETA_CREDITO),
        new("US06", 209, "Doble Clásica", 1, "Pedro Salazar", "1026209", 40000m, true, 235350m, MetodoPago.EFECTIVO),
        new("US10", 210, "Individual", 1, "Tomás Restrepo", "1010210", 0m, false, 22800m, MetodoPago.EFECTIVO),
        new("US10", 211, "Suite Ejecutiva", 1, "Laura Castillo", "1010211", 0m, false, 130800m, MetodoPago.EFECTIVO),
        new("US10", 212, "Doble Clásica", 2, "Mariana Gómez", "1010212", 0m, false, 290300m, MetodoPago.EFECTIVO),
        new("US10", 215, "Individual", 1, "Sofía Mejía", "1010215", 0m, false, 22800m, MetodoPago.EFECTIVO),
        new("US05", 213, "Estándar demo US-05", 3, "Valentina Ríos", "1005213", 0m, false, 0m, MetodoPago.EFECTIVO),
        new("US05", 214, "Superior demo US-05", 1, "Camilo Herrera", "1005214", 30000m, true, 0m, MetodoPago.EFECTIVO),
    };

    public IReadOnlyCollection<string> Escenarios => new[] { "US05", "US06", "US10" };

    /// <summary>
    /// US05: 213 (3 noches a $150.000, ampliable a 5 en la cotización) y 214 (1 noche $200.000 + minibar $30.000 + late $40.000).
    /// US06: 208 (#RS-1025 Ana Gómez, total y pagado $350.000; ESC3 check-out de la 208) y 209 (#RS-1026, saldo $80.000).
    /// US10: deuda $120.000 (210 efectivo con $150.000 recibidos; 215 mixto 50.000 TC aprob. 4892 + 70.000 TRANSFERENCIA ref TRF-9921),
    /// $250.000 (211, tarjeta) y $150.000 (212, pago de $100.000 o sin referencia) [D-39][D-41].
    /// </summary>
    public Task SembrarEscenarioAsync(OasisStayDbContext db, string escenario) => Sembrar(db, escenario);

    public Task SembrarAsync(OasisStayDbContext db) => Sembrar(db, null);

    private async Task Sembrar(OasisStayDbContext db, string? historia)
    {
        foreach (var (nombre, capacidad, precio) in new[] { ("Estándar demo US-05", 2, 150000m), ("Superior demo US-05", 2, 200000m) })
            if (!await db.TiposHabitacion.AnyAsync(t => t.Nombre == nombre))
                db.TiposHabitacion.Add(new TipoHabitacion { Nombre = nombre, CapacidadMax = capacidad, PrecioBase = precio, Descripcion = "Tarifa de ejemplo de Jira US-05" });
        await db.SaveChangesAsync();

        var minibar = await db.Servicios.FirstOrDefaultAsync(s => s.Nombre == "Minibar");
        var hoy = DateTime.Today;
        foreach (var e in Lista.Where(x => historia == null || x.Historia == historia))
        {
            if (await db.Reservas.AnyAsync(r => r.NumeroHabitacion == e.Habitacion)) continue;
            var tipo = await db.TiposHabitacion.FirstAsync(t => t.Nombre == e.Tipo);
            var hab = await db.Habitaciones.FindAsync(e.Habitacion);
            if (hab == null) db.Habitaciones.Add(new Habitacion { NumeroHabitacion = e.Habitacion, Piso = e.Habitacion / 100, IdTipo = tipo.IdTipo, Estado = EstadoHabitacion.OCUPADO });
            else hab.Estado = EstadoHabitacion.OCUPADO;

            var cliente = new Cliente
            {
                TipoDocumento = TipoDocumento.CC, Documento = e.Documento, Nombre = e.Huesped,
                Telefono = "3100000000", Correo = $"{e.Documento}@demo.oasisstay.co",
            };
            var reserva = new Reserva
            {
                Cliente = cliente, NumeroHabitacion = e.Habitacion, Huespedes = 1, Estado = EstadoReserva.EnCurso,
                FechaInicio = hoy.AddDays(-e.Noches), FechaFin = hoy,
            };
            db.Reservas.Add(reserva);
            await db.SaveChangesAsync();

            if (e.Minibar > 0)
                db.ConsumosReserva.Add(new ConsumoReserva { IdReserva = reserva.IdReserva, IdServicio = minibar?.IdServicio, Concepto = "Minibar", Cantidad = 1, ValorUnitario = e.Minibar, Fecha = hoy });
            if (e.Late)
                db.ConsumosReserva.Add(new ConsumoReserva { IdReserva = reserva.IdReserva, Concepto = ReglasFacturacion.ConceptoLateCheckout, Cantidad = 1, ValorUnitario = ReglasFacturacion.RecargoLateCheckout, Fecha = hoy });
            if (e.Abono > 0)
                db.Pagos.Add(new Pago
                {
                    IdReserva = reserva.IdReserva, FechaPago = hoy, Monto = e.Abono, MetodoPago = e.Metodo, Vuelto = e.Metodo == MetodoPago.EFECTIVO ? 0m : null,
                    TipoTarjeta = e.Metodo == MetodoPago.TARJETA_CREDITO ? "Crédito" : null,
                    NumeroAutorizacion = e.Metodo == MetodoPago.TARJETA_CREDITO ? "AP-1025" : null,
                });
            await db.SaveChangesAsync();
        }
    }
}
