using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Pages.Presentaciones
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Presentacion Presentacion { get; set; } = new Presentacion();

        public SelectList ProductosList { get; set; } = default!;

        public async Task OnGetAsync()
        {
            // ✅ Solo productos que pertenezcan a la categoría "Licores"
            var productosLicores = await _context.Productos
                .Include(p => p.Categoria)
                .Where(p => p.Categoria.Nombre == "Licores")
                .ToListAsync();

            ProductosList = new SelectList(productosLicores, "Id", "Nombre");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Evitamos validar propiedades de navegación
            ModelState.Remove("Presentacion.Producto");

            if (!ModelState.IsValid)
            {
                var productosLicores = await _context.Productos
                    .Include(p => p.Categoria)
                    .Where(p => p.Categoria.Nombre == "Licores")
                    .ToListAsync();

                ProductosList = new SelectList(productosLicores, "Id", "Nombre");
                return Page();
            }

            _context.Presentaciones.Add(Presentacion);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
