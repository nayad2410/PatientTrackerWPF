using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PatientTrackerWPF.Data;
using PatientTrackerWPF.Models;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PatientTrackerWPF.Services
{
    public class AuditService
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AuditService> _logger;

        public AuditService(ICurrentUserService currentUserService, IServiceProvider serviceProvider, ILogger<AuditService> logger)
        {
            _currentUserService = currentUserService;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task LogActionAsync(string action, string? patientId = null, string? details = null)
        {
            try
            {
                var currentUser = _currentUserService.CurrentUser;
                if (currentUser == null)
                {
                    _logger.LogWarning("No current user for audit logging");
                    return;
                }

                // Get properly configured DbContext from DI container
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var auditLog = new AuditLog
                {
                    Timestamp = DateTime.UtcNow,
                    UserId = currentUser.Id,
                    Action = action,
                    PatientId = patientId,
                    IPAddress = GetLocalIPAddress(),
                    Details = details ?? $"{action} performed by {currentUser.Username}"
                };

                context.AuditLogs.Add(auditLog);
                await context.SaveChangesAsync();

                _logger.LogInformation("Audit logged: {Action} by {Username}", action, currentUser.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit logging failed");
            }
        }

        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                return host.AddressList.FirstOrDefault()?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}