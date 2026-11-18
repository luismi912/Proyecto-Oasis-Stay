using OasisStay.Dominio;

namespace OasisStay.Aplicacion;

/// <summary>Mensajes de reportes [JIRA US-11 RN1/RN3/ESC3][DS3 alt][D-46].</summary>
public static class MensajesReportes
{
    public const string SoloAdministrador = "Solo el Administrador puede generar y descargar reportes";
    public const string RangoExcedido = "El rango máximo por reporte es de 365 días";
    public const string RangoInvertido = "La fecha inicial no puede ser posterior a la fecha final";
    public const string FaltanFechas = "Indique las fechas desde y hasta del reporte";
    public const string SinMovimientos = "No hay movimientos en el periodo seleccionado: no se genera un archivo en blanco";
    public const string FormatoInvalido = "El formato del reporte no es válido: use xlsx o pdf";
    public static string ErrorGeneracion(string motivo) => $"No se pudo generar el reporte: {motivo}";
}

/// <summary>Desglose de ingresos por método de pago [JIRA US-11 alcance].</summary>
public record ResumenMetodoPago(string Metodo, decimal Monto, int Pagos);

/// <summary>
/// KPIs y consolidado previos a la descarga [JIRA US-11 UX][D-46]: bruto = con IVA, neto = sin IVA, Ticket Promedio = ingreso total ÷ facturas,
/// Ocupación % = noches ocupadas ÷ (habitaciones × días), cancelaciones = penalidades del rango.
/// </summary>
public record ResumenFinanciero(decimal IngresoTotal, decimal IngresoBruto, decimal IngresoNeto, decimal TicketPromedio,
    decimal OcupacionPorcentaje, decimal Cancelaciones, IReadOnlyList<ResumenMetodoPago> PorMetodoPago, int Facturas);

/// <summary>Fila del archivo: fecha, folio, concepto, impuestos, método de pago y total [JIRA US-11 ESC1].</summary>
public record FilaReporte(DateTime Fecha, int Folio, string Concepto, decimal Impuestos, string MetodoPago, decimal Total);

/// <summary>Ingreso bruto de un día, para el gráfico de ingresos [JIRA US-11 ESC2].</summary>
public record IngresoDia(DateTime Fecha, decimal Total);

/// <summary>Todo lo que necesitan los generadores; no calculan nada [CTR §5].</summary>
public record DatosReporte(DateTime Desde, DateTime Hasta, DateTime Generado, string Usuario, ResumenFinanciero Resumen,
    IReadOnlyList<FilaReporte> Filas, IReadOnlyList<IngresoDia> IngresoPorDia);

/// <summary>Archivo exportado [DS3 «create» reporte:Archivo].</summary>
public record ArchivoReporte(byte[] Contenido, string NombreArchivo, string TipoContenido);

/// <summary>Consultas de solo lectura sobre facturas y recibos cerrados [JIRA US-11 supuesto/RN2][D-46].</summary>
public interface IRepositorioReportes
{
    /// <summary>Facturas emitidas entre las dos fechas (inclusive).</summary>
    IReadOnlyList<FilaReporte> FacturasCerradas(DateTime desde, DateTime hasta);
    /// <summary>Total de las facturas por método de pago.</summary>
    IReadOnlyList<ResumenMetodoPago> PagosPorMetodo(DateTime desde, DateTime hasta);
    IReadOnlyList<IngresoDia> IngresoPorDia(DateTime desde, DateTime hasta);
    /// <summary>Suma de penalidades de cancelaciones y No-Show cuya llegada cae en el rango [D-44][D-46].</summary>
    decimal PenalidadesDelRango(DateTime desde, DateTime hasta);
    /// <summary>Noches de reservas EnCurso o Finalizada dentro del rango [D-46].</summary>
    int NochesOcupadas(DateTime desde, DateTime hasta);
    int TotalHabitaciones();
}

/// <summary>Constancia de cada exportación [DS3 GuardarAuditoria][D-46]; la implementa <c>RepositorioHistorial</c> [CTR §5].</summary>
public interface IRepositorioAuditoriaReportes
{
    void GuardarAuditoria(AuditoriaReporte reporte);
}

/// <summary>Un generador por formato (<c>xlsx</c> o <c>pdf</c>) [JIRA US-11 alcance][D-04][D-46].</summary>
public interface IGeneradorReporte
{
    string Formato { get; }
    ArchivoReporte Generar(DatosReporte datos);
}

/// <summary>Resumen y exportación de reportes financieros solo para el Administrador [JIRA US-11 RN1-RN3][DS3][D-46].</summary>
public class ServicioReportes
{
    public const int MaximoDias = 365;
    private readonly IRepositorioReportes _repositorio;
    private readonly AuditoriaHistorial _auditoria;

    public ServicioReportes(IRepositorioReportes repositorio, AuditoriaHistorial auditoria)
    {
        _repositorio = repositorio;
        _auditoria = auditoria;
    }

    /// <summary>Días continuos del rango, ambos extremos incluidos.</summary>
    private static int Dias(DateTime desde, DateTime hasta) => (hasta.Date - desde.Date).Days + 1;

    private static void ValidarRango(DateTime desde, DateTime hasta)
    {
        if (desde.Date > hasta.Date) throw new ValidacionDominioException(MensajesReportes.RangoInvertido);
        if (Dias(desde, hasta) > MaximoDias) throw new ValidacionDominioException(MensajesReportes.RangoExcedido); // [JIRA US-11 RN3][D-24]
    }

    /// <summary>KPIs y consolidado sin generar archivo; con rango sin movimientos devuelve <c>Facturas = 0</c> [JIRA US-11 ESC3].</summary>
    public ResumenFinanciero ResumenFinanciero(DateTime desde, DateTime hasta)
    {
        ValidarRango(desde, hasta);
        return Armar(desde, hasta, _repositorio.FacturasCerradas(desde, hasta));
    }

    private ResumenFinanciero Armar(DateTime desde, DateTime hasta, IReadOnlyList<FilaReporte> filas)
    {
        var bruto = filas.Sum(f => f.Total);
        var iva = filas.Sum(f => f.Impuestos);
        var ticket = filas.Count == 0 ? 0m : Math.Round(bruto / filas.Count, 2, MidpointRounding.AwayFromZero);
        var capacidad = (decimal)_repositorio.TotalHabitaciones() * Dias(desde, hasta);
        var ocupacion = capacidad == 0 ? 0m : Math.Round(_repositorio.NochesOcupadas(desde, hasta) / capacidad * 100m, 2, MidpointRounding.AwayFromZero);
        return new ResumenFinanciero(bruto, bruto, bruto - iva, ticket, ocupacion, _repositorio.PenalidadesDelRango(desde, hasta),
            _repositorio.PagosPorMetodo(desde, hasta), filas.Count);
    }

    /// <summary>Valida rol, formato, rango y movimientos; no genera archivos en blanco [JIRA US-11 RN1/RN3/ESC3].</summary>
    public ArchivoReporte GenerarReporte(DateTime desde, DateTime hasta, string formato, string usuario, string rol)
    {
        if (rol != "Administrador") throw new AccesoProhibidoException(MensajesReportes.SoloAdministrador);
        if (AuditoriaHistorial.NormalizarFormato(formato) == null) throw new ValidacionDominioException(MensajesReportes.FormatoInvalido);
        ValidarRango(desde, hasta);

        var filas = _repositorio.FacturasCerradas(desde, hasta);
        if (filas.Count == 0) throw new ConflictoDominioException(MensajesReportes.SinMovimientos);
        var resumen = Armar(desde, hasta, filas);
        return _auditoria.GenerarReporte(formato, new DatosReporte(desde.Date, hasta.Date, default, usuario, resumen, filas,
            _repositorio.IngresoPorDia(desde, hasta)));
    }
}
