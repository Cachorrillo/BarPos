using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Pages.Productos
{
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;

        public EditModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Producto Producto { get; set; } = default!;

        public SelectList CategoriasList { get; set; } = default!;

        // ====================================
        // Cargar los datos del producto a editar
        // ====================================
        public async Task<IActionResult> OnGetAsync(long id)
        {
            Producto = await _context.Productos.FindAsync(id);

            if (Producto == null)
            {
                return NotFound();
            }

            var categorias = await _context.Categorias.ToListAsync();
            CategoriasList = new SelectList(categorias, "Id", "Nombre", Producto.CategoriaId);

            return Page();
        }

        // ====================================
        // Guardar los cambios realizados
        // ====================================
        public async Task<IActionResult> OnPostAsync()
        {
            // 🔹 Quitar validaciones automáticas de navegaciones que no se editan aquí
            ModelState.Remove("Producto.Categoria");
            ModelState.Remove("Producto.Presentaciones");

            if (!ModelState.IsValid)
            {
                await CargarCategoriasAsync();
                return Page();
            }

            // ================================
            // 🔸 Validación especial para licores
            // ================================
            if (Producto.EsLicor)
            {
                if (!Producto.MililitrosPorBotella.HasValue || Producto.MililitrosPorBotella <= 0)
                {
                    ModelState.AddModelError("Producto.MililitrosPorBotella",
                        "Debe indicar cuántos mililitros tiene la botella del licor.");
                    await CargarCategoriasAsync();
                    return Page();
                }
            }
            else
            {
                // Si el producto dejó de ser licor, limpiamos los valores
                Producto.MililitrosPorBotella = null;
                Producto.MlRestantesBotellaAbierta = 0;
            }

            // ================================
            // 🔸 Guardar cambios en la BD
            // ================================
            try
            {
                _context.Attach(Producto).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await ProductoExists(Producto.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        // ====================================
        // Métodos auxiliares
        // ====================================
        private async Task CargarCategoriasAsync()
        {
            var categorias = await _context.Categorias.ToListAsync();
            CategoriasList = new SelectList(categorias, "Id", "Nombre", Producto?.CategoriaId);
        }

        private async Task<bool> ProductoExists(long id)
        {
            return await _context.Productos.AnyAsync(e => e.Id == id);
        }
    }
}
