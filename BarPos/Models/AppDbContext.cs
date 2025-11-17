using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BarPos.Models;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Categoria> Categorias { get; set; }

    public virtual DbSet<Cuenta> Cuentas { get; set; }

    public virtual DbSet<DetalleCuenta> DetalleCuenta { get; set; }

    public virtual DbSet<MovimientosInventario> MovimientosInventarios { get; set; }

    public virtual DbSet<Presentacion> Presentaciones { get; set; }

    public virtual DbSet<Producto> Productos { get; set; }

    //    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
    //        => optionsBuilder.UseSqlServer("Server=VÍCTOR\\SQLEXPRESS;Database=BarPOS;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Categori__3214EC07BFF0842C");

            entity.Property(e => e.Nombre).HasMaxLength(50);
        });

        modelBuilder.Entity<Cuenta>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Cuentas__3214EC07DAB02C65");

            entity.Property(e => e.Estado).HasMaxLength(20);
            entity.Property(e => e.FechaApertura)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.MetodoPago).HasMaxLength(20);
            entity.Property(e => e.MontoPagado)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(10, 2)");
            entity.Property(e => e.NombreCliente).HasMaxLength(50);
            entity.Property(e => e.Total)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Vuelto)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(10, 2)");
        });

        modelBuilder.Entity<DetalleCuenta>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Cantidad).HasDefaultValue(1);

            // Relación obligatoria con Cuenta
            entity.HasOne(d => d.Cuenta)
                .WithMany(p => p.DetalleCuenta)
                .HasForeignKey(d => d.CuentaId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_DetalleCuenta_Cuentas");

            // Relación opcional con Presentación
            entity.HasOne(d => d.Presentacion)
                .WithMany(p => p.DetalleCuenta)
                .HasForeignKey(d => d.PresentacionId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false)
                .HasConstraintName("FK_DetalleCuenta_Presentaciones");

            // ✅ Nueva relación opcional directa con Producto
            entity.HasOne(d => d.Producto)
                .WithMany()
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false)
                .HasConstraintName("FK_DetalleCuenta_Productos");
        });



        modelBuilder.Entity<MovimientosInventario>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Movimien__3214EC076F7A600D");

            entity.ToTable("MovimientosInventario");

            entity.Property(e => e.Cantidad).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Descripcion).HasMaxLength(100);
            entity.Property(e => e.Fecha)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TipoMovimiento).HasMaxLength(20);

            entity.HasOne(d => d.Producto).WithMany(p => p.MovimientosInventario)
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MovimientosInventario_Productos");
        });

        modelBuilder.Entity<Presentacion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Presenta__3214EC07A2645688");

            entity.Property(e => e.Nombre).HasMaxLength(50);
            entity.Property(e => e.PrecioVenta).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Producto).WithMany(p => p.Presentaciones)
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Presentaciones_Productos");
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Producto__3214EC074A802B3B");

            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.PrecioCompra).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Categoria).WithMany(p => p.Productos)
                .HasForeignKey(d => d.CategoriaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Productos_Categorias");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
