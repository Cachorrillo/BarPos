using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Pages.Categorias
{
    public class IndexModel : PageModel
    {

        public readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<Categoria> ListaCategorias { get; set; } = new List<Categoria>();

        public async Task<IActionResult> OnGetAsync()
        {
            // Verificar que sea administrador
            var tipoUsuario = HttpContext.Session.GetString("TipoUsuario");
            if (tipoUsuario != "Administrador")
            {
                return RedirectToPage("/Index");
            }

            ListaCategorias = await _context.Categorias.ToListAsync();

            return Page();
        }



    }
}
