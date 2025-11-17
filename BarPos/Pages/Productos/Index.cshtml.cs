using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Pages.Productos
{
    public class IndexModel : PageModel
    {
       private readonly AppDbContext _context;
        public IndexModel(AppDbContext context)
        {
            _context = context;
        }
        public IList<Producto> ListaProductos { get;set; } = new List<Producto>();    

        public async Task OnGetAsync()
        {
            //aqui se incluye las categorias para mostrar el nombre de la categoria en la vista

            ListaProductos = await _context.Productos
                .Include(p => p.Categoria)
                .ToListAsync();

        }

    }
}
