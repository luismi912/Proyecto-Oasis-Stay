namespace OasisStay.Api.Contratos;

public class HuespedSolicitud
{
    public string TipoDocumento { get; set; } = "";
    public string Documento { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Correo { get; set; } = "";
}

public class HuespedRespuesta
{
    public string TipoDocumento { get; set; } = "";
    public string Documento { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Correo { get; set; } = "";
}

public class ReservaSolicitud : HuespedSolicitud
{
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public int Huespedes { get; set; }
    public int NumeroHabitacion { get; set; }
}

public class ReservaCreadaRespuesta
{
    public int IdReserva { get; set; }
    public string Estado { get; set; } = "";
}

public class ReservaListadoRespuesta
{
    public int IdReserva { get; set; }
    public string Cliente { get; set; } = "";
    public int Habitacion { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public int Huespedes { get; set; }
    public string Estado { get; set; } = "";
    /// <summary>Penalidad registrada por cancelación tardía o No-Show [D-44].</summary>
    public decimal Penalidad { get; set; }
    public bool PenalidadExonerada { get; set; }
}

public class CancelacionSolicitud
{
    public bool Exonerar { get; set; }
    public string? Motivo { get; set; }
}

public class CancelacionRespuesta
{
    public string Estado { get; set; } = "";
    public decimal Penalidad { get; set; }
    public bool Exonerada { get; set; }
}

public class PenalidadRespuesta
{
    public bool Gratis { get; set; }
    public decimal Penalidad { get; set; }
    public DateTime Limite { get; set; }
}

public class NoShowSolicitud
{
    public DateTime? FechaCorte { get; set; }
}

public class NoShowRespuesta
{
    public int Procesadas { get; set; }
}

public class ReasignacionSolicitud
{
    public int NumeroHabitacionNueva { get; set; }
    public bool ConfirmarUpgrade { get; set; }
}

public class CheckInSolicitud
{
    public string? Documento { get; set; }
}

public class CheckInRespuesta
{
    public string EstadoReserva { get; set; } = "";
    public string EstadoHabitacion { get; set; } = "";
}
