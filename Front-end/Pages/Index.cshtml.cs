using System.Security.Claims;
using Front_end.Servicios.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Front_end.Pages
{
    public class IndexModel : PageModel
    {
        private readonly OasisApiClient _api;
        public IndexModel(OasisApiClient api) => _api = api;

        [BindProperty]
        public string? Username { get; set; }

        [BindProperty]
        public string? Password { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnGetLogoutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                var r = await _api.LoginAsync(Username ?? "", Password ?? "");
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, r.Nombre),
                    new Claim(ClaimTypes.Role, r.Rol),
                    new Claim("jwt", r.Token),
                };
                var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                    new AuthenticationProperties { IsPersistent = true, ExpiresUtc = r.ExpiraEn });

                return RedirectToPage(r.Rol == "AmaDeLlaves" ? "/Ventanas/Habitaciones" : "/Ventanas/PanelControl");
            }
            catch (ApiException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(string.Empty, "No se pudo conectar con el servidor.");
            }
            return Page();
        }
    }
}
