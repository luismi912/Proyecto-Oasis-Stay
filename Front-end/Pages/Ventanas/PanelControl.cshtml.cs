using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Front_end.Pages
{
    [Authorize(Roles = "Administrador,Recepcionista")]
    public class PanelControlModel : PageModel
    {
        public void OnGet()
        {

        }
    }
}
