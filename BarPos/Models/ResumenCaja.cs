namespace BarPos.Models
{
    public class ResumenCaja
    {
        public DateTime Fecha { get; set; }
        public int TotalCuentas { get; set; }
        public decimal TotalVentas { get; set; }
        public decimal VentasEfectivo { get; set; }
        public decimal VentasTarjeta { get; set; }
        public List<ProductoVendido> ProductosMasVendidos { get; set; } = new();
        public List<ProductoVendido> ProductosMenosVendidos { get; set; } = new();
    }

    public class ProductoVendido
    {
        public string NombreProducto { get; set; } = "";
        public string? Presentacion { get; set; }
        public int CantidadVendida { get; set; }
        public decimal TotalVentas { get; set; }
    }
}
