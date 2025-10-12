using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarPos.Models;

public partial class Presentacion
{
    [Key]
    public long Id { get; set; }

    public long ProductoId { get; set; }

    [StringLength(50)]
    public string Nombre { get; set; } = null!;

    [BindRequired]
    public decimal PrecioVenta { get; set; }

    public int? CantidadEquivalente { get; set; }


    public virtual ICollection<DetalleCuenta> DetalleCuenta { get; set; } = new List<DetalleCuenta>();

    [ForeignKey("ProductoId")]
    [InverseProperty("Presentaciones")]
    public virtual Producto Producto { get; set; } = null!;
}
