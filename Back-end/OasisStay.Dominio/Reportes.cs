namespace OasisStay.Dominio;

/// <summary>Constancia de cada exportación de reporte: usuario, fecha/hora, rango y formato [DS3 GuardarAuditoria][D-46].</summary>
public class AuditoriaReporte
{
    public int IdAuditoria { get; set; }
    public string Usuario { get; set; } = "";
    public DateTime FechaHora { get; set; }
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    /// <summary>"xlsx" o "pdf" [JIRA US-11 alcance].</summary>
    public string Formato { get; set; } = "";
}

/// <summary>El rol no puede generar el reporte: solo Administrador (HTTP 403) [JIRA US-11 RN1][D-34].</summary>
public class AccesoProhibidoException : Exception
{
    public AccesoProhibidoException(string mensaje) : base(mensaje) { }
}

/// <summary>Error al crear el archivo: <c>errorGeneracion(motivo)</c> (HTTP 500) [DS3 alt].</summary>
public class ErrorGeneracionReporteException : Exception
{
    public string Motivo { get; }
    public ErrorGeneracionReporteException(string motivo, string mensaje) : base(mensaje) => Motivo = motivo;
}
