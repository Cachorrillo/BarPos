using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarPos.Models;

public partial class Producto
{
    [Key]
    public long Id { get; set; }

    [StringLength(100)]
    public string Nombre { get; set; } = null!;

    [BindRequired]
    public decimal PrecioCompra { get; set; }

    public int Stock { get; set; }

    public long CategoriaId { get; set; }

    [ValidateNever]
    public virtual Categoria Categoria { get; set; } = null!;

    [InverseProperty("Producto")]
    public virtual ICollection<Presentacion> Presentaciones { get; set; } = new List<Presentacion>();
}
