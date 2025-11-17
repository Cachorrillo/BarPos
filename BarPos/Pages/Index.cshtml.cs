using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly AppDbContext _context;

        public IndexModel(ILogger<IndexModel> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }
        [TempData]
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAdminAsync(string password)
        {
            // Obtener la contraseña guardada en la base de datos
            var config = await _context.Contrasena
                .FirstOrDefaultAsync(c => c.Clave == "PasswordAdmin");

            if (config == null)
            {
                ErrorMessage = "Error de configuración. Contacte al administrador.";
                return Page();
            }

            if (password == config.Valor)
            {
                // Guardar en sesión que es administrador
                HttpContext.Session.SetString("TipoUsuario", "Administrador");
                _logger.LogInformation("Acceso como administrador");
                return RedirectToPage("/POS/Index");
            }
            else
            {
                ErrorMessage = "Contraseña incorrecta";
                _logger.LogWarning("Intento fallido de acceso como administrador");
                return Page();
            }
        }

        public IActionResult OnPostEmpleado()
        {
            // Guardar en sesión que es empleado
            HttpContext.Session.SetString("TipoUsuario", "Empleado");
            _logger.LogInformation("Acceso como empleado");
            return RedirectToPage("/POS/Index");
        }
    }
}