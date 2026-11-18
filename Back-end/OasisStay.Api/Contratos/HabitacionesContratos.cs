namespace OasisStay.Api.Contratos;

/// <summary>Habitación libre en una consulta de disponibilidad [JIRA US-01 ESC1][D-19].</summary>
public class HabitacionLibreRespuesta
{
    public int Numero { get; set; }
    public string Tipo { get; set; } = "";
    public int Capacidad { get; set; }
    public decimal PrecioNoche { get; set; }
}

/// <summary>Tipo con cupo sugerido cuando no hay disponibilidad [JIRA US-01 ESC2].</summary>
public class TipoAlternativoRespuesta
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int CapacidadMax { get; set; }
    public decimal PrecioBase { get; set; }
}

public class DisponibilidadRespuesta
{
    public List<HabitacionLibreRespuesta> Habitaciones { get; set; } = new();
    public string? Mensaje { get; set; }
    public List<TipoAlternativoRespuesta> Alternativas { get; set; } = new();
}

/// <summary>Habitación del inventario [DS2 obtenerHabitaciones].</summary>
public class HabitacionRespuesta
{
    public int Numero { get; set; }
    public string Tipo { get; set; } = "";
    public int Piso { get; set; }
    public decimal PrecioNoche { get; set; }
    public string Estado { get; set; } = "";
    public string? MotivoMantenimiento { get; set; }
}

public class CambiarEstadoSolicitud
{
    public string? NuevoEstado { get; set; }
    public string? Motivo { get; set; }
}

public class CrearHabitacionSolicitud
{
    public int? Numero { get; set; }
    public int? Tipo { get; set; }
    public int? Piso { get; set; }
}

/// <summary>Reserva activa del panel lateral del mapa [JIRA US-09 RN2][DS5].</summary>
public class ReservaActivaRespuesta
{
    public int IdReserva { get; set; }
    public string Huesped { get; set; } = "";
    public DateTime FechaLlegada { get; set; }
    public DateTime FechaSalida { get; set; }
}

/// <summary>Habitación del mapa por pisos [DS5 datosMapaInteractivo][JIRA US-09].</summary>
public class DistribucionHabitacionRespuesta
{
    public int Numero { get; set; }
    public int Piso { get; set; }
    public string Tipo { get; set; } = "";
    public string Estado { get; set; } = "";
    public decimal PrecioNoche { get; set; }
    public string? MotivoMantenimiento { get; set; }
    public ReservaActivaRespuesta? Reserva { get; set; }
}
