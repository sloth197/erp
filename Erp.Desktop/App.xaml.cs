using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Erp.Desktop.Services;
using Erp.Infrastructure.Extensions;
using Erp.Infrastructure.Persistence;
using Erp.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Erp.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private ILogger<App>? _logger;
    private IUserMessageService? _userMessageService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterGlobalExceptionHandlers();

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
                services.AddSingleton<IFileSaveDialogService, FileSaveDialogService>();
                services.AddSingleton<IItemCsvExportService, ItemCsvExportService>();
                services.AddSingleton<ICodeExplorerService, CodeExplorerService>();

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
                services.AddTransient<ViewModels.CodeExplorerViewModel>();
                services.AddTransient<ViewModels.WarehousesViewModel>();
                services.AddTransient<ViewModels.CodesViewModel>();
                services.AddTransient<ViewModels.InventoryOnHandViewModel>();
                services.AddTransient<ViewModels.StockReceiptViewModel>();
                services.AddTransient<ViewModels.StockIssueViewModel>();
                services.AddTransient<ViewModels.StockAdjustViewModel>();
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
            _logger = _host.Services.GetService<ILogger<App>>();
            _userMessageService = _host.Services.GetService<IUserMessageService>();
            await InitializeDatabaseAsync();
        }
        catch (Exception ex)
        {
            ReportUnhandledException("App.Startup", ex, showUserMessage: false);
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

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ReportUnhandledException("UI.DispatcherUnhandledException", e.Exception, showUserMessage: true);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ReportUnhandledException("AppDomain.UnhandledException", ex, showUserMessage: true);
        }
    }

    private void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ReportUnhandledException("TaskScheduler.UnobservedTaskException", e.Exception, showUserMessage: true);
        e.SetObserved();
    }

    private void ReportUnhandledException(string source, Exception exception, bool showUserMessage)
    {
        _logger?.LogError(exception, "Unhandled exception ({Source})", source);
        WriteFallbackErrorLog(source, exception);

        if (!showUserMessage || _userMessageService is null)
        {
            return;
        }

        try
        {
            if (Dispatcher.CheckAccess())
            {
                _userMessageService.ShowError("예상치 못한 오류가 발생했습니다. 로그를 확인하세요.");
            }
            else
            {
                Dispatcher.Invoke(() => _userMessageService.ShowError("예상치 못한 오류가 발생했습니다. 로그를 확인하세요."));
            }
        }
        catch
        {
            // Do not throw from global exception handlers.
        }
    }

    private static void WriteFallbackErrorLog(string source, Exception exception)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "ui-errors.log");
            var payload = $"[{DateTime.UtcNow:O}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(path, payload);
        }
        catch
        {
            // Ignore fallback logging failures.
        }
    }
}
