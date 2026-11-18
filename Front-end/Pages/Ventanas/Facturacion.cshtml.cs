using System.Globalization;
using System.Net;
using Front_end.Servicios.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Front_end.Pages
{
    /// <summary>
    /// Facturación y check-out [DS4][D-10][D-30]: solo cliente HTTP; el cálculo, los pagos y el check-out los decide la API
    /// (<c>POST /api/reservas/{id}/checkout</c> = ProcesarSalida del Facade) [E2 §3 Facade][C4-N2].
    /// </summary>
    [Authorize(Roles = "Administrador,Recepcionista")]
    public class FacturacionModel : PageModel
    {
        private readonly FacturacionApi _api;
        public FacturacionModel(FacturacionApi api) => _api = api;

        public IReadOnlyList<PendienteDto> Pendientes { get; private set; } = Array.Empty<PendienteDto>();
        public ResumenCobroDto? Detalle { get; private set; }
        /// <summary>Cifras de las tarjetas calculadas por la API [D-48].</summary>
        public IndicadoresDto Indicadores { get; private set; } = new(0m, 0, 0m, 0, 0);
        /// <summary>Factura recién emitida (para mostrar «Factura generada con éxito» y el PDF) [DS4 mostrarComprobantePDFYExito].</summary>
        public int? FacturaId { get; private set; }
        public int? Folio { get; private set; }

        public async Task OnGetAsync(int? id, int? factura, int? folio)
        {
            FacturaId = factura;
            Folio = folio;
            await CargarAsync(id);
        }

        /// <summary>Muestra el PDF dentro de la página o lo descarga [D-04][E1 RF-06].</summary>
        public async Task<IActionResult> OnGetPdfAsync(int idFactura, bool descargar = false)
        {
            try
            {
                var pdf = await _api.PdfAsync(idFactura);
                return descargar ? File(pdf, "application/pdf", $"factura-{idFactura}.pdf") : File(pdf, "application/pdf");
            }
            catch (ApiException ex) { return StatusCode((int)ex.Estado, ex.Message); }
        }

        /// <summary>Añadir consumo [actividad-4 "Añadir consumos extra"][D-29].</summary>
        public async Task<IActionResult> OnPostConsumoAsync(int id, string concepto, int cantidad, string monto)
            => await EjecutarAsync(id, "Consumo agregado. El total se actualizó.",
                () => _api.AgregarConsumoAsync(id, concepto ?? "", cantidad, Num(monto)));

        /// <summary>Recargo de salida tardía [E1 RN-04][D-12].</summary>
        public async Task<IActionResult> OnPostSalidaTardiaAsync(int id)
            => await EjecutarAsync(id, "Salida tardía registrada: recargo de $40.000 antes del IVA.",
                () => _api.AgregarConsumoAsync(id, "Late check-out", 1, 0m));

        /// <summary>Abono con su estrategia de pago y recibo [E1 RF-10][D-13][D-33].</summary>
        public async Task<IActionResult> OnPostPagoAsync(int id, string metodoPago, string montoPago, string montoRecibido,
            string autorizacion, string referencia)
        {
            try
            {
                var r = await _api.PagarAsync(id, metodoPago ?? "", Num(montoPago), Datos(montoRecibido, autorizacion, referencia));
                TempData["FacturacionOk"] = $"Recibo de caja #{r.IdPago}: {Dinero(r.Monto)} por {r.Metodo}"
                    + (r.Vuelto is > 0 ? $", vuelto {Dinero(r.Vuelto.Value)}" : "") + $". Saldo restante: {Dinero(r.SaldoRestante)}.";
            }
            catch (ApiException ex) { TempData["FacturacionError"] = ex.Message; }
            return RedirectToPage(null, null, new { id }, "pagos");
        }

        /// <summary>Completar check-out = ProcesarSalida [DS4][D-10]; con saldo pendiente vuelve al bloque de pagos [JIRA US-06 ESC2].</summary>
        public async Task<IActionResult> OnPostCheckoutAsync(int id, string metodoPago, string montoRecibido, string autorizacion, string referencia)
        {
            try
            {
                var r = await _api.CheckoutAsync(id, metodoPago, Datos(montoRecibido, autorizacion, referencia));
                TempData["FacturacionOk"] = "Factura generada con éxito";
                return RedirectToPage(new { factura = r.Factura.IdFactura, folio = r.Factura.Folio });
            }
            catch (ApiException ex)
            {
                TempData["FacturacionError"] = ex.Message;
                return RedirectToPage(null, null, new { id }, ex.Estado == HttpStatusCode.Conflict ? "pagos" : "detalle");
            }
        }

        private async Task<IActionResult> EjecutarAsync(int id, string exito, Func<Task> accion)
        {
            try
            {
                await accion();
                TempData["FacturacionOk"] = exito;
            }
            catch (ApiException ex) { TempData["FacturacionError"] = ex.Message; }
            return RedirectToPage(null, null, new { id }, "detalle");
        }

        private async Task CargarAsync(int? id)
        {
            try
            {
                Indicadores = await _api.IndicadoresAsync();
                Pendientes = await _api.PendientesAsync();
                if (id.HasValue) Detalle = await _api.TotalesAsync(id.Value);
            }
            catch (ApiException ex) { TempData["FacturacionError"] ??= ex.Message; }
        }

        private static object Datos(string? montoRecibido, string? autorizacion, string? referencia) => new
        {
            montoRecibido = Num(montoRecibido),
            numeroAutorizacion = autorizacion,
            referenciaBancaria = referencia,
        };

        /// <summary>Convierte el texto del campo; el formato monetario es solo de presentación.</summary>
        private static decimal Num(string? texto)
            => decimal.TryParse((texto ?? "").Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;

        public static string Dinero(decimal v)
            => "$" + v.ToString(v == decimal.Truncate(v) ? "N0" : "N2", CultureInfo.GetCultureInfo("es-CO"));
    }
}
