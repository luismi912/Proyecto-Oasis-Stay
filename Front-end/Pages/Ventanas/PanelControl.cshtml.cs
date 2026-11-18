using System.Globalization;
using Front_end.Servicios.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Front_end.Pages
{
    /// <summary>
    /// Mapa gráfico por pisos [JIRA US-09][DS5]: carga la distribución al abrir y con "Actualizar"; los filtros se aplican en el cliente
    /// (site.js). Los accesos rápidos reutilizan Check-in (F2), Facturación (F3) y Cambiar estado (F1) sin reglas nuevas [D-45].
    /// </summary>
    [Authorize(Roles = "Administrador,Recepcionista")]
    public class PanelControlModel : PageModel
    {
        private readonly DistribucionApi _api;
        private readonly ReservasApi _reservas;
        private readonly HabitacionesApi _habitaciones;

        public PanelControlModel(DistribucionApi api, ReservasApi reservas, HabitacionesApi habitaciones)
        {
            _api = api;
            _reservas = reservas;
            _habitaciones = habitaciones;
        }

        public List<DistribucionDto> Habitaciones { get; private set; } = new();
        public string? Error { get; private set; }
        public DateTime Hoy { get; } = DateTime.Today;

        public string FechaActual
        {
            get
            {
                var t = Hoy.ToString("dddd, d 'de' MMMM 'de' yyyy", new CultureInfo("es-CO"));
                return char.ToUpper(t[0], CultureInfo.InvariantCulture) + t[1..];
            }
        }

        public int Total => Habitaciones.Count;
        public int Disponibles => Habitaciones.Count(h => h.Estado == "Disponible");
        public int Ocupadas => Habitaciones.Count(h => h.Estado == "Ocupada");
        public int Reservadas => Habitaciones.Count(h => h.Estado == "Reservada");
        public int EnLimpieza => Habitaciones.Count(h => h.Estado == "EnLimpieza");
        public int OcupacionPorcentaje => Total == 0 ? 0 : (int)Math.Round(100.0 * Ocupadas / Total);

        /// <summary>Check-ins de hoy: reservas Confirmadas con llegada hoy [D-45].</summary>
        public List<DistribucionDto> LlegadasHoy => Habitaciones
            .Where(h => h.Estado == "Reservada" && h.Reserva is not null && h.Reserva.FechaLlegada.Date == Hoy).ToList();

        /// <summary>Check-outs de hoy: reservas En curso con salida hoy [D-45].</summary>
        public List<DistribucionDto> SalidasHoy => Habitaciones
            .Where(h => h.Estado == "Ocupada" && h.Reserva is not null && h.Reserva.FechaSalida.Date == Hoy).ToList();

        public async Task OnGetAsync() => await CargarAsync();

        /// <summary>Acceso rápido Check-in: reutiliza POST /api/reservas/{id}/check-in [D-31].</summary>
        public async Task<IActionResult> OnPostCheckInAsync(int idReserva, int numero, string? documento)
        {
            try
            {
                await _reservas.CheckInAsync(idReserva, documento);
                TempData["MapaExito"] = $"Check-in realizado en la habitación {numero}.";
            }
            catch (ApiException ex) { TempData["MapaError"] = ex.Message; }
            return RedirectToPage();
        }

        /// <summary>Acceso rápido Cambiar estado: reutiliza PUT /api/habitaciones/{n}/estado con sus reglas [DS2].</summary>
        public async Task<IActionResult> OnPostCambiarEstadoAsync(int numero, string nuevoEstado, string? motivo)
        {
            try
            {
                await _habitaciones.CambiarEstadoAsync(numero, nuevoEstado, motivo);
                TempData["MapaExito"] = $"Habitación {numero} actualizada a {nuevoEstado}.";
            }
            catch (ApiException ex) { TempData["MapaError"] = ex.Message; }
            return RedirectToPage();
        }

        private async Task CargarAsync()
        {
            try { Habitaciones = await _api.ObtenerAsync(); }
            catch (ApiException ex) { Error = ex.Message; }
            catch (HttpRequestException) { Error = "No se pudo conectar con la API."; }
        }
    }
}
