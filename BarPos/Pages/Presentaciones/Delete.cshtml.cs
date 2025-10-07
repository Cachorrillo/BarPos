using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Pages.Presentaciones
{
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;

        public DeleteModel(AppDbContext context)
        {
            _context = context;
        }
        [BindProperty]
        public Presentacion Presentacion { get; set; } = default;

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
            return Page();

        }

        public async Task<IActionResult> OnPostAsync(long? id)
        {
            var presentacion = await _context.Presentaciones.FindAsync(id);

            if (presentacion != null)
            {
                _context.Presentaciones.Remove(presentacion);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }


    }
}
