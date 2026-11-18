namespace OasisStay.Api.Contratos;

public class ConsumoSolicitud
{
    public string Concepto { get; set; } = "";
    public int Cantidad { get; set; } = 1;
    /// <summary>Valor unitario del consumo.</summary>
    public decimal Monto { get; set; }
}

public class CotizacionSolicitud
{
    public int? NumeroHabitacion { get; set; }
    public int? Tipo { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public List<ConsumoSolicitud>? Consumos { get; set; }
    public bool SalidaTardia { get; set; }
}

public class DatosPagoSolicitud
{
    public decimal MontoRecibido { get; set; }
    public string? TipoTarjeta { get; set; }
    public string? NumeroAutorizacion { get; set; }
    public string? ReferenciaBancaria { get; set; }
}

public class PagoSolicitud
{
    public string MetodoPago { get; set; } = "";
    public decimal Monto { get; set; }
    public DatosPagoSolicitud? DatosPago { get; set; }
}

public class CheckoutSolicitud
{
    public string? MetodoPago { get; set; }
    public DatosPagoSolicitud? DatosPago { get; set; }
}

public class ConsumoRespuesta
{
    public int IdConsumo { get; set; }
    public string Concepto { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal SubTotal { get; set; }
}

public class DetalleFacturaRespuesta
{
    public string Concepto { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal SubTotal { get; set; }
}

public class FacturaRespuesta
{
    public int IdFactura { get; set; }
    public int Folio { get; set; }
    public DateTime FechaEmision { get; set; }
    public decimal ImpuestoIVA { get; set; }
    public decimal ValorTotal { get; set; }
    public int IdReserva { get; set; }
    public int IdHabitacion { get; set; }
    public List<DetalleFacturaRespuesta> Detalles { get; set; } = new();
}

public class CheckoutRespuesta
{
    public FacturaRespuesta Factura { get; set; } = new();
    public string PdfUrl { get; set; } = "";
}
