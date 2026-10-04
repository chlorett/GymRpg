using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithProperty("Application", "MyApp")
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();

try
{
    Log.Information("Application started");

    var userId = 42;
    var userName = "Sazerwar";
    Log.Information("User {UserId} logged in as {UserName}", userId, userName);

    Log.Warning("Order {OrderId} has low stock, quantity: {Quantity}", 101, 3);

    try
    {
        throw new InvalidOperationException("Test error");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to process order {OrderId}", 101);
    }
}
finally
{
    await Log.CloseAndFlushAsync();
}