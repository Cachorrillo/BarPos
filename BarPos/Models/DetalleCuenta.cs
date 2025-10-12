using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarPos.Models;

public partial class DetalleCuenta
{
    public long Id { get; set; }

    public long CuentaId { get; set; }

    public long? PresentacionId { get; set; }

    public long? ProductoId { get; set; }      // ✅ Nuevo campo para productos sin presentación

    public int Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public decimal Subtotal { get; set; }

    public virtual Cuenta Cuenta { get; set; } = null!;

    public virtual Presentacion? Presentacion { get; set; } = null!;

    public virtual Producto? Producto { get; set; } // ✅ Nuevo enlace directo al producto
}
