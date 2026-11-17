namespace OasisStay.Dominio;

/// <summary>Resultado de una operación de estado [GoF State].</summary>
public sealed class ResultadoOperacion
{
    public bool EsExitosa { get; }
    public string? Motivo { get; }
    private ResultadoOperacion(bool ok, string? motivo) { EsExitosa = ok; Motivo = motivo; }
    public static ResultadoOperacion Exitosa() => new(true, null);
    public static ResultadoOperacion Rechazada(string motivo) => new(false, motivo);
}

/// <summary>Estado de la habitación [GoF State]. Las 5 clases concretas son de F1.</summary>
public interface IEstadoHabitacion
{
    string Nombre { get; }
    ResultadoOperacion Reservar(Habitacion h);
    ResultadoOperacion HacerCheckIn(Habitacion h);
    ResultadoOperacion HacerCheckOut(Habitacion h);
    ResultadoOperacion MarcarDisponible(Habitacion h);
}
