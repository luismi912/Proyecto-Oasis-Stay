namespace OasisStay.Api.Contratos;

/// <summary>Evento de la bitácora [JIRA US-07 RN3][D-23].</summary>
public class EventoHistorialRespuesta
{
    public DateTime FechaHora { get; set; }
    public int IdReserva { get; set; }
    public string Usuario { get; set; } = "";
    public string Accion { get; set; } = "";
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
}
