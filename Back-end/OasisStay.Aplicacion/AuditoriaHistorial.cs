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
}

/// <summary>[DS3] ObtenerRegistros(filtros) → RepositorioHistorial.BuscarEventos(filtros). Solo lectura: la bitácora es de solo inserción [JIRA US-07 RN2].</summary>
public class AuditoriaHistorial : IAuditoriaHistorial
{
    private readonly IRepositorioHistorial _repositorio;
    public AuditoriaHistorial(IRepositorioHistorial repositorio) => _repositorio = repositorio;

    public IReadOnlyList<HistorialReserva> ObtenerRegistros(FiltrosHistorial filtros)
    {
        if (filtros.Desde.HasValue && filtros.Hasta.HasValue && filtros.Desde.Value.Date > filtros.Hasta.Value.Date)
            throw new ValidacionDominioException("La fecha inicial no puede ser posterior a la fecha final.");
        return _repositorio.BuscarEventos(filtros);
    }
}
