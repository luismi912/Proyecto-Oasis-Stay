using Front_end.Servicios.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Front_end.Pages
{
    [Authorize(Roles = "Administrador,Recepcionista")]
    public class ReservasModel : PageModel
    {
        private const string TextoUpgrade = "La habitación nueva es de categoría superior (Upgrade). Confirme para continuar."; // [D-32]
        private readonly ReservasApi _api;
        private readonly HabitacionesApi _habitaciones;
        private readonly FacturacionApi _facturacion;
        public ReservasModel(ReservasApi api, HabitacionesApi habitaciones, FacturacionApi facturacion)
        { _api = api; _habitaciones = habitaciones; _facturacion = facturacion; }

        /// <summary>Estado de pago y total por reserva, calculados por F3 (<c>GET /api/facturacion/{id}/totales</c>) [E2 §6].</summary>
        public IReadOnlyDictionary<int, (string EstadoPago, decimal Total)> Cobros { get; private set; } = new Dictionary<int, (string, decimal)>();

        /// <summary>Resumen del modal: subtotal, IVA y total los calcula la API [JIRA US-05 ESC3][E2 §6 SRP].</summary>
        public async Task<IActionResult> OnGetCotizacionAsync(int numeroHabitacion, DateTime fechaInicio, DateTime fechaFin)
        {
            try
            {
                var c = await _facturacion.CotizarAsync(new { numeroHabitacion, fechaInicio, fechaFin });
                return new JsonResult(c);
            }
            catch (ApiException ex)
            {
                return new JsonResult(new { detail = ex.Message }) { StatusCode = (int)ex.Estado };
            }
            catch (HttpRequestException)
            {
                return new JsonResult(new { detail = "No se pudo calcular la cotización." }) { StatusCode = 502 };
            }
        }

        /// <summary>Tipos para el filtro de categoría de la consulta de disponibilidad [JIRA US-01].</summary>
        public IReadOnlyList<TipoHabitacionDto> Tipos { get; private set; } = Array.Empty<TipoHabitacionDto>();

        [BindProperty]
        public ReservationInput Reservation { get; set; } = new();

        public IReadOnlyList<ReservaListadoDto> Reservas { get; private set; } = Array.Empty<ReservaListadoDto>();

        /// <summary>Reabre el modal cuando la API rechazó la reserva, conservando lo digitado [JIRA US-03 ESC3].</summary>
        public bool ReabrirModal { get; private set; }

        public async Task OnGetAsync() => await CargarAsync();

        /// <summary>Autocompletado por documento [JIRA US-03 ESC2]; el navegador no llama a la API directamente.</summary>
        public async Task<IActionResult> OnGetHuespedAsync(string tipoDocumento, string documento)
        {
            try
            {
                var h = await _api.BuscarHuespedAsync(tipoDocumento, documento);
                return h == null ? NotFound() : new JsonResult(h);
            }
            catch (ApiException) { return NotFound(); }
        }

        /// <summary>Paso 3 del modal: habitaciones libres reales según fechas y huéspedes [JIRA US-01 ESC1-3].</summary>
        public async Task<IActionResult> OnGetDisponibilidadAsync(DateTime fechaInicio, DateTime fechaFin, int huespedes, string? tipo = null)
        {
            try
            {
                var (estado, cuerpo) = await _api.DisponibilidadAsync(fechaInicio, fechaFin, huespedes, tipo);
                return new ContentResult { StatusCode = estado, Content = cuerpo, ContentType = "application/json" };
            }
            catch (HttpRequestException)
            {
                return new ContentResult { StatusCode = 502, ContentType = "application/json", Content = "{\"detail\":\"No se pudo consultar la disponibilidad.\"}" };
            }
        }

        public async Task<IActionResult> OnPostCreateReservationAsync()
        {
            // Solo valida formato básico; las reglas (fechas, capacidad, solapamiento) las decide la API [C4-N2].
            if (!Reservation.AcceptPrivacy)
                ModelState.AddModelError("Reservation.AcceptPrivacy", "Debe aceptar la política de privacidad.");
            if (string.IsNullOrWhiteSpace(Reservation.RoomNumber))
                ModelState.AddModelError("Reservation.RoomNumber", "Seleccione una habitación.");
            if (!ModelState.IsValid)
                return await VolverConErrorAsync("Completa todos los datos obligatorios de la reserva.");

            try
            {
                var r = await _api.CrearAsync(new
                {
                    tipoDocumento = Reservation.DocumentType,
                    documento = Reservation.Document,
                    nombre = Reservation.GuestName,
                    telefono = Reservation.Phone,
                    correo = Reservation.Email,
                    fechaInicio = Reservation.CheckIn,
                    fechaFin = Reservation.CheckOut,
                    huespedes = Reservation.Guests,
                    numeroHabitacion = int.Parse(Reservation.RoomNumber),
                });
                TempData["ReservationSuccess"] =
                    $"Reserva creada para {Reservation.GuestName}. Habitación {Reservation.RoomNumber}.";
                return RedirectToPage();
            }
            catch (ApiReservasException ex)
            {
                foreach (var (campo, mensajes) in ex.Campos)
                    foreach (var m in mensajes)
                        ModelState.AddModelError(Mapear(campo), m);
                return await VolverConErrorAsync(ex.Message);
            }
            catch (ApiException ex)
            {
                return await VolverConErrorAsync(ex.Message);
            }
        }

        /// <summary>Reasignar [JIRA US-04 ESC3][D-21]: responde JSON; el Upgrade pide confirmación con el texto D-32.</summary>
        public async Task<IActionResult> OnPostReasignarAsync(int id, int numeroHabitacionNueva, bool confirmarUpgrade)
        {
            try
            {
                await _api.ReasignarAsync(id, numeroHabitacionNueva, confirmarUpgrade);
                TempData["ReservationSuccess"] = $"Reserva {id} reasignada a la habitación {numeroHabitacionNueva}.";
                return new JsonResult(new { ok = true });
            }
            catch (ApiException ex)
            {
                return new JsonResult(new { ok = false, upgrade = !confirmarUpgrade && ex.Message == TextoUpgrade, detail = ex.Message });
            }
            catch (HttpRequestException)
            {
                return new JsonResult(new { ok = false, upgrade = false, detail = "No se pudo reasignar la habitación." });
            }
        }

        public async Task<IActionResult> OnPostCheckInAsync(int id, string? documento)
        {
            try
            {
                await _api.CheckInAsync(id, documento);
                TempData["ReservationSuccess"] = $"Check-in realizado. Reserva {id} en curso.";
            }
            catch (ApiException ex)
            {
                TempData["ReservationError"] = ex.Message;
            }
            return RedirectToPage();
        }

        private async Task<IActionResult> VolverConErrorAsync(string mensaje)
        {
            TempData["ReservationError"] = mensaje;
            ReabrirModal = true;
            await CargarAsync();
            return Page();
        }

        private async Task CargarAsync()
        {
            try { Reservas = await _api.ListarAsync(); }
            catch (ApiException ex) { TempData["ReservationError"] ??= ex.Message; }
            var cobros = new Dictionary<int, (string, decimal)>();
            foreach (var r in Reservas.Where(r => r.Estado is "Confirmada" or "EnCurso" or "Finalizada"))
            {
                try
                {
                    var t = await _facturacion.TotalesAsync(r.IdReserva);
                    cobros[r.IdReserva] = (t.Total > 0 && t.Saldo == 0 ? "Pagado" : "Pendiente", t.Total);
                }
                catch (ApiException) { /* sin dato de cobro */ }
            }
            Cobros = cobros;
            try { Tipos = await _habitaciones.TiposAsync(); }
            catch (ApiException) { /* sin filtro de categoría */ }
        }

        private static string Mapear(string campoApi) => campoApi switch
        {
            "nombre" => "Reservation.GuestName",
            "documento" => "Reservation.Document",
            "tipoDocumento" => "Reservation.DocumentType",
            "correo" => "Reservation.Email",
            "telefono" => "Reservation.Phone",
            _ => "",
        };

        public class ReservationInput
        {
            [BindProperty]
            public string GuestName { get; set; } = string.Empty;

            [BindProperty]
            public string DocumentType { get; set; } = "CC";

            [BindProperty]
            public string Document { get; set; } = string.Empty;

            [BindProperty]
            public string Email { get; set; } = string.Empty;

            [BindProperty]
            public string Phone { get; set; } = string.Empty;

            [BindProperty]
            public DateTime CheckIn { get; set; } = new(2026, 6, 12);

            [BindProperty]
            public DateTime CheckOut { get; set; } = new(2026, 6, 15);

            [BindProperty]
            public int Guests { get; set; } = 2;

            [BindProperty]
            public string RoomNumber { get; set; } = "102";

            /// <summary>Aceptación de la política de privacidad [E1 RNF-08][D-30].</summary>
            [BindProperty]
            public bool AcceptPrivacy { get; set; }
        }
    }
}
