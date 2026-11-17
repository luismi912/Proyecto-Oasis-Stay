using OasisStay.Dominio;

namespace OasisStay.Aplicacion;

/// <summary>Políticas configurables en appsettings "Politicas" [D-01][D-12][JIRA US-08 RN1].</summary>
public class PoliticasOpciones
{
    public int PlazoCancelacionHoras { get; set; } = 24;
    public TimeSpan HoraMaximaCheckout { get; set; } = new(12, 0, 0);
    public TimeSpan HoraCheckIn { get; set; } = new(15, 0, 0);
}

/// <summary>Reloj inyectable para pruebas de horas y fechas (hora local de Colombia).</summary>
public interface IReloj { DateTime Ahora { get; } }

/// <summary>Transacción de la unidad de trabajo [GoF Facade].</summary>
public interface ITransaccion : IDisposable { void Confirmar(); }

/// <summary>Unidad de trabajo: Iniciar() devuelve la transacción; tx.Confirmar() [GoF Facade].</summary>
public interface IUnidadDeTrabajo { ITransaccion Iniciar(); }

/// <summary>Historial de solo inserción: sin actualizar ni borrar [D-23][JIRA US-07 RN2].</summary>
public interface IRepositorioHistorial
{
    void RegistrarEvento(HistorialReserva evento);
    void Archivar(int idReserva, Factura factura);
}

/// <summary>Notificador; el adaptador simulado guarda en BD [D-04].</summary>
public interface INotificador { void Notificar(string destinatario, string asunto, string mensaje); }

/// <summary>[CTR §5]</summary>
public interface IRepositorioHabitaciones
{
    IReadOnlyList<Habitacion> BuscarLibres(DateTime fechaInicio, DateTime fechaFin, TipoHabitacion? tipo, int numHuespedes);
    IReadOnlyList<Habitacion> ConsultarTodas();
    Habitacion? BuscarPorNumero(int numero);
    void Guardar(Habitacion habitacion);
}

/// <summary>[CTR §5]</summary>
public interface IRepositorioReservas
{
    IReadOnlyList<Reserva> BuscarPorHabitacionYFechas(Habitacion habitacion, DateTime fechaInicio, DateTime fechaFin);
    void Guardar(Reserva reserva);
}

/// <summary>Huéspedes; búsqueda por documento (autocompletado) [JIRA US-03 ESC2][D-03].</summary>
public interface IRepositorioHuespedes
{
    Cliente? BuscarPorDocumento(string documento);
    void Guardar(Cliente cliente);
}

/// <summary>Consumos (servicios) [CTR §5].</summary>
public interface IRepositorioConsumos { IReadOnlyList<Servicio> ConsultarServicios(); }

/// <summary>[CTR §5]</summary>
public interface IFacturaRepository { void Guardar(Factura factura); }

/// <summary>[CTR §5]</summary>
public interface IRepositorioPagos { void Guardar(Pago pago); }

/// <summary>[CTR §5][D-09]</summary>
public interface IGestionReservas
{
    IReadOnlyList<Habitacion> ConsultarDisponibilidad(DateTime fechaInicio, DateTime fechaFin, TipoHabitacion? tipo, int numHuespedes);
    Reserva RegistrarReserva(SolicitudReserva solicitud);
}

/// <summary>Orquesta la reserva [CTR §5]. F1 llena Consulta, F2 llena Registro.</summary>
public partial class GestionReservas : IGestionReservas
{
    private readonly IRepositorioHabitaciones _habitaciones;
    private readonly IRepositorioReservas _reservas;

    public GestionReservas(IRepositorioHabitaciones habitaciones, IRepositorioReservas reservas)
    {
        _habitaciones = habitaciones;
        _reservas = reservas;
    }
}
