using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Pages.Presentaciones
{
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;

        public EditModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Presentacion Presentacion { get; set; } = new Presentacion();

        public SelectList ProductosList { get; set; } = null!;  

        public async Task<IActionResult> OnGetAsync(long? id)
        {
            Presentacion = await _context.Presentaciones
                .Include(p => p.Producto)
                .ThenInclude(c => c.Categoria)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (Presentacion == null)
            {
                return NotFound();
            }

            //Solo productos de la categoria Licores

            var productosLicores = await _context.Productos
                .Include(p => p.Categoria)
                .Where(p => p.Categoria.Nombre == "Licores")
                .ToListAsync();

            ProductosList = new SelectList(productosLicores, "Id", "Nombre", Presentacion.ProductoId);

            return Page();  

        }

        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("Presentacion.Producto");

            if (!ModelState.IsValid)
            {
                var productosLicores = await _context.Productos
                    .Include(p => p.Categoria)
                    .Where(p => p.Categoria.Nombre == "Licores")
                    .ToListAsync();

                ProductosList = new SelectList(productosLicores, "Id", "Nombre", Presentacion.ProductoId);
                return Page();
            }

            _context.Attach(Presentacion).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");

        }
    }
}
