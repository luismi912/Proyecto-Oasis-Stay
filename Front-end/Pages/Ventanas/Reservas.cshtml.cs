using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Front_end.Pages
{
    public class ReservasModel : PageModel
    {
        [BindProperty]
        public ReservationInput Reservation { get; set; } = new();

        public void OnGet()
        {

        }

        public IActionResult OnPostCreateReservation()
        {
            if (!ModelState.IsValid)
            {
                TempData["ReservationError"] = "Completa todos los datos obligatorios de la reserva.";
                return RedirectToPage();
            }

            TempData["ReservationSuccess"] =
                $"Reserva creada para {Reservation.GuestName}. Habitación {Reservation.RoomNumber}.";
            return RedirectToPage();
        }

        public class ReservationInput
        {
            [BindProperty]
            public string GuestName { get; set; } = string.Empty;

            [BindProperty]
            public string Document { get; set; } = string.Empty;

            [BindProperty]
            public string Email { get; set; } = string.Empty;

            [BindProperty]
            public string Phone { get; set; } = string.Empty;

            [BindProperty]
            public DateTime CheckIn { get; set; }

            [BindProperty]
            public DateTime CheckOut { get; set; }

            [BindProperty]
            public int Guests { get; set; }

            [BindProperty]
            public string RoomNumber { get; set; } = string.Empty;
        }
    }
}
