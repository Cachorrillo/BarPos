using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BarPos.Models;

namespace BarPos.Pages.POS
{
    public class DetallesModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetallesModel(AppDbContext context)
        {
            _context = context;
        }

        public Cuenta Cuenta { get; set; } = new Cuenta();
        public IList<DetalleCuenta> Detalles { get; set; } = new List<DetalleCuenta>();

        [BindProperty]
        public long CategoriaId { get; set; }

        [BindProperty]
        public long ProductoId { get; set; }

        [BindProperty]
        public long PresentacionId { get; set; }

        [BindProperty]
        public int Cantidad { get; set; } = 1;

        public async Task<IActionResult> OnGetAsync(long id)
        {
            Cuenta = await _context.Cuentas.FirstOrDefaultAsync(c => c.Id == id);
            if (Cuenta == null)
                return NotFound();

            Detalles = await _context.DetalleCuenta
                  .Include(d => d.Producto)
                  .Include(d => d.Presentacion)
                  .ThenInclude(p => p.Producto)
                  .Where(d => d.CuentaId == id)
                  .ToListAsync();




            ViewData["Categorias"] = await _context.Categorias
                .OrderBy(c => c.Nombre)
                .ToListAsync();

            return Page();
        }

        // Handler AJAX → productos por categoría
        public async Task<JsonResult> OnGetProductosPorCategoria(long categoriaId)
        {
            var productos = await _context.Productos
                .Where(p => p.CategoriaId == categoriaId)
                .Select(p => new { p.Id, p.Nombre })
                .ToListAsync();

            return new JsonResult(productos);
        }

        // Handler AJAX → presentaciones por producto
        public async Task<JsonResult> OnGetPresentacionesPorProducto(long productoId)
        {
            var presentaciones = await _context.Presentaciones
                .Where(pr => pr.ProductoId == productoId)
                .Select(pr => new { pr.Id, pr.Nombre, pr.PrecioVenta })
                .ToListAsync();

            return new JsonResult(presentaciones);
        }

        public string Prueba => "Handler detectado correctamente";

        // Handler POST → agregar producto
        public async Task<IActionResult> OnPostAgregarAsync(long id)
        {
            var cuenta = await _context.Cuentas.FindAsync(id);
            if (cuenta == null)
                return NotFound();

            decimal precioVenta = 0;
            long? presentacionId = null;
            long? productoId = null;

            if (PresentacionId > 0)
            {
                // 🔸 Caso: producto con presentación (licor)
                var presentacion = await _context.Presentaciones
                    .Include(p => p.Producto)
                    .FirstOrDefaultAsync(p => p.Id == PresentacionId);

                if (presentacion == null)
                    return NotFound();

                presentacionId = presentacion.Id;
                productoId = presentacion.ProductoId;
                precioVenta = presentacion.PrecioVenta;
            }
            else
            {
                // 🔹 Caso: producto sin presentación
                var producto = await _context.Productos.FirstOrDefaultAsync(p => p.Id == ProductoId);
                if (producto == null)
                    return NotFound();

                productoId = producto.Id;
                precioVenta = producto.PrecioCompra;
            }

            var nuevoDetalle = new DetalleCuenta
            {
                CuentaId = cuenta.Id,
                PresentacionId = presentacionId,
                ProductoId = productoId,   // ✅ ahora siempre guarda el producto asociado
                Cantidad = Cantidad,
                PrecioUnitario = precioVenta
            };

            _context.DetalleCuenta.Add(nuevoDetalle);
            cuenta.Total += precioVenta * Cantidad;
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id });
        }



    }
}
