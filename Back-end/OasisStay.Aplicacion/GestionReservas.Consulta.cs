using OasisStay.Dominio;

namespace OasisStay.Aplicacion;

public partial class GestionReservas
{
    /// <summary>Cuerpo en F1 [D-35].</summary>
    public IReadOnlyList<Habitacion> ConsultarDisponibilidad(DateTime fechaInicio, DateTime fechaFin, TipoHabitacion? tipo, int numHuespedes)
        => throw new NotImplementedException("F1");
}
