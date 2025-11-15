using System;
using System.Linq;
using BarPos.Models;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Services.Impresion
{
    public class ReceiptService
    {
        private readonly AppDbContext _context;

        public ReceiptService(AppDbContext context)
        {
            _context = context;
        }

        public byte[] GenerarRecibo(long cuentaId)
        {
            var cuenta = _context.Cuentas
                .Include(c => c.DetalleCuenta)
                    .ThenInclude(d => d.Producto)
                .Include(c => c.DetalleCuenta)
                    .ThenInclude(d => d.Presentacion)
                .FirstOrDefault(c => c.Id == cuentaId);

            if (cuenta == null)
                throw new Exception($"No se encontró la cuenta con ID {cuentaId}");

            var detalles = cuenta.DetalleCuenta
                .OrderBy(d => d.Id)
                .ToList();

            var builder = new ReceiptBuilder();

            return builder.BuildReceipt(cuenta, detalles);
        }
    }
}
