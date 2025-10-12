using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;

namespace BarPos.Models;

public partial class Cuenta
{
    public long Id { get; set; }

    public string NombreCliente { get; set; } = null!;

    public DateTime FechaApertura { get; set; }


    [ValidateNever]
    public string Estado { get; set; } = null!;

    public string? MetodoPago { get; set; }

    public decimal Total { get; set; }

    public decimal? MontoPagado { get; set; }

    public decimal? Vuelto { get; set; }

    public virtual ICollection<DetalleCuenta> DetalleCuenta { get; set; } = new List<DetalleCuenta>();
}
