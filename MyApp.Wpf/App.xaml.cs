using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using MyApp.Application;
using MyApp.Infrastructure;

namespace MyApp.Wpf;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureAppConfiguration(config => config.AddJsonFile("appsettings.Local.json", optional: true))
            .UseSerilog((context, logger) => logger
                .Enrich.WithProperty("Application", "MyApp")
                .WriteTo.Console()
                .WriteTo.Seq(context.Configuration["Seq:Url"]
                    ?? throw new InvalidOperationException("Seq:Url не задано.")))
            .ConfigureServices((context, services) => ConfigureServices(services))
            .Build();

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        await _host.StartAsync();
        _host.Services.GetRequiredService<MainWindow>().Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<MainWindow>();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception");
        MessageBox.Show("Сталася неочікувана помилка. Деталі записано в журнал.", "PulseForge", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}