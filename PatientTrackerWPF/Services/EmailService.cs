using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading.Tasks;
using System.Web;

namespace PatientTrackerWPF.Services
{
    public class EmailService
    {
        private readonly ILogger<EmailService> _logger;

        private readonly string smtpServer;
        private readonly int smtpPort;
        private readonly bool enableSsl;
        private readonly string emailUsername;
        private readonly string emailPassword;
        private readonly string fromEmail;
        private readonly string fromName;
        private readonly string resetLinkBaseUrl;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _logger = logger;

            var emailSettings = configuration.GetSection("EmailSettings");

            smtpServer = emailSettings["SmtpServer"] ?? throw new Exception("EmailSettings:SmtpServer missing");
            smtpPort = int.TryParse(emailSettings["SmtpPort"], out var p) ? p : throw new Exception("EmailSettings:SmtpPort missing/invalid");
            enableSsl = bool.TryParse(emailSettings["EnableSsl"], out var ssl) && ssl;
            emailUsername = emailSettings["Username"] ?? throw new Exception("EmailSettings:Username missing");
            emailPassword = emailSettings["Password"] ?? throw new Exception("EmailSettings:Password missing");
            fromEmail = emailSettings["FromEmail"] ?? throw new Exception("EmailSettings:FromEmail missing");
            fromName  = emailSettings["FromName"]  ?? "Reconnect Progress Tracker";
            resetLinkBaseUrl = emailSettings["ResetLinkBaseUrl"] ?? "https://localhost/reset-password";

            // For older runtimes: ensure TLS 1.2 (ignored on modern .NET)
            try
            {
#pragma warning disable SYSLIB0039
                System.Net.ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
#pragma warning restore SYSLIB0039
            }
            catch { /* ignore */ }
        }

        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetToken, string fullName)
        {
            try
            {
                // Build a usable link from your config base URL
                // (URL-encode both token and email)
                var link = $"{resetLinkBaseUrl}?token={HttpUtility.UrlEncode(resetToken)}&email={HttpUtility.UrlEncode(toEmail)}";

                var subject = "Password Reset Request - Reconnect Progress Tracker";
                var body = GeneratePasswordResetEmailBody(fullName, resetToken, link);

                await SendAsync(toEmail, subject, body);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmailService: failed to send reset email to {Email}", toEmail);
                return false;
            }
        }

        public async Task<bool> SendAccountCreatedEmailAsync(string toEmail, string username, string fullName, string? temporaryPassword = null)
        {
            try
            {
                var subject = "Account Created - Reconnect Progress Tracker";
                var body = GenerateAccountCreatedEmailBody(fullName, username, temporaryPassword ?? "");

                await SendAsync(toEmail, subject, body);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmailService: failed to send account-created email to {Email}", toEmail);
                return false;
            }
        }

        private async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            // O365 requirement: From should equal the authenticated user
            var from = new MailAddress(fromEmail, fromName);
            var to = new MailAddress(toEmail);

            using var message = new MailMessage(from, to)
            {
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                EnableSsl = enableSsl,              // true for 587 (STARTTLS) with Office 365
                Credentials = new NetworkCredential(emailUsername, emailPassword),
                Timeout = 30000                     // 30s
            };

            try
            {
                await client.SendMailAsync(message);
                _logger.LogInformation("EmailService: sent \"{Subject}\" to {Email}", subject, toEmail);
            }
            catch (SmtpException smtpex)
            {
                _logger.LogError(smtpex,
                    "SMTP failed (StatusCode={StatusCode}) sending to {Email}",
                    smtpex.StatusCode,
                    toEmail);

                if (smtpex.InnerException != null)
                {
                    _logger.LogError("Inner exception: {InnerType} - {InnerMessage}",
                        smtpex.InnerException.GetType().Name,
                        smtpex.InnerException.Message);
                }

                // Optional: Also write to Debug output for quick viewing
                System.Diagnostics.Debug.WriteLine(
                    $"SMTP failed: {smtpex.StatusCode} {smtpex.Message}");
                if (smtpex.InnerException != null)
                    System.Diagnostics.Debug.WriteLine(
                        $"Inner: {smtpex.InnerException.GetType().Name}: {smtpex.InnerException.Message}");

                throw; // keep throwing if you want higher-level handlers to catch it
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP failed sending to {Email}", toEmail);
                throw;
            }
        }

        private string GeneratePasswordResetEmailBody(string fullName, string resetToken, string resetLink)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'/>
  <style>
    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
    .header {{ background-color: #1e3a8a; color: white; padding: 20px; text-align: center; }}
    .content {{ padding: 20px; background-color: #f9f9f9; }}
    .token-box {{ background-color: #e3f2fd; padding: 15px; margin: 20px 0; border-left: 4px solid #2196f3; }}
    .button {{ display: inline-block; padding: 12px 24px; background-color: #1e3a8a; color: white; text-decoration: none; border-radius: 5px; }}
    .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
  </style>
</head>
<body>
  <div class='container'>
    <div class='header'>
      <h1>Password Reset Request</h1>
      <p>Reconnect Progress Tracker</p>
    </div>
    <div class='content'>
      <h2>Hello {WebUtility.HtmlEncode(fullName)},</h2>
      <p>We received a request to reset your password.</p>
      <div class='token-box'>
        <strong>Reset Token:</strong> <code>{WebUtility.HtmlEncode(resetToken)}</code>
      </div>
      <p>You can also click the button below to reset your password:</p>
      <p><a class='button' href='{resetLink}'>Reset Password</a></p>
      <p><strong>Important:</strong> This token expires in 24 hours.</p>
    </div>
    <div class='footer'>This is an automated message. Please do not reply.</div>
  </div>
</body>
</html>";
        }

        private string GenerateAccountCreatedEmailBody(string fullName, string username, string temporaryPassword)
        {
            var passwordSection = string.IsNullOrWhiteSpace(temporaryPassword)
                ? "<p>You set your password during registration.</p>"
                : $@"<div class='token-box'>
                        <strong>Temporary Password:</strong> <code>{WebUtility.HtmlEncode(temporaryPassword)}</code>
                        <br><strong>Important:</strong> Change this after your first login.
                     </div>";

            return $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'/>
  <style>
    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
    .header {{ background-color: #16a085; color: white; padding: 20px; text-align: center; }}
    .content {{ padding: 20px; background-color: #f9f9f9; }}
    .token-box {{ background-color: #e8f5e8; padding: 15px; margin: 20px 0; border-left: 4px solid #16a085; }}
    .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
  </style>
</head>
<body>
  <div class='container'>
    <div class='header'>
      <h1>Welcome to Reconnect Progress Tracker</h1>
    </div>
    <div class='content'>
      <h2>Hello {WebUtility.HtmlEncode(fullName)},</h2>
      <p>Your account has been created.</p>
      <p><strong>Username:</strong> <code>{WebUtility.HtmlEncode(username)}</code></p>
      {passwordSection}
    </div>
    <div class='footer'>This is an automated message. Please do not reply.</div>
  </div>
</body>
</html>";
        }
    }
}
