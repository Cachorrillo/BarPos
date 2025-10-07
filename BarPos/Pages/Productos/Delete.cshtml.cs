using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Pages.Productos
{
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;

        public DeleteModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Producto Producto { get; set; } = default!;

        //Carga los datos del producto al abrir la página de eliminación

        public async Task<IActionResult> OnGetAsync(long id)
        {
            Producto = await _context.Productos
                .Include(p => p.Categoria)
                .   FirstOrDefaultAsync(p => p.Id == id);

            if (Producto == null)
                {
                return NotFound();
            }
            return Page();
        }

        //Elimina el producto de la base de datos al confirmar la eliminación

        public async Task<IActionResult> OnPostAsync(long id)
        {
            var producto = await _context.Productos.FindAsync(id);

            if (producto != null)
            {
                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage("./Index");
        }
    }

}
