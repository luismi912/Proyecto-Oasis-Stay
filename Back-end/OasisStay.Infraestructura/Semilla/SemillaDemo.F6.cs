using Microsoft.EntityFrameworkCore;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;

namespace OasisStay.Infraestructura.Semilla;

/// <summary>
/// Escenario US11 de la semilla demo [D-39][D-41][JIRA US-11 datos]: 45 facturas cerradas entre el 01/10/2026 y el 15/10/2026 por un
/// total exacto de $12.500.000, cada una con su pago (efectivo, tarjeta crédito, tarjeta débito o transferencia). Se insertan
/// directamente, sin el flujo de check-out, para llegar al total exacto. El periodo 01/01/2027 al 15/01/2027 queda sin ventas.
/// Sin escenario no hace nada.
/// </summary>
public class SemillaDemoF6 : ISemillaDemo
{
    public const int Facturas = 45;
    public const decimal TotalEsperado = 12_500_000m;
    private const int FolioInicial = 1001; // lejos de la secuencia SeqFolioFactura para no chocar con los folios reales

    private static readonly MetodoPago[] Metodos = { MetodoPago.EFECTIVO, MetodoPago.TARJETA_CREDITO, MetodoPago.TARJETA_DEBITO, MetodoPago.TRANSFERENCIA };

    public IReadOnlyCollection<string> Escenarios => new[] { "US11" };

    public Task SembrarAsync(OasisStayDbContext db) => Task.CompletedTask;

    /// <summary>45 totales variados que suman exactamente $12.500.000; el residuo del reparto va a la última factura.</summary>
    public static decimal[] Totales()
    {
        var t = Enumerable.Range(0, Facturas).Select(i => 240_000m + ((i * 7 + 3) % 13) * 5_000m).ToArray();
        var ajuste = decimal.Truncate((TotalEsperado - t.Sum()) / Facturas);
        for (var i = 0; i < t.Length; i++) t[i] += ajuste;
        t[^1] += TotalEsperado - t.Sum();
        return t;
    }

    public async Task SembrarEscenarioAsync(OasisStayDbContext db, string escenario)
    {
        var tipo = await db.TiposHabitacion.OrderBy(t => t.IdTipo).FirstAsync();
        foreach (var n in Enumerable.Range(101, 10))
            if (!await db.Habitaciones.AnyAsync(h => h.NumeroHabitacion == n))
                db.Habitaciones.Add(new Habitacion { NumeroHabitacion = n, Piso = 1, IdTipo = tipo.IdTipo, Estado = EstadoHabitacion.DISPONIBLE });
        await db.SaveChangesAsync();
        var cliente = await ReinicioEscenario.AsegurarCarlosAsync(db);

        var totales = Totales();
        for (var i = 0; i < Facturas; i++)
        {
            var emision = new DateTime(2026, 10, 1 + i / 3, 9 + i % 3 * 3, 15, 0); // 3 por día, del 01 al 15
            var neto = Math.Round(totales[i] / 1.19m, 2, MidpointRounding.AwayFromZero);
            var metodo = Metodos[i % Metodos.Length];
            var reserva = new Reserva
            {
                IdCliente = cliente.IdCliente, NumeroHabitacion = 101 + i % 10, Huespedes = 2, Estado = EstadoReserva.Finalizada,
                FechaInicio = emision.Date.AddDays(-2), FechaFin = emision.Date,
            };
            db.Reservas.Add(reserva);
            await db.SaveChangesAsync(); // la factura necesita el Id de la reserva (FK)
            var factura = new Factura
            {
                Folio = FolioInicial + i, FechaEmision = emision, ImpuestoIVA = totales[i] - neto, ValorTotal = totales[i],
                IdReserva = reserva.IdReserva, IdHabitacion = reserva.NumeroHabitacion,
            };
            factura.Detalles.Add(new DetalleFactura { Concepto = ReglasFacturacion.ConceptoEstadia, Cantidad = 2, ValorUnitario = Math.Round(neto / 2, 2), SubTotal = neto });
            db.Facturas.Add(factura);
            await db.SaveChangesAsync();
            db.Pagos.Add(new Pago
            {
                FechaPago = emision, Monto = totales[i], MetodoPago = metodo, IdReserva = reserva.IdReserva, IdFactura = factura.IdFactura,
                TipoTarjeta = metodo == MetodoPago.TARJETA_CREDITO ? "Crédito" : metodo == MetodoPago.TARJETA_DEBITO ? "Débito" : null,
                NumeroAutorizacion = metodo is MetodoPago.TARJETA_CREDITO or MetodoPago.TARJETA_DEBITO ? $"AUT{emision:MMdd}{i:D3}" : null,
                ReferenciaBancaria = metodo == MetodoPago.TRANSFERENCIA ? $"REF{emision:MMdd}{i:D3}" : null,
                Vuelto = metodo == MetodoPago.EFECTIVO ? 0m : null,
            });
            await db.SaveChangesAsync();
        }
    }
}
