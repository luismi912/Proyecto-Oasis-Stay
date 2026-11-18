using OasisStay.Dominio;

namespace OasisStay.Aplicacion;

/// <summary>Línea de consumo del resumen de cobro [DS4 calcularTotales].</summary>
public record LineaConsumo(string Concepto, int Cantidad, decimal ValorUnitario, decimal SubTotal);

/// <summary>Abono registrado [D-33][JIRA US-10 RN3].</summary>
public record LineaPago(int IdPago, DateTime Fecha, string Metodo, decimal Monto, decimal? Vuelto);

/// <summary>Resumen de cobro de una reserva: consumos, subtotal, IVA, total, pagado y saldo [DS4 calcularTotales][E1 RF-05].</summary>
public record ResumenCobro(int IdReserva, string Huesped, int NumeroHabitacion, string TipoHabitacion,
    DateTime FechaInicio, DateTime FechaFin, string EstadoReserva, int Noches, decimal Tarifa,
    IReadOnlyList<LineaConsumo> Consumos, bool SalidaTardia, bool SalidaTardiaSugerida,
    decimal Subtotal, decimal Iva, decimal Total, decimal Pagado, decimal Saldo,
    IReadOnlyList<LineaPago> Pagos, int? IdFactura);

/// <summary>Consumo enviado en una cotización [JIRA US-05 ESC2].</summary>
public record ConsumoCotizado(string Concepto, int Cantidad, decimal Monto);

/// <summary>Entrada de la cotización: habitación o tipo, fechas, consumos y salida tardía [JIRA US-05].</summary>
public record SolicitudCotizacion(int? NumeroHabitacion, int? IdTipo, DateTime FechaInicio, DateTime FechaFin,
    IReadOnlyList<ConsumoCotizado>? Consumos, bool SalidaTardia);

/// <summary>Desglose de la cotización [JIRA US-05 ESC1].</summary>
public record CotizacionResultado(int Noches, decimal Tarifa, decimal Subtotal, decimal Iva, decimal Total);

/// <summary>Fila de "Check-outs pendientes" de la pantalla Facturación [E2 §6].</summary>
public record PendienteCobro(int IdReserva, string Huesped, int NumeroHabitacion, DateTime FechaFin, string EstadoPago, decimal Total, decimal Saldo);

/// <summary>Recibo de caja digital del abono [JIRA US-10 RN3][D-24][D-33].</summary>
public record ReciboPago(int IdPago, DateTime Fecha, decimal Monto, string Metodo, decimal? Vuelto, decimal SaldoRestante);

/// <summary>Resultado de <see cref="ICheckoutFacade.ProcesarSalida"/> [CTR §4 Facade].</summary>
public sealed class ResultadoCheckout
{
    public Factura Factura { get; }
    private ResultadoCheckout(Factura factura) => Factura = factura;
    public static ResultadoCheckout Exitoso(Factura factura) => new(factura);
}

/// <summary>Genera el PDF de una factura ya cerrada; implementado en infraestructura con QuestPDF [CTR §5][D-04].</summary>
public interface IGeneradorFacturaPdf
{
    byte[] Generar(Factura factura, string huesped, int numeroHabitacion);
}

/// <summary>Cálculo de cuentas, cierre y PDF [CTR §5 ServicioFacturacion][DS4].</summary>
public interface IServicioFacturacion
{
    ResumenCobro CalcularTotales(int idReserva);
    CotizacionResultado Cotizar(SolicitudCotizacion solicitud);
    IReadOnlyList<PendienteCobro> ConsultarPendientes();
    ConsumoReserva AgregarConsumo(int idReserva, string concepto, int cantidad, decimal valorUnitario);
    Factura CerrarCuenta(int idReserva, string? metodoPago, DatosPago datos);
    byte[] ObtenerPdf(int idFactura);
}

/// <summary>Registro de abonos con su estrategia [CTR §5 GestionPagos][D-13][D-33].</summary>
public interface IGestionPagos
{
    ReciboPago RegistrarPago(int idReserva, string metodoPago, decimal monto, DatosPago datos);
}

/// <summary>Cambios de estado de habitación que dispara el check-out [CTR §4 Facade][DS4].</summary>
public interface IServicioHabitacion
{
    void MarcarEnLimpieza(int numeroHabitacion);
}

/// <summary>[CTR §4 Facade][D-10]</summary>
public interface ICheckoutFacade
{
    ResultadoCheckout ProcesarSalida(int idReserva, string metodoPago, DatosPago datos);
}

