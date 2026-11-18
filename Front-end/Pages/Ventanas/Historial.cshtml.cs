using Front_end.Servicios.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Front_end.Pages
{
    /// <summary>Historial de reservas y bitácora de auditoría [JIRA US-07][D-37][D-34]: solo lectura, sin reglas de negocio.</summary>
    [Authorize(Roles = "Administrador")]
    public class HistorialModel : PageModel
    {
        public const string TextoSinResultados = "No hay resultados para los filtros aplicados"; // [D-37]

        private readonly ReservasApi _reservas;
        private readonly HistorialApi _historial;
        private readonly FacturacionApi _facturacion;
        public HistorialModel(ReservasApi reservas, HistorialApi historial, FacturacionApi facturacion)
        { _reservas = reservas; _historial = historial; _facturacion = facturacion; }

        [BindProperty(SupportsGet = true)] public string? Q { get; set; }
        [BindProperty(SupportsGet = true)] public string? Estado { get; set; }
        [BindProperty(SupportsGet = true)] public string? Usuario { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? Desde { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? Hasta { get; set; }

        public IReadOnlyList<ReservaListadoDto> Reservas { get; private set; } = Array.Empty<ReservaListadoDto>();
        public IReadOnlyDictionary<int, (string EstadoPago, decimal Total)> Cobros { get; private set; } = new Dictionary<int, (string, decimal)>();
        /// <summary>Eventos que cumplen usuario y fecha; null si esos filtros no se aplicaron [JIRA US-07 ESC2].</summary>
        public IReadOnlyList<EventoHistorialDto>? Eventos { get; private set; }
        public string? Error { get; private set; }
        public bool HayFiltros => !string.IsNullOrWhiteSpace(Q) || !string.IsNullOrWhiteSpace(Estado)
            || !string.IsNullOrWhiteSpace(Usuario) || Desde.HasValue || Hasta.HasValue;

        public async Task OnGetAsync()
        {
            try
            {
                var lista = await _reservas.ListarAsync(Q);
                if (!string.IsNullOrWhiteSpace(Estado)) lista = lista.Where(r => r.Estado == Estado).ToList();
                if (!string.IsNullOrWhiteSpace(Usuario) || Desde.HasValue || Hasta.HasValue)
                {
                    Eventos = await _historial.ConsultarAsync(usuario: Usuario, desde: Desde, hasta: Hasta);
                    var ids = Eventos.Select(e => e.IdReserva).ToHashSet();
                    lista = lista.Where(r => ids.Contains(r.IdReserva)).ToList();
                }
                Reservas = lista;
            }
            catch (ApiException ex) { Error = ex.Message; Eventos = Array.Empty<EventoHistorialDto>(); }
            catch (HttpRequestException) { Error = "No se pudo consultar el historial."; Eventos = Array.Empty<EventoHistorialDto>(); }

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
        }

        /// <summary>Línea de tiempo de una reserva (botón "Ver historial") [JIRA US-07 ESC1].</summary>
        public async Task<IActionResult> OnGetLineaTiempoAsync(int id)
        {
            try { return new JsonResult(await _historial.ConsultarAsync(idReserva: id)); }
            catch (ApiException ex) { return new JsonResult(new { detail = ex.Message }) { StatusCode = (int)ex.Estado }; }
            catch (HttpRequestException) { return new JsonResult(new { detail = "No se pudo consultar el historial." }) { StatusCode = 502 }; }
        }
    }
}
