namespace OasisStay.Aplicacion;

/// <summary>
/// Cifras de las tarjetas de Facturación y del banner de Historial [D-48][D-45]: ingresos y facturas del mes actual, saldo por cobrar de las
/// cuentas abiertas (reservas EnCurso) y reservas completadas del mes (cada reserva finalizada cierra con una sola factura).
/// </summary>
public record IndicadoresFacturacion(decimal IngresosMes, int FacturasMes, decimal PorCobrar, int CuentasAbiertas, int ReservasCompletadasMes);

/// <summary>Calcula los indicadores con datos reales, sin comparaciones sin fuente [D-48][JIRA US-11 RN2][D-45].</summary>
public class ServicioIndicadores
{
    private readonly IRepositorioReportes _reportes;
    private readonly IServicioFacturacion _facturacion;
    private readonly IReloj _reloj;

    public ServicioIndicadores(IRepositorioReportes reportes, IServicioFacturacion facturacion, IReloj reloj)
    {
        _reportes = reportes;
        _facturacion = facturacion;
        _reloj = reloj;
    }

    public IndicadoresFacturacion Consultar()
    {
        var hoy = _reloj.Ahora.Date;
        var inicio = new DateTime(hoy.Year, hoy.Month, 1);
        var fin = inicio.AddMonths(1).AddDays(-1);
        var facturas = _reportes.FacturasCerradas(inicio, fin);
        var abiertas = _facturacion.ConsultarPendientes();
        return new IndicadoresFacturacion(facturas.Sum(f => f.Total), facturas.Count, abiertas.Sum(a => a.Saldo), abiertas.Count, facturas.Count);
    }
}
