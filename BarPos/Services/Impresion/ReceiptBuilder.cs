using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BarPos.Models;

namespace BarPos.Services.Impresion
{
    public class ReceiptBuilder
    {
        private const int LineWidth = 40;
        private readonly StringBuilder _sb = new();
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        public ReceiptBuilder()
        {
            _sb.Append("\x1B@"); // Reset ESC/POS
        }

        private void Line(string t = "") => _sb.Append(t + "\n");
        private void Raw(string t) => _sb.Append(t);

        private IEnumerable<string> Wrap(string text, int width)
        {
            text ??= "";
            while (text.Length > width)
            {
                yield return text[..width];
                text = text[width..];
            }
            yield return text;
        }

        private void Center(string text)
        {
            foreach (string line in Wrap(text, LineWidth))
            {
                int pad = (LineWidth - line.Length) / 2;
                if (pad < 0) pad = 0;
                Line(new string(' ', pad) + line);
            }
        }

        private void Separator() => Line(new string('-', LineWidth));

        private void ItemLine(int qty, string desc, decimal total)
        {
            string qtyPart = qty.ToString().PadLeft(2) + " "; // " 1 "
            string totalStr = total.ToString("N2", _culture).PadLeft(10);

            int descWidth = LineWidth - qtyPart.Length - totalStr.Length;
            if (descWidth < 5) descWidth = 5;

            if (desc.Length > descWidth)
                desc = desc[..descWidth];

            string line = qtyPart + desc.PadRight(descWidth) + totalStr;
            Line(line);
        }

        private void TotalLine(string label, decimal amount)
        {
            label = label.Length > 20 ? label[..20] : label;
            string monto = amount.ToString("N2", _culture);

            int spaces = LineWidth - label.Length - monto.Length;
            if (spaces < 2) spaces = 2;

            Line(label + new string(' ', spaces) + monto);
        }

        public byte[] BuildReceipt(Cuenta cuenta, List<DetalleCuenta> detalles)
        {
            // ----- ENCABEZADO -----
            Center("BAR EL RANCHITO");
            Center("Confecciones San Martín de Heredia S.A.");
            Center("Cédula jurídica: 3-101-345679");
            Center("A tu lado desde 1956");
            Center("125 suroeste antiguo hospital de Heredia");
            Line();

            // ----- FECHA / HORA -----
            string fecha = cuenta.FechaApertura.ToString("dd/MM/yyyy");
            string hora = cuenta.FechaApertura.ToString("hh:mm tt", CultureInfo.InvariantCulture);

            string left = $"Fecha : {fecha}";
            string right = $"Hora : {hora}";
            int esp = LineWidth - left.Length - right.Length;
            if (esp < 1) esp = 1;

            Line(left + new string(' ', esp) + right);

            // ----- CLIENTE -----
            Line($"Cliente: {cuenta.NombreCliente}");
            Separator();

            // ----- DETALLES -----
            decimal subtotal = 0;

            foreach (var det in detalles)
            {
                string descripcion =
                    det.Presentacion != null
                    ? $"{det.Producto?.Nombre} {det.Presentacion.Nombre}"
                    : det.Producto?.Nombre ?? "Producto";

                subtotal += det.Subtotal;

                ItemLine(det.Cantidad, descripcion, det.Subtotal);
            }

            Separator();

            // ----- TOTALES -----
            TotalLine("Sub total", subtotal);
            TotalLine("Total", cuenta.Total);
            Line();

            // ----- MÉTODO DE PAGO -----
            if (!string.IsNullOrWhiteSpace(cuenta.MetodoPago))
            {
                Line($"Método de pago: {cuenta.MetodoPago}");

                if (cuenta.MetodoPago == "Efectivo")
                {
                    TotalLine("Efectivo", cuenta.MontoPagado ?? 0);
                    TotalLine("Vuelto", cuenta.Vuelto ?? 0);
                }

                Line();
            }

            // ----- ATIENDE -----
            Line("Le atiende: Personal del bar");
            Line();
            Line();

            // ----- ESTADO -----
            if (cuenta.Estado == "Abierta")
                Center("C U E N T A   P E N D I E N T E");
            else
                Center("P A G A D O");

            Line();
            Line();

            // ----- PIE -----
            Center("Sistema BarPOS");
            Line();
            Line();
            Line();

            // Corte de papel ESC/POS
            Raw("\x1D\x56\x00");

            return Encoding.GetEncoding(850).GetBytes(_sb.ToString());
        }
    }
}
