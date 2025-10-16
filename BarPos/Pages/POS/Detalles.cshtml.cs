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
        public long ProductoId { get; set; }

        [BindProperty]
        public long? PresentacionId { get; set; }

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

        public async Task<JsonResult> OnGetProductosPorCategoria(long categoriaId)
        {
            var productos = await _context.Productos
                .Where(p => p.CategoriaId == categoriaId)
                .Select(p => new { p.Id, p.Nombre })
                .ToListAsync();

            return new JsonResult(productos);
        }

        public async Task<JsonResult> OnGetPresentacionesPorProducto(long productoId)
        {
            var presentaciones = await _context.Presentaciones
                .Where(pr => pr.ProductoId == productoId)
                .Select(pr => new { pr.Id, pr.Nombre, pr.PrecioVenta })
                .ToListAsync();

            return new JsonResult(presentaciones);
        }

        public async Task<IActionResult> OnPostAgregarMultiplesAsync(long id, [FromBody] List<DetalleCuentaTemp> productos)
        {
            var cuenta = await _context.Cuentas.FindAsync(id);
            if (cuenta == null)
                return NotFound();

            foreach (var item in productos)
            {
                decimal precio = 0;
                long? productoId = item.ProductoId;
                long? presentacionId = item.PresentacionId;

                if (presentacionId.HasValue)
                {
                    var presentacion = await _context.Presentaciones.FirstOrDefaultAsync(p => p.Id == presentacionId);
                    if (presentacion == null) continue;
                    precio = presentacion.PrecioVenta;
                    productoId = presentacion.ProductoId;
                }
                else
                {
                    var producto = await _context.Productos.FirstOrDefaultAsync(p => p.Id == productoId);
                    if (producto == null) continue;
                    precio = producto.PrecioCompra;
                }

                var nuevo = new DetalleCuenta
                {
                    CuentaId = cuenta.Id,
                    ProductoId = productoId,
                    PresentacionId = presentacionId,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = precio
                };

                _context.DetalleCuenta.Add(nuevo);
                cuenta.Total += precio * item.Cantidad;
            }

            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }

        public class DetalleCuentaTemp
        {
            public long ProductoId { get; set; }
            public long? PresentacionId { get; set; }
            public int Cantidad { get; set; }
        }

        public async Task<IActionResult> OnPostActualizarDetalleAsync(long id, [FromBody] DetalleUpdateRequest req)
        {
            if (req == null || req.DetalleId <= 0 || req.Cantidad < 1)
                return new JsonResult(new { success = false, message = "Datos inválidos." });

            var detalle = await _context.DetalleCuenta
                .Include(d => d.Cuenta)
                .FirstOrDefaultAsync(d => d.Id == req.DetalleId && d.CuentaId == id);

            if (detalle == null)
                return new JsonResult(new { success = false, message = "Detalle no encontrado." });

            // Restar el subtotal anterior del total
            detalle.Cuenta.Total -= detalle.PrecioUnitario * detalle.Cantidad;

            // Actualizar cantidad
            detalle.Cantidad = req.Cantidad;

            // Sumar el nuevo subtotal al total
            var nuevoSubtotal = detalle.PrecioUnitario * detalle.Cantidad;
            detalle.Cuenta.Total += nuevoSubtotal;

            await _context.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                detalleId = detalle.Id,
                nuevoSubtotal = nuevoSubtotal,
                nuevoTotal = detalle.Cuenta.Total
            });
        }

        public async Task<IActionResult> OnPostEliminarDetalleAsync(long id, [FromBody] DetalleDeleteRequest req)
        {
            if (req == null || req.DetalleId <= 0)
                return new JsonResult(new { success = false, message = "Datos inválidos." });

            var detalle = await _context.DetalleCuenta
                .Include(d => d.Cuenta)
                .FirstOrDefaultAsync(d => d.Id == req.DetalleId && d.CuentaId == id);

            if (detalle == null)
                return new JsonResult(new { success = false, message = "Detalle no encontrado." });

            // Restar el subtotal del total y eliminar
            detalle.Cuenta.Total -= detalle.PrecioUnitario * detalle.Cantidad;
            _context.DetalleCuenta.Remove(detalle);
            await _context.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                detalleId = detalle.Id,
                nuevoTotal = detalle.Cuenta.Total
            });
        }

        public class DetalleUpdateRequest
        {
            public long DetalleId { get; set; }
            public int Cantidad { get; set; }
        }

        public class DetalleDeleteRequest
        {
            public long DetalleId { get; set; }
        }


    }
}
