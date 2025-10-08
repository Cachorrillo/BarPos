using System;
using System.Collections.Generic;

namespace BarPos.Models;

public partial class DetalleCuenta
{
    public long Id { get; set; }

    public long CuentaId { get; set; }

    public long PresentacionId { get; set; }

    public int Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    public decimal? Subtotal { get; set; }

    public virtual Cuenta Cuenta { get; set; } = null!;

    public virtual Presentacion Presentacion { get; set; } = null!;
}
