using System;
using System.Collections.Generic;

namespace BarPos.Models;

public partial class MovimientosInventario
{
    public long Id { get; set; }

    public long ProductoId { get; set; }

    public string? TipoMovimiento { get; set; }

    public decimal Cantidad { get; set; }

    public DateTime? Fecha { get; set; }

    public string? Descripcion { get; set; }

    public virtual Producto Producto { get; set; } = null!;
}
