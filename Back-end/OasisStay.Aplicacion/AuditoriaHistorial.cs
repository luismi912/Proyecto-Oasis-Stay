using OasisStay.Dominio;

namespace OasisStay.Aplicacion;

/// <summary>Filtros de la consulta de bitácora; todos opcionales [DS3 consultarHistorial(filtros)][JIRA US-07 ESC1/ESC2][D-37].</summary>
public class FiltrosHistorial
{
    public int? IdReserva { get; set; }
    /// <summary>Usuario responsable, comparación exacta sin distinguir mayúsculas [JIRA US-07 ESC2].</summary>
    public string? Usuario { get; set; }
    /// <summary>Día inicial, inclusive.</summary>
    public DateTime? Desde { get; set; }
    /// <summary>Día final, inclusive (hasta las 23:59:59).</summary>
    public DateTime? Hasta { get; set; }
    /// <summary>Texto libre sobre acción, valores y usuario [JIRA US-07 alcance: filtro de búsqueda].</summary>
    public string? Texto { get; set; }
}

/// <summary>Consulta de la bitácora de auditoría; no replica reglas, solo lee [CTR §5 AuditoriaHistorial][E1 RF-07].</summary>
public interface IAuditoriaHistorial
{
    IReadOnlyList<HistorialReserva> ObtenerRegistros(FiltrosHistorial filtros);
    /// <summary>Crea el archivo, deja constancia con GuardarAuditoria y lo devuelve; formato inválido o error: errorGeneracion(motivo) [DS3][D-46].</summary>
    ArchivoReporte GenerarReporte(string formato, DatosReporte datos);
}

/// <summary>[DS3] ObtenerRegistros(filtros) → RepositorioHistorial.BuscarEventos(filtros). Solo lectura: la bitácora es de solo inserción [JIRA US-07 RN2].</summary>
public class AuditoriaHistorial : IAuditoriaHistorial
{
    private readonly IRepositorioHistorial _repositorio;
    private readonly IRepositorioAuditoriaReportes? _auditoria;
    private readonly IReadOnlyList<IGeneradorReporte> _generadores = Array.Empty<IGeneradorReporte>();
    private readonly IReloj? _reloj;

    /// <summary>Solo consulta de la bitácora (sin exportación).</summary>
    public AuditoriaHistorial(IRepositorioHistorial repositorio) => _repositorio = repositorio;

    /// <summary>Consulta y exportación de reportes [DS3 GenerarReporte][D-46].</summary>
    public AuditoriaHistorial(IRepositorioHistorial repositorio, IRepositorioAuditoriaReportes auditoria,
        IEnumerable<IGeneradorReporte> generadores, IReloj reloj) : this(repositorio)
    {
        _auditoria = auditoria;
        _generadores = generadores.ToList();
        _reloj = reloj;
    }

    public static string? NormalizarFormato(string? formato)
    {
        var f = formato?.Trim().ToLowerInvariant();
        return f is "xlsx" or "pdf" ? f : null;
    }

    /// <summary>[DS3] Crea el archivo, llama a GuardarAuditoria(reporte) y lo devuelve; formato inválido o error → errorGeneracion(motivo).</summary>
    public ArchivoReporte GenerarReporte(string formato, DatosReporte datos)
    {
        var f = NormalizarFormato(formato) ?? throw new ValidacionDominioException(MensajesReportes.FormatoInvalido);
        var generador = _generadores.FirstOrDefault(g => g.Formato == f) ?? throw new ValidacionDominioException(MensajesReportes.FormatoInvalido);
        datos = datos with { Generado = _reloj!.Ahora };
        ArchivoReporte archivo;
        try { archivo = generador.Generar(datos); }
        catch (Exception ex) when (ex is not (ValidacionDominioException or ConflictoDominioException))
        {
            throw new ErrorGeneracionReporteException(ex.Message, MensajesReportes.ErrorGeneracion(ex.Message));
        }

        _auditoria!.GuardarAuditoria(new AuditoriaReporte
        {
            Usuario = datos.Usuario, FechaHora = _reloj.Ahora, Desde = datos.Desde.Date, Hasta = datos.Hasta.Date, Formato = f,
        });
        return archivo;
    }

    public IReadOnlyList<HistorialReserva> ObtenerRegistros(FiltrosHistorial filtros)
    {
        if (filtros.Desde.HasValue && filtros.Hasta.HasValue && filtros.Desde.Value.Date > filtros.Hasta.Value.Date)
            throw new ValidacionDominioException("La fecha inicial no puede ser posterior a la fecha final.");
        return _repositorio.BuscarEventos(filtros);
    }
}
