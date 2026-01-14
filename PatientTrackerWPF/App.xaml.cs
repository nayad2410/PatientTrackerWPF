using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PatientTrackerWPF.Data;
using PatientTrackerWPF.Services;
using Serilog;
using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace PatientTrackerWPF
{
    public partial class App : Application
    {
        private IHost _host;

        public App()
        {
            // ✅ Configure Serilog BEFORE building the host
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PatientTracker", "Logs", "PatientTracker.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7
                )
                .CreateLogger();

            _host = Host.CreateDefaultBuilder()
                .UseSerilog() // ✅ Use Serilog as the logging provider
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    var configuration = context.Configuration;

                    // Register services
                    services.AddSingleton<ICurrentUserService, CurrentUserService>();

                    services.AddDbContext<AppDbContext>((serviceProvider, options) =>
                    {
                        var connectionString = configuration.GetConnectionString("PatientDb") ??
                                               configuration.GetConnectionString("DefaultConnection") ??
                                               "Server=localhost;Database=ReconnectMentalHealth-db;Trusted_Connection=true;TrustServerCertificate=true;";

                        options.UseSqlServer(connectionString, sql =>
                        {
                            sql.EnableRetryOnFailure(
                                maxRetryCount: 5,
                                maxRetryDelay: TimeSpan.FromSeconds(10),
                                errorNumbersToAdd: null);
                        });


#if DEBUG
                        options.EnableSensitiveDataLogging(true);
                        options.LogTo(message => System.Diagnostics.Debug.WriteLine($"EF: {message}"));
#endif
                    });

                    services.AddScoped<AuthenticationService>();
                    services.AddSingleton<EmailService>();
                    services.AddScoped<ClinicalMetricsService>();
                    services.AddScoped<RemissionTrackingService>();
                    services.AddSingleton<EncryptionService>();
                    services.AddScoped<AuditService>();

                    services.AddTransient<LoginWindow>();
                    services.AddTransient<MainWindow>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            await _host.StartAsync();

            try
            {
                using var scope = _host.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Database.MigrateAsync();

                var logger = scope.ServiceProvider.GetRequiredService<ILogger<App>>();
                logger.LogInformation("Application started successfully");
            }
            catch (Exception ex)
            {
                var logger = _host.Services.GetRequiredService<ILogger<App>>();
                logger?.LogError(ex, "Database initialization error");

                MessageBox.Show($"Database Connection Error:\n\n{ex.Message}",
                               "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            var loginWindow = _host.Services.GetRequiredService<LoginWindow>();
            loginWindow.Show();

            base.OnStartup(e);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException(e.ExceptionObject as Exception, "UnhandledException");
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception, "DispatcherUnhandledException");
            e.Handled = true;
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException(e.Exception, "UnobservedTaskException");
            e.SetObserved();
        }

        private void LogException(Exception ex, string source)
        {
            try
            {
                var logger = _host?.Services?.GetService<ILogger<App>>();
                logger?.LogError(ex, "Critical error from {Source}", source);
            }
            catch { }

            MessageBox.Show("An unexpected error occurred. Please contact support if this persists.",
                           "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();

            Log.CloseAndFlush(); // ✅ Clean up Serilog
            base.OnExit(e);
        }

        public static T GetService<T>() where T : class
        {
            return ((App)Current)._host.Services.GetRequiredService<T>();
        }
    }
}
