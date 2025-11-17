using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BarPos.Pages.Roles
{
    public class LogoutModel : PageModel
    {
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(ILogger<LogoutModel> logger)
        {
            _logger = logger;
        }

        public IActionResult OnPost()
        {
            // Limpiar la sesión
            HttpContext.Session.Clear();
            _logger.LogInformation("Sesión cerrada");

            return RedirectToPage("/Index");
        }
    }
}