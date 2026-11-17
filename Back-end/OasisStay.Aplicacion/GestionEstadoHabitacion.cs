using OasisStay.Dominio;

namespace OasisStay.Aplicacion;

/// <summary>Tipos de habitación; el alta toma el valor por noche del tipo [CTR §2 TipoHabitacion][D-28].</summary>
public interface IRepositorioTiposHabitacion
{
    IReadOnlyList<TipoHabitacion> ConsultarTodos();
    TipoHabitacion? BuscarPorId(int idTipo);
}

/// <summary>Desenlaces de un cambio de estado manual [DS2][JIRA US-02][D-34].</summary>
public enum DesenlaceCambioEstado { Exitoso, NoExiste, DatosInvalidos, Rechazado, SinPermiso }

/// <summary>Resultado de <see cref="GestionEstadoHabitacion.CambiarEstado"/> [DS2][JIRA US-02].</summary>
public sealed record ResultadoCambioEstado(DesenlaceCambioEstado Desenlace, Habitacion? Habitacion, string? Mensaje);

/// <summary>Desenlaces del alta de habitación [D-16][actividad-1][D-28].</summary>
public enum DesenlaceAlta { Creada, DatosInvalidos, NumeroRepetido }

/// <summary>Resultado de <see cref="GestionEstadoHabitacion.CrearHabitacion"/> [D-16][actividad-1].</summary>
public sealed record ResultadoAlta(DesenlaceAlta Desenlace, Habitacion? Habitacion, string? Mensaje);

/// <summary>[CTR §5][DS2][D-16]</summary>
public interface IGestionEstadoHabitacion
{
    IReadOnlyList<Habitacion> ObtenerHabitaciones(EstadoHabitacion? estado = null);
    ResultadoCambioEstado CambiarEstado(int numero, EstadoHabitacion nuevoEstado, string? motivo, string? rolSolicitante = null);
    ResultadoAlta CrearHabitacion(int numero, int idTipo, int piso);
    IReadOnlyList<TipoHabitacion> ObtenerTipos();
}

/// <summary>Cambia y consulta el estado operativo de las habitaciones con State [CTR §5][DS2][E1 RF-02].</summary>
public class GestionEstadoHabitacion : IGestionEstadoHabitacion
{
    public const string RolRecepcionista = "Recepcionista";
    public const string MensajeAltaIncompleta = "Completa todos los datos de la habitación.";
    public const string MensajeNumeroRepetido = "Ya existe una habitación con ese número.";
    public const string MensajeNoExiste = "La habitación no existe.";
    public const string MensajeSoloAmaDeLlaves = "Solo el ama de llaves puede marcar la habitación como disponible tras la limpieza";

    private readonly IRepositorioHabitaciones _habitaciones;
    private readonly IRepositorioTiposHabitacion _tipos;
    private readonly IUnidadDeTrabajo _unidad;

    public GestionEstadoHabitacion(IRepositorioHabitaciones habitaciones, IRepositorioTiposHabitacion tipos, IUnidadDeTrabajo unidad)
    {
        _habitaciones = habitaciones;
        _tipos = tipos;
        _unidad = unidad;
    }

    /// <summary>[DS2 obtenerHabitaciones][actividad-2]; el filtro por estado se aplica aquí [E2 §4 US-01].</summary>
    public IReadOnlyList<Habitacion> ObtenerHabitaciones(EstadoHabitacion? estado = null)
    {
        var todas = _habitaciones.ConsultarTodas();
        foreach (var h in todas) h.ReconstruirEstado();
        return estado is null ? todas : todas.Where(h => h.Estado == estado).ToList();
    }

    /// <summary>
    /// Solo acciones manuales: Disponible y Mantenimiento; el resto ocurre por reserva, check-in y check-out
    /// [DS2][DE#1 §4 Eventos]. EnLimpieza a Disponible solo lo hace el ama de llaves (o el Administrador) [DE#1 §5][D-34].
    /// </summary>
    public ResultadoCambioEstado CambiarEstado(int numero, EstadoHabitacion nuevoEstado, string? motivo, string? rolSolicitante = null)
    {
        var h = _habitaciones.BuscarPorNumero(numero);
        if (h is null) return new(DesenlaceCambioEstado.NoExiste, null, MensajeNoExiste);
        h.ReconstruirEstado();

        if (nuevoEstado == EstadoHabitacion.MANTENIMIENTO && !EstadosHabitacion.MotivoValido(motivo))
            return new(DesenlaceCambioEstado.DatosInvalidos, h, EstadosHabitacion.MotivoMantenimiento);

        if (nuevoEstado == EstadoHabitacion.DISPONIBLE && h.Estado == EstadoHabitacion.EN_LIMPIEZA
            && rolSolicitante == RolRecepcionista)
            return new(DesenlaceCambioEstado.SinPermiso, h, MensajeSoloAmaDeLlaves);

        var r = nuevoEstado switch
        {
            EstadoHabitacion.DISPONIBLE => h.MarcarDisponible(),
            EstadoHabitacion.MANTENIMIENTO => h.MarcarMantenimiento(motivo),
            _ => ResultadoOperacion.Rechazada(EstadosHabitacion.NoPermitida(h.estadoActual!.Nombre)),
        };
        if (!r.EsExitosa) return new(DesenlaceCambioEstado.Rechazado, h, r.Motivo);

        using var tx = _unidad.Iniciar();
        _habitaciones.Guardar(h);
        tx.Confirmar();
        return new(DesenlaceCambioEstado.Exitoso, h, null);
    }

    /// <summary>Alta por el Administrador; valor por noche = PrecioBase del tipo [D-16][actividad-1][D-28].</summary>
    public ResultadoAlta CrearHabitacion(int numero, int idTipo, int piso)
    {
        var tipo = _tipos.BuscarPorId(idTipo);
        if (numero <= 0 || piso < 1 || piso > 3 || tipo is null)
            return new(DesenlaceAlta.DatosInvalidos, null, MensajeAltaIncompleta);
        if (_habitaciones.BuscarPorNumero(numero) is not null)
            return new(DesenlaceAlta.NumeroRepetido, null, MensajeNumeroRepetido);

        var h = new Habitacion { NumeroHabitacion = numero, Piso = piso, IdTipo = tipo.IdTipo, Tipo = tipo, Estado = EstadoHabitacion.DISPONIBLE };
        h.ReconstruirEstado();
        _habitaciones.Guardar(h);
        return new(DesenlaceAlta.Creada, h, null);
    }

    /// <summary>[CTR §2 TipoHabitacion]</summary>
    public IReadOnlyList<TipoHabitacion> ObtenerTipos() => _tipos.ConsultarTodos();
}
