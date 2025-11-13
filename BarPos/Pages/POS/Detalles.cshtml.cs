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
                    if (producto.EsLicor)
                    {
                        // Para licores: evitar validación de stock en el front
                        // Stock infinito para permitir sumar tragos sin límite visual
                        StockDisponible[detalle.Id] = 999999;
                    }
                    else
                    {
                        // Productos normales: stock real + lo que ya está en el pedido
                        StockDisponible[detalle.Id] = producto.Stock + detalle.Cantidad;
                    }
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
            // Buscar cuenta
            var cuenta = await _context.Cuentas.FindAsync(id);
            if (cuenta == null)
                return NotFound();

            var detallesExistentes = await _context.DetalleCuenta
                .Where(d => d.CuentaId == id)
                .ToListAsync();

            var errores = new List<string>();

            foreach (var item in productos)
            {
                var producto = await _context.Productos.FindAsync(item.ProductoId);
                if (producto == null)
                {
                    errores.Add($"Producto ID {item.ProductoId} no encontrado.");
                    continue;
                }

                long? presentacionId = item.PresentacionId;

                // ===============================================
                //    🟧 CONTROL DE STOCK PARA LICORES (ml)
                // ===============================================
                if (producto.EsLicor && presentacionId.HasValue)
                {
                    var presentacion = await _context.Presentaciones
                        .FirstOrDefaultAsync(p => p.Id == presentacionId.Value);

                    if (presentacion == null)
                    {
                        errores.Add($"{producto.Nombre}: Presentación no válida.");
                        continue;
                    }

                    // Usar lógica de botella virtual EXACTA de Venta Rápida
                    var resultado = await ProcesarLicorAsync(producto, presentacion, item.Cantidad);

                    if (!resultado.Success)
                    {
                        errores.Add(resultado.Error ?? $"Error al procesar licor '{producto.Nombre}'.");
                        continue;
                    }

                    decimal precio = presentacion.PrecioVenta;
                    long? productoIdFinal = presentacion.ProductoId;

                    // ¿Existe ya un detalle igual?
                    var detalleExistenteLicor = detallesExistentes.FirstOrDefault(d =>
                        d.ProductoId == productoIdFinal &&
                        d.PresentacionId == presentacionId);

                    if (detalleExistenteLicor != null)
                    {
                        detalleExistenteLicor.Cantidad += item.Cantidad;
                        cuenta.Total += precio * item.Cantidad;
                    }
                    else
                    {
                        var nuevoDetalle = new DetalleCuenta
                        {
                            CuentaId = cuenta.Id,
                            ProductoId = productoIdFinal,
                            PresentacionId = presentacionId,
                            Cantidad = item.Cantidad,
                            PrecioUnitario = precio
                        };

                        _context.DetalleCuenta.Add(nuevoDetalle);
                        cuenta.Total += precio * item.Cantidad;
                    }

                    // MUY IMPORTANTE → evitar que siga a la lógica normal
                    continue;
                }

                // ===============================================
                //    🟩 PRODUCTOS NORMALES (LO DE SIEMPRE)
                // ===============================================

                if (producto.Stock < item.Cantidad)
                {
                    errores.Add($"{producto.Nombre}: Stock insuficiente. Solo hay {producto.Stock} unidades disponibles.");
                    continue;
                }

                decimal precioNormal = 0;
                long? productoIdNormal = item.ProductoId;

                if (presentacionId.HasValue)
                {
                    var presentacion = await _context.Presentaciones.FirstOrDefaultAsync(p => p.Id == presentacionId);
                    if (presentacion == null) continue;

                    precioNormal = presentacion.PrecioVenta;
                    productoIdNormal = presentacion.ProductoId;
                }
                else
                {
                    precioNormal = producto.PrecioCompra;
                }

                var detalleExistente = detallesExistentes.FirstOrDefault(d =>
                    d.ProductoId == productoIdNormal &&
                    d.PresentacionId == presentacionId);

                if (detalleExistente != null)
                {
                    detalleExistente.Cantidad += item.Cantidad;
                    cuenta.Total += precioNormal * item.Cantidad;
                    producto.Stock -= item.Cantidad;
                }
                else
                {
                    producto.Stock -= item.Cantidad;

                    var nuevo = new DetalleCuenta
                    {
                        CuentaId = cuenta.Id,
                        ProductoId = productoIdNormal,
                        PresentacionId = presentacionId,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = precioNormal
                    };

                    _context.DetalleCuenta.Add(nuevo);
                    cuenta.Total += precioNormal * item.Cantidad;
                }
            }

            if (errores.Count > 0)
            {
                return new JsonResult(new
                {
                    success = false,
                    errores = errores
                });
            }

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


        // ===========================================================
        //   MÉTODO INTERNO PARA PROCESAR LICORES (BOTELLA VIRTUAL)
        //   Copiado y adaptado de Venta Rápida
        // ===========================================================
        private class ResultadoLicor
        {
            public bool Success { get; set; }
            public string? Error { get; set; }

            public int BotellasConsumidas { get; set; }
            public int MlConsumidosDeBotellaAbierta { get; set; }
            public int MlRestantesBotellaAbiertaFinal { get; set; }
        }

        private async Task<ResultadoLicor> ProcesarLicorAsync(
            Producto producto,
            Presentacion presentacion,
            int cantidadTragos)
        {
            var result = new ResultadoLicor { Success = false };

            if (producto == null || presentacion == null)
            {
                result.Error = "Producto o presentación inválidos.";
                return result;
            }

            if (!presentacion.CantidadEquivalente.HasValue || presentacion.CantidadEquivalente.Value <= 0)
            {
                result.Error = $"La presentación '{presentacion.Nombre}' no tiene definida CantidadEquivalente (ml).";
                return result;
            }

            if (!producto.MililitrosPorBotella.HasValue)
            {
                result.Error = $"El producto '{producto.Nombre}' no tiene definido MililitrosPorBotella.";
                return result;
            }

            int mlPorTrago = presentacion.CantidadEquivalente.Value;
            int totalMlConsumidos = mlPorTrago * cantidadTragos;

            int mlPorBotella = producto.MililitrosPorBotella.Value;
            int mlRestantes = producto.MlRestantesBotellaAbierta;

            // Cálculo total disponible
            int mlTotalesDisponibles = (producto.Stock * mlPorBotella) + mlRestantes;

            if (totalMlConsumidos > mlTotalesDisponibles)
            {
                result.Error = $"{producto.Nombre}: Stock insuficiente. Solo hay {mlTotalesDisponibles} ml disponibles.";
                return result;
            }

            // ===========================================================
            // 1️⃣ Consumir primero la botella abierta
            // ===========================================================
            int botellasConsumidas = 0;
            int mlConsumidosDeBotellaAbierta = 0;

            if (mlRestantes > 0)
            {
                if (totalMlConsumidos <= mlRestantes)
                {
                    producto.MlRestantesBotellaAbierta -= totalMlConsumidos;
                    mlConsumidosDeBotellaAbierta = totalMlConsumidos;
                    totalMlConsumidos = 0;
                }
                else
                {
                    totalMlConsumidos -= mlRestantes;
                    mlConsumidosDeBotellaAbierta = mlRestantes;
                    producto.MlRestantesBotellaAbierta = 0;
                }
            }

            // ===========================================================
            // 2️⃣ Consumir botellas nuevas si aún faltan ml
            // ===========================================================
            if (totalMlConsumidos > 0)
            {
                int botellasCompletas = totalMlConsumidos / mlPorBotella;
                int mlRestoNuevaBotella = totalMlConsumidos % mlPorBotella;

                // Consumir botellas enteras
                if (botellasCompletas > 0)
                {
                    producto.Stock -= botellasCompletas;
                    botellasConsumidas += botellasCompletas;
                }

                // Si sobran ml → abrir una botella nueva
                if (mlRestoNuevaBotella > 0)
                {
                    if (producto.Stock <= 0)
                    {
                        result.Error = $"{producto.Nombre}: No hay botellas suficientes para cubrir la venta.";
                        return result;
                    }

                    producto.Stock -= 1;
                    botellasConsumidas += 1;

                    producto.MlRestantesBotellaAbierta = mlPorBotella - mlRestoNuevaBotella;
                }
                else
                {
                    producto.MlRestantesBotellaAbierta = 0;
                }
            }

            // ===========================================================
            // 3️⃣ APLICAR UMBRAL (BOTELLAS CASI VACIAS)
            // ===========================================================
            var presentacionesProducto = (await _context.Presentaciones
                .Where(p => p.ProductoId == producto.Id &&
                            p.CantidadEquivalente != null &&
                            p.CantidadEquivalente > 0)
                .ToListAsync())
                .AsEnumerable();

            int mlTragoMinimo = presentacionesProducto.Any()
                ? presentacionesProducto.Min(p => p.CantidadEquivalente!.Value)
                : 0;

            if (mlTragoMinimo > 0 &&
                producto.MlRestantesBotellaAbierta > 0 &&
                producto.MlRestantesBotellaAbierta < mlTragoMinimo)
            {
                // Botella se considera vacía
                producto.MlRestantesBotellaAbierta = 0;

                if (producto.Stock > 0)
                {
                    producto.Stock -= 1;
                    botellasConsumidas += 1;
                }
            }

            // ===========================================================
            // ÉXITO
            // ===========================================================
            result.Success = true;
            result.BotellasConsumidas = botellasConsumidas;
            result.MlConsumidosDeBotellaAbierta = mlConsumidosDeBotellaAbierta;
            result.MlRestantesBotellaAbiertaFinal = producto.MlRestantesBotellaAbierta;

            return result;
        }


        // =======================================================
        //  🟦 Revertir tragos de licor (devolver ml al inventario)
        //  Lógica profesional igual a Venta Rápida pero en reversa
        // =======================================================
        private async Task<(bool Success, string? Error)> RevertirLicorAsync(
            Producto producto,
            Presentacion presentacion,
            int cantidad)
        {
            if (!producto.EsLicor ||
                !presentacion.CantidadEquivalente.HasValue ||
                !producto.MililitrosPorBotella.HasValue)
            {
                return (false, "Producto o presentación inválida para revertir licor.");
            }

            int mlPorTrago = presentacion.CantidadEquivalente.Value;
            int totalMlDevueltos = mlPorTrago * cantidad;
            int mlPorBotella = producto.MililitrosPorBotella.Value;

            // Sumamos los ml de los tragos eliminados a la botella abierta
            producto.MlRestantesBotellaAbierta += totalMlDevueltos;

            // ======================================================
            // 1️⃣ Si la botella abierta excede su capacidad → convertir en botellas
            // ======================================================
            if (producto.MlRestantesBotellaAbierta >= mlPorBotella)
            {
                int botellasRecuperadas = producto.MlRestantesBotellaAbierta / mlPorBotella;
                producto.Stock += botellasRecuperadas;

                // ml que quedan después de rellenar botellas completas
                producto.MlRestantesBotellaAbierta = producto.MlRestantesBotellaAbierta % mlPorBotella;
            }

            // ======================================================
            // 2️⃣ Aplicar el umbral (igual que en Venta Rápida)
            //    Si la botella abierta queda con pocos ml → considerarla vacía
            // ======================================================
            var presentacionesProducto = await _context.Presentaciones
                .Where(p => p.ProductoId == producto.Id &&
                            p.CantidadEquivalente.HasValue &&
                            p.CantidadEquivalente > 0)
                .ToListAsync();

            int mlTragoMinimo = presentacionesProducto.Any()
                ? presentacionesProducto.Min(p => p.CantidadEquivalente!.Value)
                : 0;

            int umbralCambioBotella = mlTragoMinimo;

            if (umbralCambioBotella > 0 &&
                producto.MlRestantesBotellaAbierta > 0 &&
                producto.MlRestantesBotellaAbierta < umbralCambioBotella)
            {
                // Botella demasiado vacía → descartarla
                producto.MlRestantesBotellaAbierta = 0;

                // NO restamos al stock (eso ya ocurrió cuando se consumió)
                // Solo se limpia el remanente inválido
            }

            // Validación final
            if (producto.Stock < 0)
            {
                return (false, "Inconsistencia detectada: stock negativo al revertir licor.");
            }

            _context.Productos.Update(producto);

            return (true, null);
        }










        // Metodo POST que actualiza la cantidad de un detalle existente
        public async Task<IActionResult> OnPostActualizarDetalleAsync(long id, [FromBody] DetalleUpdateRequest req)
        {
            if (req == null || req.DetalleId <= 0 || req.Cantidad < 1)
                return new JsonResult(new { success = false, message = "Datos inválidos." });

            var detalle = await _context.DetalleCuenta
                .Include(d => d.Cuenta)
                .Include(d => d.Producto)
                .Include(d => d.Presentacion)
                .FirstOrDefaultAsync(d => d.Id == req.DetalleId && d.CuentaId == id);

            if (detalle == null)
                return new JsonResult(new { success = false, message = "Detalle no encontrado." });

            int cantidadActual = detalle.Cantidad;
            int nuevaCantidad = req.Cantidad;
            int diferencia = nuevaCantidad - cantidadActual;

            var producto = detalle.Producto ?? await _context.Productos.FindAsync(detalle.ProductoId);

            if (producto == null)
                return new JsonResult(new { success = false, message = "Producto no encontrado." });

            var presentacion = detalle.Presentacion;

            // ============================================================
            //       🟧 CONTROL DE STOCK — LICORES (BOTELLA VIRTUAL)
            // ============================================================
            if (producto.EsLicor && presentacion != null && presentacion.CantidadEquivalente.HasValue)
            {
                if (diferencia > 0)
                {
                    // Incrementar tragos → consumir ml
                    var r = await ProcesarLicorAsync(producto, presentacion, diferencia);

                    if (!r.Success)
                    {
                        return new JsonResult(new
                        {
                            success = false,
                            message = r.Error ?? "No hay suficiente licor para aumentar la cantidad.",
                            stockDisponible = producto.Stock + detalle.Cantidad
                        });
                    }
                }
                else if (diferencia < 0)
                {
                    // Disminuir tragos → devolver ml
                    int tragosARevertir = Math.Abs(diferencia);

                    var r = await RevertirLicorAsync(producto, presentacion, tragosARevertir);

                    if (!r.Success)
                    {
                        return new JsonResult(new
                        {
                            success = false,
                            message = r.Error ?? "No se pudo revertir el consumo de licor."
                        });
                    }
                }

                // Actualizar total de cuenta
                detalle.Cuenta.Total -= detalle.PrecioUnitario * detalle.Cantidad;
                detalle.Cantidad = nuevaCantidad;
                var nuevoSubtotalLicor = detalle.PrecioUnitario * detalle.Cantidad;
                detalle.Cuenta.Total += nuevoSubtotalLicor;

                await _context.SaveChangesAsync();

                return new JsonResult(new
                {
                    success = true,
                    detalleId = detalle.Id,
                    nuevoSubtotal = nuevoSubtotalLicor,
                    nuevoTotal = detalle.Cuenta.Total,
                    stockDisponible = producto.Stock + detalle.Cantidad  // referencia visual
                });
            }

            // ============================================================
            //       🟩 PRODUCTOS NORMALES (LÓGICA ORIGINAL)
            // ============================================================
            if (diferencia > 0)
            {
                if (producto.Stock < diferencia)
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = $"Stock insuficiente. Solo hay {producto.Stock} unidades disponibles.",
                        stockDisponible = producto.Stock + detalle.Cantidad
                    });
                }

                producto.Stock -= diferencia;
            }
            else if (diferencia < 0)
            {
                producto.Stock += Math.Abs(diferencia);
            }

            // Actualizar totales
            detalle.Cuenta.Total -= detalle.PrecioUnitario * detalle.Cantidad;
            detalle.Cantidad = nuevaCantidad;
            var nuevoSubtotal = detalle.PrecioUnitario * detalle.Cantidad;
            detalle.Cuenta.Total += nuevoSubtotal;

            await _context.SaveChangesAsync();

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

            // Obtener detalle
            var detalle = await _context.DetalleCuenta
                .Include(d => d.Cuenta)
                .Include(d => d.Producto)
                .Include(d => d.Presentacion)
                .FirstOrDefaultAsync(d => d.Id == req.DetalleId && d.CuentaId == id);

            if (detalle == null)
                return new JsonResult(new { success = false, message = "Detalle no encontrado." });

            var producto = detalle.Producto ?? await _context.Productos.FindAsync(detalle.ProductoId);
            var presentacion = detalle.Presentacion;

            // ============================================================
            //       🟧 LICORES — DEVOLVER TODA LA CANTIDAD (BOTELLA VIRTUAL)
            // ============================================================
            if (producto.EsLicor && presentacion != null && presentacion.CantidadEquivalente.HasValue)
            {
                int tragosARevertir = detalle.Cantidad;

                var r = await RevertirLicorAsync(producto, presentacion, tragosARevertir);

                if (!r.Success)
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = r.Error ?? "No se pudo revertir el consumo del licor."
                    });
                }
            }
            else
            {
                // ============================================================
                //       🟩 PRODUCTOS NORMALES — LÓGICA ORIGINAL
                // ============================================================
                if (producto != null)
                {
                    producto.Stock += detalle.Cantidad;
                }
            }

            // Actualizar total de la cuenta
            detalle.Cuenta.Total -= detalle.PrecioUnitario * detalle.Cantidad;

            // Eliminar detalle
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