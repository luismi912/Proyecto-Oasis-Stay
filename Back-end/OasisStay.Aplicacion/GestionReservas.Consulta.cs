using OasisStay.Dominio;

namespace OasisStay.Aplicacion;

/// <summary>Respuesta de la consulta de disponibilidad con sugerencias [JIRA US-01 ESC1/ESC2][D-35].</summary>
public sealed record DisponibilidadResultado(
    IReadOnlyList<Habitacion> Habitaciones, string? Mensaje, IReadOnlyList<TipoHabitacion> Alternativas);

/// <summary>Consulta de disponibilidad con alternativas [DS1][JIRA US-01].</summary>
public interface IConsultaDisponibilidad
{
    DisponibilidadResultado ConsultarConAlternativas(DateTime fechaInicio, DateTime fechaFin, string? nombreTipo, int numHuespedes);
}

public partial class GestionReservas : IConsultaDisponibilidad
{
    public const string MensajeFechasInvalidas = "La fecha de salida debe ser posterior a la fecha de llegada";
    public const string MensajeSinDisponibilidad = "No hay habitaciones disponibles para ese criterio. Pruebe otras fechas u otro tipo de habitación.";

    /// <summary>Solo habitaciones Disponibles, con capacidad suficiente y sin reserva solapada [D-35][JIRA US-01 RN1-RN3].</summary>
    /// <exception cref="ArgumentException">Salida no posterior a la llegada [D-19][JIRA US-01 RN2].</exception>
    public IReadOnlyList<Habitacion> ConsultarDisponibilidad(DateTime fechaInicio, DateTime fechaFin, TipoHabitacion? tipo, int numHuespedes)
    {
        if (fechaFin.Date <= fechaInicio.Date) throw new ArgumentException(MensajeFechasInvalidas);
        return _habitaciones.BuscarLibres(fechaInicio, fechaFin, tipo, numHuespedes);
    }

    /// <summary>Sin cupo: lista vacía, mensaje y tipos alternativos con cupo en el mismo rango [JIRA US-01 ESC2][D-32].</summary>
    public DisponibilidadResultado ConsultarConAlternativas(DateTime fechaInicio, DateTime fechaFin, string? nombreTipo, int numHuespedes)
    {
        var tipos = _habitaciones.ConsultarTodas().Where(h => h.Tipo is not null).Select(h => h.Tipo!)
            .DistinctBy(t => t.IdTipo).OrderBy(t => t.IdTipo).ToList();

        TipoHabitacion? tipo = null;
        if (!string.IsNullOrWhiteSpace(nombreTipo))
        {
            tipo = tipos.FirstOrDefault(t => t.Nombre.Equals(nombreTipo.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? tipos.FirstOrDefault(t => t.Nombre.StartsWith(nombreTipo.Trim(), StringComparison.OrdinalIgnoreCase));
            if (tipo is null)
                return new DisponibilidadResultado(Array.Empty<Habitacion>(), MensajeSinDisponibilidad, Array.Empty<TipoHabitacion>());
        }

        var libres = ConsultarDisponibilidad(fechaInicio, fechaFin, tipo, numHuespedes);
        if (libres.Count > 0) return new DisponibilidadResultado(libres, null, Array.Empty<TipoHabitacion>());

        var alternativas = tipos.Where(t => tipo is null || t.IdTipo != tipo.IdTipo)
            .Where(t => ConsultarDisponibilidad(fechaInicio, fechaFin, t, numHuespedes).Count > 0).ToList();
        return new DisponibilidadResultado(libres, MensajeSinDisponibilidad, alternativas);
    }
}
