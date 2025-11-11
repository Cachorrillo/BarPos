using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Pages.Productos
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        // ===========================
        // Propiedades y datos del modelo
        // ===========================
        [BindProperty]
        public Producto Producto { get; set; } = default!;

        public SelectList CategoriasList { get; set; } = default!;

        // ===========================
        // Cargar categorías al entrar
        // ===========================
        public async Task OnGetAsync()
        {
            var categorias = await _context.Categorias.ToListAsync();
            CategoriasList = new SelectList(categorias, "Id", "Nombre");
        }

        // ===========================
        // Crear nuevo producto
        // ===========================
        public async Task<IActionResult> OnPostAsync()
        {
            // Si algo en el formulario no cumple validaciones
            if (!ModelState.IsValid)
            {
                await CargarCategoriasAsync();
                return Page();
            }

            // ===========================
            // Validación adicional: Si es licor, debe tener mililitros definidos
            // ===========================
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
                // Si no es licor, se asegura que el campo quede en null
                Producto.MililitrosPorBotella = null;
                Producto.MlRestantesBotellaAbierta = 0;
            }

            // ===========================
            // Guardar producto
            // ===========================
            _context.Productos.Add(Producto);
            await _context.SaveChangesAsync();

            // Redirige al listado
            return RedirectToPage("./Index");
        }

        // ===========================
        // Método auxiliar para recargar categorías
        // ===========================
        private async Task CargarCategoriasAsync()
        {
            var categorias = await _context.Categorias.ToListAsync();
            CategoriasList = new SelectList(categorias, "Id", "Nombre");
        }
    }
}
