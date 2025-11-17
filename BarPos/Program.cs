using BarPos.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using BarPos.Services.Impresion;

var builder = WebApplication.CreateBuilder(args);

// Registrar CP850 (para acentos en impresión)
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Configurar base de datos
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configurar cultura global
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

// -------------------------------
// Configuración de sesiones
// -------------------------------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// -------------------------------
// Servicios
// -------------------------------
builder.Services.AddRazorPages();
builder.Services.AddScoped<ReceiptService>(); // servicio de impresión

var app = builder.Build();

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Activar sesiones
app.UseSession();

app.UseAuthorization();

// ------- ENDPOINT PARA IMPRESIÓN -------
app.MapGet("/api/impresion/cuenta/{id:long}", (long id, ReceiptService receiptService) =>
{
    try
    {
        var bytes = receiptService.GenerarRecibo(id);
        var base64 = Convert.ToBase64String(bytes);

        return Results.Json(new
        {
            success = true,
            data = base64
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            success = false,
            error = ex.Message
        });
    }
});
// ----------------------------------------

app.MapRazorPages();

app.Run();
