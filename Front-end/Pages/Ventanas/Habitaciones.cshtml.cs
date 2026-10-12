using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Front_end.Pages
{
    public class HabitacionesModel : PageModel
    {
        [BindProperty]
        public RoomInput Room { get; set; } = new();

        public void OnGet()
        {

        }

        public IActionResult OnPostCreateRoom()
        {
            if (!ModelState.IsValid)
            {
                TempData["RoomError"] = "Completa todos los datos de la habitación.";
                return RedirectToPage();
            }

            TempData["RoomSuccess"] =
                $"Habitación {Room.Number} creada correctamente en el {Room.Floor.ToLowerInvariant()}.";
            TempData["CreatedRoomNumber"] = Room.Number;
            TempData["CreatedRoomType"] = Room.Type;
            TempData["CreatedRoomPrice"] = Room.Price;
            return RedirectToPage();
        }

        public class RoomInput
        {
            [BindProperty]
            public string Number { get; set; } = string.Empty;

            [BindProperty]
            public string Type { get; set; } = "Doble Clásica";

            [BindProperty]
            public string Floor { get; set; } = "Piso 4";

            [BindProperty]
            public string Price { get; set; } = "$ 185.000";
        }
    }
}
