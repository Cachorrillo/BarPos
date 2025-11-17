using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Pages.POS
{
    public class CuentasAbiertasModel : PageModel
    {
        private readonly AppDbContext _context;

        public CuentasAbiertasModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Cuenta> CuentasAbiertas { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? FiltroCliente { get; set; }

        public async Task OnGetAsync()
        {
            var query = _context.Cuentas
                .Include(c => c.DetalleCuenta)
                    .ThenInclude(d => d.Producto)
                .Include(c => c.DetalleCuenta)
                    .ThenInclude(d => d.Presentacion)
                        .ThenInclude(p => p.Producto)
                .Where(c => c.Estado == "Abierta")
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(FiltroCliente))
                query = query.Where(c => c.NombreCliente.Contains(FiltroCliente));

            CuentasAbiertas = await query
                .OrderByDescending(c => c.FechaApertura)
                .ToListAsync();
        }
    }
}
