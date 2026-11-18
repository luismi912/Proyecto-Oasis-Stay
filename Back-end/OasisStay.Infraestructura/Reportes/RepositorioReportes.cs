using Microsoft.EntityFrameworkCore;
using OasisStay.Aplicacion;
using OasisStay.Dominio;
using OasisStay.Infraestructura.Persistencia;

namespace OasisStay.Infraestructura.Reportes;

/// <summary>Consultas de solo lectura para los reportes: solo facturas y recibos cerrados [JIRA US-11 supuesto/RN2][DS3][D-46].</summary>
public class RepositorioReportes : IRepositorioReportes
{
    private readonly OasisStayDbContext _db;
    public RepositorioReportes(OasisStayDbContext db) => _db = db;

    private static (DateTime Inicio, DateTime Fin) Rango(DateTime desde, DateTime hasta) => (desde.Date, hasta.Date.AddDays(1));

    public IReadOnlyList<FilaReporte> FacturasCerradas(DateTime desde, DateTime hasta)
    {
        var (i, f) = Rango(desde, hasta);
        var facturas = _db.Facturas.AsNoTracking().Include(x => x.Detalles)
            .Where(x => x.FechaEmision >= i && x.FechaEmision < f)
            .OrderBy(x => x.FechaEmision).ThenBy(x => x.Folio).ToList();
        var metodos = (from p in _db.Pagos.AsNoTracking()
                       join x in _db.Facturas on p.IdFactura equals x.IdFactura
                       where x.FechaEmision >= i && x.FechaEmision < f
                       select new { p.IdFactura, p.MetodoPago }).ToList()
            .GroupBy(p => p.IdFactura!.Value)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(p => p.MetodoPago.ToString()).Distinct()));
        return facturas.Select(x => new FilaReporte(x.FechaEmision, x.Folio,
            string.Join(", ", x.Detalles.Select(d => d.Concepto).Distinct()), x.ImpuestoIVA,
            metodos.GetValueOrDefault(x.IdFactura, ""), x.ValorTotal)).ToList();
    }

    public IReadOnlyList<ResumenMetodoPago> PagosPorMetodo(DateTime desde, DateTime hasta)
    {
        var (i, f) = Rango(desde, hasta);
        return (from p in _db.Pagos.AsNoTracking()
                join x in _db.Facturas on p.IdFactura equals x.IdFactura
                where x.FechaEmision >= i && x.FechaEmision < f
                select new { p.MetodoPago, p.Monto }).ToList()
            .GroupBy(p => p.MetodoPago)
            .OrderBy(g => g.Key)
            .Select(g => new ResumenMetodoPago(g.Key.ToString(), g.Sum(p => p.Monto), g.Count()))
            .ToList();
    }

    public IReadOnlyList<IngresoDia> IngresoPorDia(DateTime desde, DateTime hasta)
    {
        var (i, f) = Rango(desde, hasta);
        return _db.Facturas.AsNoTracking().Where(x => x.FechaEmision >= i && x.FechaEmision < f)
            .Select(x => new { x.FechaEmision, x.ValorTotal }).ToList()
            .GroupBy(x => x.FechaEmision.Date).OrderBy(g => g.Key)
            .Select(g => new IngresoDia(g.Key, g.Sum(x => x.ValorTotal))).ToList();
    }

    public decimal PenalidadesDelRango(DateTime desde, DateTime hasta)
    {
        var (i, f) = Rango(desde, hasta);
        return _db.Reservas.AsNoTracking()
            .Where(r => (r.Estado == EstadoReserva.Cancelada || r.Estado == EstadoReserva.NoShow) && r.FechaInicio >= i && r.FechaInicio < f)
            .Select(r => r.Penalidad).ToList().Sum();
    }

    public int NochesOcupadas(DateTime desde, DateTime hasta)
    {
        var (i, f) = Rango(desde, hasta);
        return _db.Reservas.AsNoTracking()
            .Where(r => (r.Estado == EstadoReserva.EnCurso || r.Estado == EstadoReserva.Finalizada) && r.FechaInicio < f && r.FechaFin > i)
            .Select(r => new { r.FechaInicio, r.FechaFin }).ToList()
            .Sum(r => (int)(Min(r.FechaFin.Date, f) - Max(r.FechaInicio.Date, i)).TotalDays);
    }

    public int TotalHabitaciones() => _db.Habitaciones.Count();

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
