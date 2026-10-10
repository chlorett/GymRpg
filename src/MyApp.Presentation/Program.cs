using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Serilog;
using MyApp.Infrastructure.Data; 

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithProperty("Application", "MyApp")
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();

try
{
    Log.Information("Application started");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    using var host = builder.Build();

    var userId = 42;
    var userName = "Sazerwar";
    Log.Information("User {UserId} logged in as {UserName}", userId, userName);

    Log.Warning("Order {OrderId} has low stock, quantity: {Quantity}", 101, 3);

}
catch (Exception ex)
{
    Log.Error(ex, "Критична помилка під час запуску");
}
finally
{
    Log.CloseAndFlush();
}