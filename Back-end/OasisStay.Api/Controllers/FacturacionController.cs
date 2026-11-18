using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OasisStay.Api.Contratos;
using OasisStay.Api.Filtros;
using OasisStay.Aplicacion;
using OasisStay.Dominio;

namespace OasisStay.Api.Controllers;

/// <summary>
/// Cotización, totales, consumos, pagos, check-out y PDF [DS4][E1 RF-05/RF-06/RF-10][D-10][D-33][D-34]:
/// Administrador y Recepcionista. El check-out entra por <see cref="ICheckoutFacade"/>.
/// </summary>
[ApiController]
[Authorize(Roles = "Administrador,Recepcionista")]
[ErroresDominioReservasFiltro]
public class FacturacionController : ControllerBase
{
    private readonly IServicioFacturacion _facturacion;
    private readonly IGestionPagos _pagos;
    private readonly ICheckoutFacade _checkout;

    public FacturacionController(IServicioFacturacion facturacion, IGestionPagos pagos, ICheckoutFacade checkout)
    {
        _facturacion = facturacion;
        _pagos = pagos;
        _checkout = checkout;
    }

    private static DatosPago Datos(DatosPagoSolicitud? d) => new()
    {
        MontoRecibido = d?.MontoRecibido ?? 0m, TipoTarjeta = d?.TipoTarjeta,
        NumeroAutorizacion = d?.NumeroAutorizacion, ReferenciaBancaria = d?.ReferenciaBancaria,
    };

    /// <summary>[JIRA US-05 ESC1-ESC3][DS4 calcularTotales]; fechas inválidas: 400 [D-22].</summary>
    [HttpPost("api/cotizaciones")]
    public ActionResult<CotizacionResultado> Cotizar(CotizacionSolicitud s)
        => _facturacion.Cotizar(new SolicitudCotizacion(s.NumeroHabitacion, s.Tipo, s.FechaInicio, s.FechaFin,
            s.Consumos?.Select(c => new ConsumoCotizado(c.Concepto, c.Cantidad, c.Monto)).ToList(), s.SalidaTardia));

    /// <summary>[DS4 calcularTotales] resumen de cobro con consumos, subtotal, IVA, total, pagado y saldo.</summary>
    [HttpGet("api/facturacion/{idReserva:int}/totales")]
    public ActionResult<ResumenCobro> Totales(int idReserva) => _facturacion.CalcularTotales(idReserva);

    /// <summary>"Check-outs pendientes" de la pantalla Facturación [E2 §6].</summary>
    [HttpGet("api/facturacion/pendientes")]
    public ActionResult<IReadOnlyList<PendienteCobro>> Pendientes() => Ok(_facturacion.ConsultarPendientes());

    /// <summary>[actividad-4 "Añadir consumos extra"][D-29]; 409 si la reserva no está EnCurso.</summary>
    [HttpPost("api/reservas/{id:int}/consumos")]
    public ActionResult<ConsumoRespuesta> AgregarConsumo(int id, ConsumoSolicitud s)
    {
        var c = _facturacion.AgregarConsumo(id, s.Concepto, s.Cantidad, s.Monto);
        return Created($"api/facturacion/{id}/totales", new ConsumoRespuesta
        {
            IdConsumo = c.IdConsumo, Concepto = c.Concepto, Cantidad = c.Cantidad, ValorUnitario = c.ValorUnitario, SubTotal = c.SubTotal,
        });
    }

    /// <summary>Abono con recibo [E1 RF-10][D-13][D-33]; 400 con el mensaje de la estrategia.</summary>
    [HttpPost("api/reservas/{id:int}/pagos")]
    public ActionResult<ReciboPago> RegistrarPago(int id, PagoSolicitud s)
    {
        var recibo = _pagos.RegistrarPago(id, s.MetodoPago, s.Monto, Datos(s.DatosPago));
        return Created($"api/facturacion/{id}/totales", recibo);
    }

    /// <summary>ProcesarSalida del Facade [DS4][D-10][D-33]: 409 con saldo pendiente, 400 con errorPago(motivo).</summary>
    [HttpPost("api/reservas/{id:int}/checkout")]
    public ActionResult<CheckoutRespuesta> Checkout(int id, CheckoutSolicitud s)
    {
        var f = _checkout.ProcesarSalida(id, s.MetodoPago ?? "", Datos(s.DatosPago)).Factura;
        return new CheckoutRespuesta
        {
            PdfUrl = $"/api/facturas/{f.IdFactura}/pdf",
            Factura = new FacturaRespuesta
            {
                IdFactura = f.IdFactura, Folio = f.Folio, FechaEmision = f.FechaEmision, ImpuestoIVA = f.ImpuestoIVA,
                ValorTotal = f.ValorTotal, IdReserva = f.IdReserva, IdHabitacion = f.IdHabitacion,
                Detalles = f.Detalles.Select(d => new DetalleFacturaRespuesta
                {
                    Concepto = d.Concepto, Cantidad = d.Cantidad, ValorUnitario = d.ValorUnitario, SubTotal = d.SubTotal,
                }).ToList(),
            },
        };
    }

    /// <summary>Descarga del comprobante [D-04][E1 RF-06].</summary>
    [HttpGet("api/facturas/{id:int}/pdf")]
    public IActionResult Pdf(int id) => File(_facturacion.ObtenerPdf(id), "application/pdf", $"factura-{id}.pdf");
}
