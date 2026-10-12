using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Front_end.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public string? Username { get; set; }

        [BindProperty]
        public string? Password { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            if (Username == "admin@oasisstay.co" && Password == "oasisstay")
            {
                return RedirectToPage("/Ventanas/PanelControl");
            }

            ModelState.AddModelError(string.Empty, "Las credenciales no son válidas.");
            return Page();
        }
    }
}
