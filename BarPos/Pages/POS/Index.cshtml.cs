using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Pages.POS
{
    public class IndexModel : PageModel
    {

        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<Cuenta> CuentasAbiertas { get; set; } = new List<Cuenta>();

        public async Task OnGetAsync()
        {
            // Trae solo las cuentas abiertas

            CuentasAbiertas = await _context.Cuentas
                .Where(c => c.Estado == "Abierta")
                .OrderByDescending(c => c.FechaApertura)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostCerrarCuentaAsync(long id)
        {
            var cuenta = await _context.Cuentas.FindAsync(id);

            if (cuenta == null)
            {
                return NotFound();
            }

            cuenta.Estado = "Cerrada";
            await _context.SaveChangesAsync();


            return RedirectToPage();
        }

    }
}
