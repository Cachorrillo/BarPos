using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Dynamic;

namespace BarPos.Pages.POS
{
    public class CreateModel : PageModel
    {
       private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Cuenta Cuenta { get; set; } = new Cuenta();

        public void OnGet()
        {
            
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            //Definir los valores iniciales de la cuenta

            Cuenta.Estado = "Abierta";
            Cuenta.FechaApertura = DateTime.Now;
            Cuenta.Total = 0;
            Cuenta.MontoPagado = 0;
            Cuenta.Vuelto = 0;

            _context.Cuentas.Add(Cuenta);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
