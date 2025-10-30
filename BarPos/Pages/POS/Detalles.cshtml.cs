using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BarPos.Models;
using System.Globalization;
using System.Threading;

namespace BarPos.Pages.POS
{
    public class DetallesModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetallesModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Cuenta Cuenta { get; set; } = new Cuenta();
        public IList<DetalleCuenta> Detalles { get; set; } = new List<DetalleCuenta>();

        // 🔹 Diccionario para pasar stock disponible al frontend
        public Dictionary<long, int> StockDisponible { get; set; } = new Dictionary<long, int>();

        [BindProperty]
        public long ProductoId { get; set; }

        [BindProperty]
        public long? PresentacionId { get; set; }

        [BindProperty]
        public int Cantidad { get; set; } = 1;

        // MEtodo que se ejecuta al cargar la página
        // Recibe el ID de la cuenta a mostrar
        public async Task<IActionResult> OnGetAsync(long id)
        {
            // Busca la cuenta correspondiente al  id
            Cuenta = await _context.Cuentas.FirstOrDefaultAsync(c => c.Id == id);
            if (Cuenta == null)
                return NotFound();//Si no existe devuelve 404

            //carga todos los detalles de esa cuenta incluyendo los productos y presentaciones
            Detalles = await _context.DetalleCuenta
                .Include(d => d.Producto)
                .Include(d => d.Presentacion)
                    .ThenInclude(p => p.Producto)
                .Where(d => d.CuentaId == id)
                .ToListAsync();

            /* Calcula el stock disponible sumando el stock actual del producto
             más la cantidad que ya está en la cuenta (para permitir modificaciones).*/
            foreach (var detalle in Detalles)
            {
                var producto = detalle.Producto ?? await _context.Productos.FindAsync(detalle.ProductoId);
                if (producto != null)
                {
                    StockDisponible[detalle.Id] = producto.Stock + detalle.Cantidad;
                }
            }
            //carga todas las categorías para mostrarlas en  menu 
            ViewData["Categorias"] = await _context.Categorias
                .OrderBy(c => c.Nombre)
                .ToListAsync();

            return Page();// Devuelve la vista Razor 
        }

        // Metodo GET que devuelve los productos de una categoria especifica en formato JSON
        public async Task<JsonResult> OnGetProductosPorCategoria(long categoriaId)
        {
            var productos = await _context.Productos
                .Where(p => p.CategoriaId == categoriaId)
                .Select(p => new { p.Id, p.Nombre, p.Stock })
                .ToListAsync();

            return new JsonResult(productos);
        }

        // Metodo GET que devuelve las presentaciones de un producto especifico
        public async Task<JsonResult> OnGetPresentacionesPorProducto(long productoId)
        {
            var presentaciones = await _context.Presentaciones
                .Include(pr => pr.Producto)
                .Where(pr => pr.ProductoId == productoId)
                .Select(pr => new {
                    pr.Id,
                    pr.Nombre,
                    pr.PrecioVenta,
                    Stock = pr.Producto.Stock
                })
                .ToListAsync();

            return new JsonResult(presentaciones);
        }

        // Metodo POST para agregar varios productos al detalle de una cuenta
        public async Task<IActionResult> OnPostAgregarMultiplesAsync(long id, [FromBody] List<DetalleCuentaTemp> productos)
        {
            // Busca la cuenta a la que se agregan los productos
            var cuenta = await _context.Cuentas.FindAsync(id);
            if (cuenta == null)
                return NotFound();

            // Obtiene los detalles ya existentes para evitar duplicados
            var detallesExistentes = await _context.DetalleCuenta
                .Where(d => d.CuentaId == id)
                .ToListAsync();

            var errores = new List<string>(); //Lista para acumular errores por ejemplo falta de stock

            // Recorre los productos que se desean agregar
            foreach (var item in productos)
            {
                // Busca el producto en base de dato
                var producto = await _context.Productos.FindAsync(item.ProductoId);
                if (producto == null)
                {
                    errores.Add($"Producto ID {item.ProductoId} no encontrado.");
                    continue;
                }
                // Verifica que haya suficiente stock
                if (producto.Stock < item.Cantidad)
                {
                    errores.Add($"{producto.Nombre}: Stock insuficiente. Solo hay {producto.Stock} unidades disponibles.");
                    continue;
                }

                decimal precio = 0;
                long? productoId = item.ProductoId;
                long? presentacionId = item.PresentacionId;
                // Si tiene presentacion usa su precio
                if (presentacionId.HasValue)
                {
                    var presentacion = await _context.Presentaciones.FirstOrDefaultAsync(p => p.Id == presentacionId);
                    if (presentacion == null) continue;
                    precio = presentacion.PrecioVenta;
                    productoId = presentacion.ProductoId;
                }
                else
                {
                    precio = producto.PrecioCompra; // Si no tiene presentación usa el precio del producto.
                }
                //verifica si el detalle ya existe en la cuenta
                var detalleExistente = detallesExistentes.FirstOrDefault(d =>
                    d.ProductoId == productoId &&
                    d.PresentacionId == presentacionId);

                if (detalleExistente != null)
                {
                    // Si ya existe aumenta la cantidad
                    detalleExistente.Cantidad += item.Cantidad;
                    cuenta.Total += precio * item.Cantidad;
                    producto.Stock -= item.Cantidad;
                }
                else
                {
                    // Si no existe crea un nuevo detalle
                    producto.Stock -= item.Cantidad;

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
            }
            // Si hubo errores los devuelve al frontend
            if (errores.Count > 0)
            {
                return new JsonResult(new
                {
                    success = false,
                    errores = errores
                });
            }
            // Guarda los cambios en la base de datos
            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }

        // Clase auxiliar usada para recibir productos desde el frontend en JSON
        public class DetalleCuentaTemp
        {
            public long ProductoId { get; set; }
            public long? PresentacionId { get; set; }
            public int Cantidad { get; set; }
        }

        // Metodo POST que actualiza la cantidad de un detalle existente
        public async Task<IActionResult> OnPostActualizarDetalleAsync(long id, [FromBody] DetalleUpdateRequest req)
        {
            //validación básica de los datos recibidos
            if (req == null || req.DetalleId <= 0 || req.Cantidad < 1)
                return new JsonResult(new { success = false, message = "Datos inválidos." });

            // Busca el detalle correspondiente a la cuenta
            var detalle = await _context.DetalleCuenta
                .Include(d => d.Cuenta)
                .Include(d => d.Producto)
                .FirstOrDefaultAsync(d => d.Id == req.DetalleId && d.CuentaId == id);

            if (detalle == null)
                return new JsonResult(new { success = false, message = "Detalle no encontrado." });

            // Calcula la diferencia entre la cantidad nueva y la anterior
            int diferencia = req.Cantidad - detalle.Cantidad;

            // Si se aumenta la cantidad:
            if (diferencia > 0)
            {
                var producto = detalle.Producto ?? await _context.Productos.FindAsync(detalle.ProductoId);

                //Verifica si hay suficiente stock
                if (producto != null && producto.Stock < diferencia)
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = $"Stock insuficiente. Solo hay {producto.Stock} unidades disponibles.",
                        stockDisponible = producto.Stock + detalle.Cantidad
                    });
                }
                // Resta del stock la diferencia
                if (producto != null)
                {
                    producto.Stock -= diferencia;
                }
            }
            // Si se disminuye la cantidad:
            else if (diferencia < 0)
            {
                var producto = detalle.Producto ?? await _context.Productos.FindAsync(detalle.ProductoId);
                if (producto != null)
                {
                    producto.Stock += Math.Abs(diferencia);// Devuelve las unidades al stock
                }
            }
            // Actualiza el total de la cuenta.
            detalle.Cuenta.Total -= detalle.PrecioUnitario * detalle.Cantidad;
            detalle.Cantidad = req.Cantidad;
            var nuevoSubtotal = detalle.PrecioUnitario * detalle.Cantidad;
            detalle.Cuenta.Total += nuevoSubtotal;

            await _context.SaveChangesAsync();

            // Devuelve al frontend el nuevo subtotal y stock actualizado.
            var prod = detalle.Producto ?? await _context.Productos.FindAsync(detalle.ProductoId);
            var stockDisponibleTotal = prod != null ? prod.Stock + detalle.Cantidad : 999;

            return new JsonResult(new
            {
                success = true,
                detalleId = detalle.Id,
                nuevoSubtotal = nuevoSubtotal,
                nuevoTotal = detalle.Cuenta.Total,
                stockDisponible = stockDisponibleTotal
            });
        }
        // Método POST que elimina un detalle (producto) de la cuenta
        public async Task<IActionResult> OnPostEliminarDetalleAsync(long id, [FromBody] DetalleDeleteRequest req)
        {
            if (req == null || req.DetalleId <= 0)
                return new JsonResult(new { success = false, message = "Datos inválidos." });

            // Busca el detalle a eliminar
            var detalle = await _context.DetalleCuenta
                .Include(d => d.Cuenta)
                .Include(d => d.Producto)
                .FirstOrDefaultAsync(d => d.Id == req.DetalleId && d.CuentaId == id);

            if (detalle == null)
                return new JsonResult(new { success = false, message = "Detalle no encontrado." });

            // Devuelve la cantidad eliminada al stock
            var producto = detalle.Producto ?? await _context.Productos.FindAsync(detalle.ProductoId);
            if (producto != null)
            {
                producto.Stock += detalle.Cantidad;
            }
            //resta el subtotal del total de la cuenta
            detalle.Cuenta.Total -= detalle.PrecioUnitario * detalle.Cantidad;

            // Elimina el detalle de la base de datos.
            _context.DetalleCuenta.Remove(detalle);
            await _context.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                detalleId = detalle.Id,
                nuevoTotal = detalle.Cuenta.Total
            });
        }

        // Clase auxiliar para actualizar detalles
        public class DetalleUpdateRequest
        {
            public long DetalleId { get; set; }
            public int Cantidad { get; set; }
        }

        // Clase auxiliar para eliminar detalles.
        public class DetalleDeleteRequest
        {
            public long DetalleId { get; set; }
        }

        // Metodo POST que cierra una cuenta (finaliza el pedido y registra el pago)
        public async Task<IActionResult> OnPostCerrarCuentaAsync(string metodoPago, decimal montoPagado, decimal vuelto)
        {
            if (Cuenta == null || Cuenta.Id <= 0)
                return BadRequest("Cuenta no válida.");
            // Guarda la configuración regional actual 
            var originalCulture = Thread.CurrentThread.CurrentCulture;
            var originalUICulture = Thread.CurrentThread.CurrentUICulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            try
            {
                // Busca la cuenta en la base de datos junto con sus detalles.
                var cuenta = await _context.Cuentas
                    .Include(c => c.DetalleCuenta)
                    .FirstOrDefaultAsync(c => c.Id == Cuenta.Id);

                if (cuenta == null)
                    return NotFound("Cuenta no encontrada.");

                if (cuenta.Estado == "Cerrada")
                    return BadRequest("La cuenta ya está cerrada.");
         
                //Calcula el total actual de la cuenta.
                var total = cuenta.DetalleCuenta.Sum(d => d.Cantidad * d.PrecioUnitario);

                //Verifica que el monto pagado sea suficiente
                if (decimal.Round(montoPagado, 2) + 0.001m < decimal.Round(total, 2))
                    return BadRequest("El monto pagado no puede ser menor al total.");

                // Actualiza los datos finales de la cuenta.
                cuenta.MetodoPago = metodoPago;
                cuenta.MontoPagado = montoPagado;
                cuenta.Vuelto = vuelto;
                cuenta.Total = total;
                cuenta.Estado = "Cerrada";

                // Guarda los cambios.
                _context.Cuentas.Update(cuenta);
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true, message = "Cuenta cerrada correctamente." });
            }
            finally
            {
                // Restaura la configuración cultural original
                Thread.CurrentThread.CurrentCulture = originalCulture;
                Thread.CurrentThread.CurrentUICulture = originalUICulture;
            }
        }
    }
}