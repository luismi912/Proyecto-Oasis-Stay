using Front_end.Servicios.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Front_end.Pages
{
    [Authorize(Roles = "Administrador,Recepcionista,AmaDeLlaves")]
    public class HabitacionesModel : PageModel
    {
        private readonly HabitacionesApi _api;
        public HabitacionesModel(HabitacionesApi api) => _api = api;

        [BindProperty]
        public RoomInput Room { get; set; } = new();

        [BindProperty]
        public StateInput State { get; set; } = new();

        /// <summary>Filtro "Todos los estados" [E2 §4 US-01].</summary>
        [BindProperty(SupportsGet = true)]
        public string? Estado { get; set; }

        public List<HabitacionDto> Rooms { get; private set; } = new();
        public List<TipoHabitacionDto> RoomTypes { get; private set; } = new();
        public int TotalRooms { get; private set; }
        public int TotalFloors { get; private set; }

        /// <summary>El alta solo la ve el Administrador [D-28][D-34].</summary>
        public bool CanCreate => User.IsInRole("Administrador");

        public async Task OnGetAsync() => await CargarAsync();

        public async Task<IActionResult> OnPostCreateRoomAsync()
        {
            if (!CanCreate)
            {
                TempData["RoomError"] = "Solo el Administrador puede crear habitaciones.";
                return RedirectToPage();
            }
            if (string.IsNullOrWhiteSpace(Room.Number) || !int.TryParse(Room.Number, out var numero) || Room.TypeId is null || Room.Floor is null)
            {
                TempData["RoomError"] = "Completa todos los datos de la habitación.";
                return RedirectToPage();
            }
            try
            {
                await _api.CrearAsync(numero, Room.TypeId, Room.Floor);
                TempData["RoomSuccess"] = $"Habitación {numero} creada correctamente en el piso {Room.Floor}.";
            }
            catch (ApiException ex) { TempData["RoomError"] = ex.Message; }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostChangeStateAsync()
        {
            try
            {
                await _api.CambiarEstadoAsync(State.Number, State.NewState, State.Reason);
                TempData["RoomSuccess"] = $"Habitación {State.Number} actualizada a {State.NewState}.";
            }
            catch (ApiException ex) { TempData["RoomError"] = ex.Message; }
            return RedirectToPage(new { Estado });
        }

        private async Task CargarAsync()
        {
            try
            {
                var todas = await _api.ListarAsync(null);
                TotalRooms = todas.Count;
                TotalFloors = todas.Select(r => r.Piso).Distinct().Count();
                Rooms = string.IsNullOrWhiteSpace(Estado) ? todas : await _api.ListarAsync(Estado);
                RoomTypes = await _api.TiposAsync();
            }
            catch (ApiException ex) { TempData["RoomError"] = ex.Message; }
        }

        public class RoomInput
        {
            public string Number { get; set; } = string.Empty;
            public int? TypeId { get; set; }
            public int? Floor { get; set; }
        }

        public class StateInput
        {
            public int Number { get; set; }
            public string NewState { get; set; } = string.Empty;
            public string? Reason { get; set; }
        }
    }
}
