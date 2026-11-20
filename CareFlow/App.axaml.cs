using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CareFlow.Data;
using CareFlow.Services;
using CareFlow.ViewModels;
using CareFlow.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CareFlow;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Ensure database is migrated and initialized with seed data
        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CareFlowDbContext>();
            dbContext.Database.Migrate();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };

            _ = mainVm.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Database configuration: locate in base directory or current directory
        string dbPath = Path.Combine(AppContext.BaseDirectory, "careflow.db");
        if (!File.Exists(dbPath) && File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "careflow.db")))
        {
            dbPath = Path.Combine(Directory.GetCurrentDirectory(), "careflow.db");
        }

        services.AddDbContext<CareFlowDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Application Services
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IDiagnosticCatalogService, DiagnosticCatalogService>();
        services.AddScoped<IBookingService, BookingService>();

        // ViewModels
        services.AddTransient<NewBookingViewModel>();
        services.AddTransient<ServiceAvailabilityViewModel>();
        services.AddTransient<BookingHistoryViewModel>();
        services.AddTransient<VerificationViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }
}
