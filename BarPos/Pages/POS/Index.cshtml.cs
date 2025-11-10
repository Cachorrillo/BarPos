using BarPos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Pages.POS
{
    public class IndexModel : PageModel
    {

        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<Cuenta> CuentasAbiertas { get; set; } = new List<Cuenta>();

        public async Task OnGetAsync()
        {
            // Trae solo las cuentas abiertas

            CuentasAbiertas = await _context.Cuentas
                .Where(c => c.Estado == "Abierta")
                .OrderByDescending(c => c.FechaApertura)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostCerrarCuentaAsync(long id)
        {
            var cuenta = await _context.Cuentas.FindAsync(id);

            if (cuenta == null)
            {
                return NotFound();
            }

            cuenta.Estado = "Cerrada";
            await _context.SaveChangesAsync();


            return RedirectToPage();
        }
        public async Task<JsonResult> OnGetResumenCajaAsync(string fecha)
        {
            // Si no se proporciona fecha, usar hoy
            DateTime fechaConsulta;
            if (string.IsNullOrEmpty(fecha) || !DateTime.TryParse(fecha, out fechaConsulta))
            {
                fechaConsulta = DateTime.Today;
            }
            else
            {
                fechaConsulta = fechaConsulta.Date;
            }
            var fechaFin = fechaConsulta.AddDays(1);

            // Obtener todas las cuentas cerradas del día
            var cuentasDia = await _context.Cuentas
                .Include(c => c.DetalleCuenta)
                    .ThenInclude(d => d.Producto)
                .Include(c => c.DetalleCuenta)
                    .ThenInclude(d => d.Presentacion)
                        .ThenInclude(p => p.Producto)
                .Where(c => c.Estado == "Cerrada"
                    && c.FechaApertura >= fechaConsulta
                    && c.FechaApertura < fechaFin)
                .ToListAsync();

            if (cuentasDia.Count == 0)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "No hay ventas registradas para esta fecha."
                });
            }

            // Calcular totales por método de pago
            var ventasEfectivo = cuentasDia
                .Where(c => c.MetodoPago == "Efectivo")
                .Sum(c => c.Total);

            var ventasTarjeta = cuentasDia
                .Where(c => c.MetodoPago == "Tarjeta")
                .Sum(c => c.Total);

            // Agrupar productos vendidos
            var productosVendidos = cuentasDia
                .SelectMany(c => c.DetalleCuenta)
                .GroupBy(d => new
                {
                    ProductoId = d.ProductoId ?? d.Presentacion.ProductoId,
                    NombreProducto = d.Producto?.Nombre ?? d.Presentacion?.Producto?.Nombre ?? "Desconocido",
                    Presentacion = d.Presentacion?.Nombre
                })
                .Select(g => new ProductoVendido
                {
                    NombreProducto = g.Key.NombreProducto,
                    Presentacion = g.Key.Presentacion,
                    CantidadVendida = g.Sum(d => d.Cantidad),
                    TotalVentas = g.Sum(d => d.Cantidad * d.PrecioUnitario)
                })
                .OrderByDescending(p => p.CantidadVendida)
                .ToList();

            // Top 5 más vendidos y 5 menos vendidos
            var masVendidos = productosVendidos.Take(5).ToList();
            var menosVendidos = productosVendidos
                .OrderBy(p => p.CantidadVendida)
                .Take(5)
                .ToList();

            var todosProductosVendidos = productosVendidos
    .OrderBy(p => p.NombreProducto)
    .ToList();

            var resumen = new
            {
                success = true,
                fecha = fechaConsulta.ToString("dd/MM/yyyy"),
                totalCuentas = cuentasDia.Count,
                totalVentas = cuentasDia.Sum(c => c.Total),
                ventasEfectivo = ventasEfectivo,
                ventasTarjeta = ventasTarjeta,
                productosMasVendidos = masVendidos,
                productosMenosVendidos = menosVendidos,
                todosProductosVendidos = todosProductosVendidos
            };

            return new JsonResult(resumen);
        }
    }
}
