using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Pages.Presentaciones
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<Presentacion> ListaPresentaciones { get; set; } = new List<Presentacion>();

        public async Task OnGetAsync()
        {
            ListaPresentaciones = await _context.Presentaciones
                .Include(p => p.Producto)
                .ThenInclude(prod => prod.Categoria)
                .OrderBy(p => p.Producto.Nombre)
                .ToListAsync();


        }
    }
}
