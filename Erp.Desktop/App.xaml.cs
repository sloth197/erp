using System;
using System.Windows;
using Erp.Desktop.Services;
using Erp.Infrastructure.Extensions;
using Erp.Infrastructure.Persistence;
using Erp.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Erp.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.SetBasePath(AppContext.BaseDirectory);
                configBuilder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddInfrastructure(context.Configuration);

                services.AddSingleton<IUserMessageService, UserMessageService>();

                services.AddSingleton<Navigation.INavigationService, Navigation.NavigationService>();

                services.AddSingleton<ViewModels.MainWindowViewModel>();

                services.AddTransient<ViewModels.LoginViewModel>();
                services.AddTransient<ViewModels.HomeViewModel>();
                services.AddTransient<ViewModels.ChangePasswordViewModel>();
                services.AddTransient<ViewModels.UsersManagementViewModel>();
                services.AddTransient<ViewModels.SettingsViewModel>();

                services.AddTransient<ViewModels.NoticesViewModel>();
                services.AddTransient<ViewModels.PartnersViewModel>();
                services.AddTransient<ViewModels.ItemsViewModel>();
                services.AddTransient<ViewModels.WarehousesViewModel>();
                services.AddTransient<ViewModels.CodesViewModel>();
                services.AddTransient<ViewModels.InventoryStockViewModel>();
                services.AddTransient<ViewModels.InventoryInOutViewModel>();
                services.AddTransient<ViewModels.InventoryAdjustmentViewModel>();
                services.AddTransient<ViewModels.PurchaseOrdersViewModel>();
                services.AddTransient<ViewModels.PurchaseReceiptViewModel>();
                services.AddTransient<ViewModels.SalesOrdersViewModel>();
                services.AddTransient<ViewModels.SalesRevenueViewModel>();
                services.AddTransient<ViewModels.AccountVouchersViewModel>();
                services.AddTransient<ViewModels.AccountReportsViewModel>();
                services.AddTransient<ViewModels.AuditLogViewModel>();

                services.AddSingleton<MainWindow>();
            })
            .Build();

        try
        {
            await _host.StartAsync();
            await InitializeDatabaseAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"애플리케이션 시작 실패: {ex.Message}", "ERP", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;

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

    private async Task InitializeDatabaseAsync()
    {
        if (_host is null)
        {
            throw new InvalidOperationException("Host is not initialized.");
        }

        using var scope = _host.Services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ErpDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
        await seeder.SeedAsync();
    }
}
