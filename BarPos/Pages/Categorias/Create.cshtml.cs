using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BarPos.Pages.Categorias
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _contex;

        public CreateModel(AppDbContext context)
        {
            _contex = context;
        }

        [BindProperty]
        public Categoria Categoria { get; set; } = new Categoria();

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }


            _contex.Categorias.Add(Categoria);
            await _contex.SaveChangesAsync();


            return RedirectToPage("./Index");
        }
    }
}
