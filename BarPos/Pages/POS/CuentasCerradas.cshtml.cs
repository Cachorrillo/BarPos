using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Pages.POS
{
    public class CuentasCerradasModel : PageModel
    {
        private readonly AppDbContext _context;

        public CuentasCerradasModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Cuenta> CuentasCerradas { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? FiltroCliente { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FiltroMetodo { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FiltroFecha { get; set; }

        public async Task OnGetAsync()
        {
            var query = _context.Cuentas
                .Include(c => c.DetalleCuenta)
                    .ThenInclude(d => d.Presentacion)
                        .ThenInclude(p => p.Producto)
                .Include(c => c.DetalleCuenta)
                    .ThenInclude(d => d.Producto)
                .Where(c => c.Estado == "Cerrada")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(FiltroCliente))
                query = query.Where(c => c.NombreCliente.Contains(FiltroCliente));

            if (!string.IsNullOrWhiteSpace(FiltroMetodo))
                query = query.Where(c => c.MetodoPago == FiltroMetodo);

            if (FiltroFecha.HasValue)
            {
                var fecha = FiltroFecha.Value.Date;
                query = query.Where(c => c.FechaApertura.Date == fecha);
            }

            CuentasCerradas = await query
                .OrderByDescending(c => c.FechaApertura)
                .ToListAsync();
        }


    }
}
