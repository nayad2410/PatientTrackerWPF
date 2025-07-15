using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using PatientTrackerWPF.Data;
using PatientTrackerWPF.Services;
using System.IO;

namespace PatientTrackerWPF
{
    public partial class App : Application
    {
        private IHost _host;

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Add configuration sources
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    // Get configuration
                    var configuration = context.Configuration;

                    // Register DbContext with connection string
                    services.AddDbContext<AppDbContext>(options =>
                    {
                        var connectionString = configuration.GetConnectionString("PatientDb") ??
                                             configuration.GetConnectionString("DefaultConnection") ??
                                             "Server=localhost;Database=ReconnectMentalHealth-db;Trusted_Connection=true;TrustServerCertificate=true;";
                        options.UseSqlServer(connectionString);
                    });

                    // Register core services
                    services.AddSingleton<EmailService>();
                    services.AddScoped<AuthenticationService>();
                    services.AddSingleton<ICurrentUserService, CurrentUserService>();

                    // Register additional services (uncomment as needed)
                    services.AddScoped<ClinicalMetricsService>();
                    services.AddScoped<RemissionTrackingService>();

                    // Register windows for DI
                    services.AddTransient<LoginWindow>();
                    services.AddTransient<MainWindow>();

                    // Optional: Register other services 
                    // services.AddSingleton<IPatientService, PatientService>();
                    // services.AddSingleton<IUserService, UserService>();
                    // services.AddSingleton<INavigationService, NavigationService>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await _host.StartAsync();

            // Ensure database is created
            try
            {
                using var scope = _host.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Database.EnsureCreatedAsync();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex.Message}");
            }

            // Resolve the LoginWindow using DI
            var loginWindow = _host.Services.GetRequiredService<LoginWindow>();
            loginWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();
            base.OnExit(e);
        }

        // Helper method to get services from anywhere in the app
        public static T GetService<T>() where T : class
        {
            return ((App)Current)._host.Services.GetRequiredService<T>();
        }
    }
}