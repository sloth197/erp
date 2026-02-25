using System;
using System.Windows;
using Erp.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Erp.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Build Host (DI container)
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.SetBasePath(AppContext.BaseDirectory);
                configBuilder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddInfrastructure(context.Configuration);

                // Navigation
                services.AddSingleton<Navigation.INavigationService, Navigation.NavigationService>();

                // ViewModels
                services.AddSingleton<ViewModels.MainWindowViewModel>();
                services.AddSingleton<ViewModels.HomeViewModel>();

                // Main window
                services.AddSingleton<MainWindow>();
            })
            .Build();

        _host.Start();

        // Resolve and show MainWindow from DI
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;

        // Close app when MainWindow closes
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _host?.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
        }
        finally
        {
            _host?.Dispose();
        }

        base.OnExit(e);
    }
}
