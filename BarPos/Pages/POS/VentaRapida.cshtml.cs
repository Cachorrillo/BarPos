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
                    p.EsLicor,
                    p.MlRestantesBotellaAbierta,
                    // ?? Si tiene botellas o ml restantes, puede venderse
                    PuedeVender = p.EsLicor
                        ? (p.Stock > 0 || p.MlRestantesBotellaAbierta > 0)
                        : p.Stock > 0,
                    Precio = p.PrecioCompra
                })
                .ToListAsync();

            return new JsonResult(productos);
        }


        // ============================================================
        //  Mostrar presentaciones + información extendida de inventario
        // ============================================================
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
                    // ?? Identificamos si es licor
                    EsLicor = pr.Producto.EsLicor,

                    // ?? Mostramos texto de stock según el tipo de producto
                    StockTexto = pr.Producto.EsLicor
                        ? $"{pr.Producto.Stock} botellas ({pr.Producto.MlRestantesBotellaAbierta} ml restantes)"
                        : $"{pr.Producto.Stock} unidades",

                    // ?? Campos adicionales (para flexibilidad en front)
                    StockBotellas = pr.Producto.Stock,
                    MlRestantes = pr.Producto.MlRestantesBotellaAbierta
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

                // =====================================================
                // ?? VALIDACIÓN DE STOCK GENERAL (UNIDADES o MILILITROS)
                // =====================================================
                foreach (var it in req.Items)
                {
                    var producto = await _context.Productos
                        .Include(p => p.Presentaciones)
                        .FirstOrDefaultAsync(p => p.Id == it.ProductoId);

                    if (producto == null)
                    {
                        errores.Add($"Producto ID {it.ProductoId} no encontrado.");
                        continue;
                    }

                    // ========================
                    // ?? LICORES (control por ml)
                    // ========================
                    if (producto.EsLicor && producto.MililitrosPorBotella.HasValue)
                    {
                        // Obtener presentación para saber cuántos ml se servirán
                        var presentacion = await _context.Presentaciones
                            .FirstOrDefaultAsync(p => p.Id == it.PresentacionId);

                        if (presentacion == null || !presentacion.CantidadEquivalente.HasValue)
                        {
                            errores.Add($"{producto.Nombre}: No se encontró la presentación o cantidad de ml no válida.");
                            continue;
                        }

                        int mlPorTrago = presentacion.CantidadEquivalente.Value;
                        int totalMlConsumidos = mlPorTrago * it.Cantidad;
                        int mlPorBotella = producto.MililitrosPorBotella.Value;
                        int mlRestantes = producto.MlRestantesBotellaAbierta;

                        // Cálculo total de mililitros disponibles
                        int mlTotalesDisponibles = (producto.Stock * mlPorBotella) + mlRestantes;

                        if (totalMlConsumidos > mlTotalesDisponibles)
                        {
                            errores.Add($"{producto.Nombre}: Stock insuficiente de botellas o mililitros. Solo quedan {mlTotalesDisponibles} ml.");
                        }
                    }
                    else
                    {
                        // ========================
                        // ?? PRODUCTOS NORMALES
                        // ========================
                        if (producto.Stock < it.Cantidad)
                            errores.Add($"{producto.Nombre}: Stock insuficiente. Solo hay {producto.Stock} unidades.");
                    }
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

                    // ============================
                    //   DETERMINAR PRECIO VENTA
                    // ============================
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

                    // ============================
                    //   CONTROL DE STOCK
                    // ============================
                    if (producto.EsLicor && producto.MililitrosPorBotella.HasValue)
                    {
                        // 1?? Obtener la presentación (para saber cuántos ml se sirven)
                        var presentacion = await _context.Presentaciones
                            .FirstOrDefaultAsync(p => p.Id == presentacionId);

                        if (presentacion == null)
                        {
                            errores.Add($"{producto.Nombre}: No se encontró la presentación.");
                            continue;
                        }

                        // Validar que tenga CantidadEquivalente definida
                        int mlPorTrago = presentacion.CantidadEquivalente ?? 0;
                        if (mlPorTrago <= 0)
                        {
                            errores.Add($"{producto.Nombre}: la presentación '{presentacion.Nombre}' no tiene definida la cantidad equivalente (ml).");
                            continue;
                        }

                        // 2?? Calcular total de mililitros vendidos
                        int totalMlConsumidos = mlPorTrago * it.Cantidad;
                        int mlPorBotella = producto.MililitrosPorBotella.Value;
                        int mlRestantes = producto.MlRestantesBotellaAbierta;

                        // =====================================================
                        // 3?? Usar primero la botella abierta (si hay)
                        // =====================================================
                        if (mlRestantes > 0)
                        {
                            if (totalMlConsumidos <= mlRestantes)
                            {
                                // Todo se consume de la botella abierta
                                producto.MlRestantesBotellaAbierta -= totalMlConsumidos;
                                totalMlConsumidos = 0;
                            }
                            else
                            {
                                // Se consume lo que quedaba y seguimos con botellas nuevas
                                totalMlConsumidos -= mlRestantes;
                                producto.MlRestantesBotellaAbierta = 0;
                            }
                        }

                        // =====================================================
                        // 4?? Abrir nuevas botellas si aún faltan mililitros
                        // =====================================================
                        if (totalMlConsumidos > 0)
                        {
                            int botellasCompletas = totalMlConsumidos / mlPorBotella;
                            int mlRestoNuevaBotella = totalMlConsumidos % mlPorBotella;

                            // Restar las botellas completas
                            producto.Stock -= botellasCompletas;

                            // Si hay resto, abrir una nueva botella y dejarla abierta con su remanente
                            if (mlRestoNuevaBotella > 0)
                            {
                                if (producto.Stock <= 0)
                                {
                                    errores.Add($"{producto.Nombre}: No hay botellas suficientes para cubrir la venta.");
                                    producto.MlRestantesBotellaAbierta = 0;
                                    continue;
                                }

                                producto.Stock -= 1;
                                producto.MlRestantesBotellaAbierta = mlPorBotella - mlRestoNuevaBotella;
                            }
                            else
                            {
                                producto.MlRestantesBotellaAbierta = 0;
                            }
                        }

                        // =====================================================
                        // 5?? Umbral mínimo de botella (para marcarla vacía)
                        // =====================================================
                        // Se evalúa en memoria para evitar error LINQ
                        var presentacionesProducto = (await _context.Presentaciones
                            .Where(p => p.ProductoId == producto.Id && p.CantidadEquivalente != null && p.CantidadEquivalente > 0)
                            .ToListAsync())
                            .AsEnumerable(); // ?? evaluación en memoria segura

                        int mlTragoMinimo = presentacionesProducto.Any()
                            ? presentacionesProducto.Min(p => p.CantidadEquivalente!.Value)
                            : 0;

                        int umbralCambioBotella = mlTragoMinimo; // puedes usar % si prefieres

                        if (umbralCambioBotella > 0 &&
                            producto.MlRestantesBotellaAbierta > 0 &&
                            producto.MlRestantesBotellaAbierta < umbralCambioBotella)
                        {
                            // Consideramos la botella vacía
                            producto.MlRestantesBotellaAbierta = 0;

                            if (producto.Stock > 0)
                                producto.Stock -= 1;
                        }

                        // =====================================================
                        // 6?? Validar que no haya stock negativo y guardar cambios
                        // =====================================================
                        if (producto.Stock < 0)
                        {
                            errores.Add($"{producto.Nombre}: Stock insuficiente de botellas.");
                            continue;
                        }

                        _context.Productos.Update(producto);
                    }
                    else
                    {
                        // =====================================================
                        // PRODUCTO NORMAL (sin control por mililitros)
                        // =====================================================
                        if (producto.Stock < it.Cantidad)
                        {
                            errores.Add($"{producto.Nombre}: Stock insuficiente al confirmar.");
                            continue;
                        }

                        producto.Stock -= it.Cantidad;
                        _context.Productos.Update(producto);
                    }

                    // =====================================================
                    // 6?? AGREGAR DETALLE DE CUENTA Y CALCULAR TOTAL
                    // =====================================================
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
