namespace OasisStay.Dominio;

/// <summary>Datos de cobro [CTR §4 Strategy][D-33].</summary>
public class DatosPago
{
    public decimal MontoRecibido { get; set; }
    public string? TipoTarjeta { get; set; }
    public string? NumeroAutorizacion { get; set; }
    public string? ReferenciaBancaria { get; set; }
}

/// <summary>Resultado de una estrategia [GoF Strategy].</summary>
public sealed class ResultadoPago
{
    public bool EsAprobado { get; }
    public decimal Vuelto { get; }
    public string? Motivo { get; }
    private ResultadoPago(bool ok, decimal vuelto, string? motivo) { EsAprobado = ok; Vuelto = vuelto; Motivo = motivo; }
    public static ResultadoPago Aprobado(decimal vuelto = 0m) => new(true, vuelto, null);
    public static ResultadoPago Rechazado(string motivo) => new(false, 0m, motivo);
}

/// <summary>[GoF Strategy][CTR §4]</summary>
public interface IEstrategiaPago
{
    string Metodo { get; }
    ResultadoPago Procesar(decimal total, DatosPago datos);
}

/// <summary>Efectivo: rechaza si lo recibido no cubre el total; si sobra, calcula el vuelto [JIRA US-10 RN1][CTR §4].</summary>
public class PagoEfectivo : IEstrategiaPago
{
    public string Metodo => "Efectivo";
    public ResultadoPago Procesar(decimal total, DatosPago datos)
        => datos.MontoRecibido < total
            ? ResultadoPago.Rechazado(MensajesFacturacion.MontoInsuficiente)
            : ResultadoPago.Aprobado(datos.MontoRecibido - total);
}

/// <summary>Tarjeta de crédito o débito: exige tipo y número de autorización [JIRA US-10 RN2][E1 RN-06].</summary>
public class PagoTarjeta : IEstrategiaPago
{
    public string Metodo => "Tarjeta";
    public ResultadoPago Procesar(decimal total, DatosPago datos)
        => string.IsNullOrWhiteSpace(datos.TipoTarjeta) || string.IsNullOrWhiteSpace(datos.NumeroAutorizacion)
            ? ResultadoPago.Rechazado(MensajesFacturacion.FaltaTarjeta)
            : ResultadoPago.Aprobado();
}

/// <summary>Transferencia: exige la referencia bancaria [JIRA US-10 RN2][E1 RN-06].</summary>
public class PagoTransferencia : IEstrategiaPago
{
    public string Metodo => "Transferencia";
    public ResultadoPago Procesar(decimal total, DatosPago datos)
        => string.IsNullOrWhiteSpace(datos.ReferenciaBancaria)
            ? ResultadoPago.Rechazado(MensajesFacturacion.FaltaReferencia)
            : ResultadoPago.Aprobado();
}

/// <summary>Elige la estrategia por método [GoF Strategy][CTR §4].</summary>
public class ProcesadorPagos
{
    private readonly IEnumerable<IEstrategiaPago> _estrategias;
    public ProcesadorPagos(IEnumerable<IEstrategiaPago> estrategias) => _estrategias = estrategias;
    public ResultadoPago Cobrar(string metodo, decimal total, DatosPago datos)
        => _estrategias.Single(e => e.Metodo == metodo).Procesar(total, datos);
}
