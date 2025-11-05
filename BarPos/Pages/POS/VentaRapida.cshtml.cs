using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BarPos.Models;

namespace BarPos.Pages.POS
{
    public class VentaRapidaModel : PageModel
    {
        private readonly AppDbContext _context;
        public VentaRapidaModel(AppDbContext context) => _context = context;

        public IList<Categoria> Categorias { get; set; } = new List<Categoria>();

        public async Task OnGetAsync()
        {
            Categorias = await _context.Categorias.OrderBy(c => c.Nombre).ToListAsync();
        }

        // ======= Lecturas =======
        public async Task<JsonResult> OnGetProductosPorCategoria(long categoriaId)
        {
            var productos = await _context.Productos
                .Where(p => p.CategoriaId == categoriaId)
                .Select(p => new
                {
                    p.Id,
                    p.Nombre,
                    p.Stock,
                    // Si tienes PrecioVenta en Producto, cámbialo aquí:
                    Precio = p.PrecioCompra
                })
                .ToListAsync();

            return new JsonResult(productos);
        }

        public async Task<JsonResult> OnGetPresentacionesPorProducto(long productoId)
        {
            var pres = await _context.Presentaciones
                .Include(pr => pr.Producto)
                .Where(pr => pr.ProductoId == productoId)
                .Select(pr => new
                {
                    pr.Id,
                    pr.Nombre,
                    pr.PrecioVenta,
                    Stock = pr.Producto.Stock // stock compartido por producto base
                })
                .ToListAsync();

            return new JsonResult(pres);
        }

        // ======= DTOs =======
        public class VentaRapidaItem
        {
            public long ProductoId { get; set; }
            public long? PresentacionId { get; set; }
            public int Cantidad { get; set; }
        }

        public class VentaRapidaRequest
        {
            public string MetodoPago { get; set; } = "Efectivo";
            public decimal MontoPagado { get; set; }
            public decimal Vuelto { get; set; }
            public List<VentaRapidaItem> Items { get; set; } = new();
        }

        // ======= Confirmar venta rápida =======
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostConfirmarVentaAsync([FromBody] VentaRapidaRequest req)
        {
            try
            {
                if (req == null || req.Items == null || req.Items.Count == 0)
                    return new JsonResult(new { success = false, message = "No se enviaron productos." });

                var errores = new List<string>();
                decimal total = 0m;

                using var tx = await _context.Database.BeginTransactionAsync();

                // Validación de stock
                foreach (var it in req.Items)
                {
                    var prod = await _context.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == it.ProductoId);
                    if (prod == null)
                    {
                        errores.Add($"Producto ID {it.ProductoId} no encontrado.");
                        continue;
                    }
                    if (prod.Stock < it.Cantidad)
                        errores.Add($"{prod.Nombre}: Stock insuficiente. Solo hay {prod.Stock} unidades.");
                }

                if (errores.Count > 0)
                    return new JsonResult(new { success = false, message = "Error de stock.", errores });

                // Crear cuenta "Venta Rápida"
                var cuenta = new Cuenta
                {
                    NombreCliente = "Venta Rápida",
                    Estado = "Cerrada",
                    FechaApertura = DateTime.Now,
                    MetodoPago = req.MetodoPago
                };
                _context.Cuentas.Add(cuenta);
                await _context.SaveChangesAsync();

                // Procesar cada producto
                foreach (var it in req.Items)
                {
                    decimal precio = 0m;
                    long? productoIdFinal = it.ProductoId;
                    long? presentacionId = it.PresentacionId;

                    var producto = await _context.Productos.FirstOrDefaultAsync(p => p.Id == it.ProductoId);
                    if (producto == null)
                    {
                        errores.Add($"Producto ID {it.ProductoId} no encontrado al procesar.");
                        continue;
                    }

                    if (presentacionId.HasValue)
                    {
                        var pres = await _context.Presentaciones
                            .Include(p => p.Producto)
                            .FirstOrDefaultAsync(p => p.Id == presentacionId.Value);

                        if (pres == null)
                        {
                            errores.Add($"Presentación no válida para producto {producto.Nombre}.");
                            continue;
                        }

                        precio = pres.PrecioVenta;
                        productoIdFinal = pres.ProductoId;
                    }
                    else
                    {
                        precio = producto.PrecioCompra;
                    }

                    if (producto.Stock < it.Cantidad)
                    {
                        errores.Add($"{producto.Nombre}: Stock insuficiente al confirmar.");
                        continue;
                    }

                    producto.Stock -= it.Cantidad;
                    _context.Productos.Update(producto);

                    _context.DetalleCuenta.Add(new DetalleCuenta
                    {
                        CuentaId = cuenta.Id,
                        ProductoId = productoIdFinal,
                        PresentacionId = presentacionId,
                        Cantidad = it.Cantidad,
                        PrecioUnitario = precio
                    });

                    total += precio * it.Cantidad;
                }

                if (errores.Count > 0)
                {
                    await tx.RollbackAsync();
                    return new JsonResult(new { success = false, message = "Error al procesar algunos productos.", errores });
                }

                // Cálculo de montos
                decimal montoPagadoFinal = req.MetodoPago == "Tarjeta" ? total : req.MontoPagado;
                decimal vueltoFinal = req.MetodoPago == "Tarjeta" ? 0 : Math.Max(0, montoPagadoFinal - total);

                if (req.MetodoPago == "Efectivo" && montoPagadoFinal + 0.001m < total)
                {
                    await tx.RollbackAsync();
                    return new JsonResult(new { success = false, message = "El monto pagado no puede ser menor al total." });
                }

                cuenta.Total = total;
                cuenta.MontoPagado = montoPagadoFinal;
                cuenta.Vuelto = vueltoFinal;
                _context.Cuentas.Update(cuenta);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return new JsonResult(new { success = true, cuentaId = cuenta.Id, total });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                return new JsonResult(new { success = false, message = "Error interno: " + inner });
            }
        }

    }
}
