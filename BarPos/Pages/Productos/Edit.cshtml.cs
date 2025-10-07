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

        //Aqui se cargan los datos del producto a editar

        public async Task<IActionResult> OnGetAsync(long id)
        {
            Producto = await _context.Productos.FindAsync(id);

            if (Producto == null)
            {
                return NotFound();
            }

            //Poblar el dropdown con las categorias
            var categorias = await _context.Categorias.ToListAsync();
            CategoriasList = new SelectList(categorias, "Id", "Nombre", Producto.CategoriaId);

            return Page();
        }

        //Aqui se guardan los cambios realizados al producto
        public async Task<IActionResult> OnPostAsync()
        {
            // 🩵 Esto mostrará errores en pantalla si algo no se envía
            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    Console.WriteLine($"❌ Error en campo '{state.Key}': {error.ErrorMessage}");
                }
            }

            // Ignorar validación de navegaciones
            ModelState.Remove("Producto.Categoria");
            ModelState.Remove("Producto.Presentaciones");


            if(!ModelState.IsValid)
            {
                //Poblar el dropdown con las categorias
                var categorias = await _context.Categorias.ToListAsync();
                CategoriasList = new SelectList(categorias, "Id", "Nombre", Producto?.CategoriaId);
                return Page();
            }

            //Actualizar el producto

            _context.Attach(Producto).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");

        }

    }
}
