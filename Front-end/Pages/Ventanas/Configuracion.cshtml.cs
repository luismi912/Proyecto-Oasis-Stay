using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Front_end.Pages
{
    [Authorize(Roles = "Administrador")]
    public class ConfiguracionModel : PageModel
    {
        public void OnGet()
        {

        }
    }
}
