using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace PatientTrackerWPF.Services
{
    public class EmailService
    {
        private readonly string smtpServer;
        private readonly int smtpPort;
        private readonly bool enableSsl;
        private readonly string emailUsername;
        private readonly string emailPassword;
        private readonly string fromEmail;
        private readonly string fromName;
        private readonly string resetLinkBaseUrl;

        public EmailService(IConfiguration configuration)
        {
            var emailSettings = configuration.GetSection("EmailSettings");
            smtpServer = emailSettings["SmtpServer"];
            smtpPort = int.Parse(emailSettings["SmtpPort"]);
            enableSsl = bool.Parse(emailSettings["EnableSsl"]);
            emailUsername = emailSettings["Username"];
            emailPassword = emailSettings["Password"];
            fromEmail = emailSettings["FromEmail"];
            fromName = emailSettings["FromName"];
            resetLinkBaseUrl = emailSettings["ResetLinkBaseUrl"];
        }
        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetToken, string fullName)
        {
            try
            {
                var resetLink = $"https://your-domain.com/reset-password?token={resetToken}&email={Uri.EscapeDataString(toEmail)}";

                var subject = "Password Reset Request - Reconnect Progress Tracker";
                var body = GeneratePasswordResetEmailBody(fullName, resetToken, resetLink);

                using var message = new MailMessage();
                message.From = new MailAddress(fromEmail, fromName);
                message.To.Add(new MailAddress(toEmail));
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

                using var client = new SmtpClient(smtpServer, smtpPort);
                client.EnableSsl = true;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(emailUsername, emailPassword);

                await client.SendMailAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine($"Email sending failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendAccountCreatedEmailAsync(string toEmail, string username, string fullName, string temporaryPassword = null)
        {
            try
            {
                var subject = "Account Created - Reconnect Progress Tracker";
                var body = GenerateAccountCreatedEmailBody(fullName, username, temporaryPassword);

                using var message = new MailMessage();
                message.From = new MailAddress(fromEmail, fromName);
                message.To.Add(new MailAddress(toEmail));
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

                using var client = new SmtpClient(smtpServer, smtpPort);
                client.EnableSsl = true;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(emailUsername, emailPassword);

                await client.SendMailAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine($"Email sending failed: {ex.Message}");
                return false;
            }
        }

        private string GeneratePasswordResetEmailBody(string fullName, string resetToken, string resetLink)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
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
            <h2>Hello {fullName},</h2>
            
            <p>We received a request to reset your password for your Reconnect Progress Tracker account.</p>
            
            <div class='token-box'>
                <strong>Reset Token:</strong> <code>{resetToken}</code>
            </div>
            
            <p>To reset your password:</p>
            <ol>
                <li>Open the Reconnect Progress Tracker application</li>
                <li>Click ""Forgot Password"" on the login screen</li>
                <li>Enter your email address</li>
                <li>Click ""Send Reset Email""</li>
                <li>Enter the reset token above</li>
                <li>Choose your new password</li>
            </ol>
            
            <p><strong>Important:</strong> This reset token will expire in 24 hours for security reasons.</p>
            
            <p>If you didn't request this password reset, please ignore this email or contact your system administrator.</p>
        </div>
        
        <div class='footer'>
            <p>This is an automated message from Reconnect Progress Tracker.<br>
            Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateAccountCreatedEmailBody(string fullName, string username, string temporaryPassword)
        {
            var passwordSection = string.IsNullOrEmpty(temporaryPassword)
                ? "<p>You have set your own password during registration.</p>"
                : $@"<div class='token-box'>
                        <strong>Temporary Password:</strong> <code>{temporaryPassword}</code>
                        <br><strong>Important:</strong> Please change this password immediately after your first login.
                     </div>";

            return $@"
<!DOCTYPE html>
<html>
<head>
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
            <p>Your Account Has Been Created</p>
        </div>
        
        <div class='content'>
            <h2>Hello {fullName},</h2>
            
            <p>Your account has been successfully created for the Reconnect Progress Tracker system.</p>
            
            <p><strong>Username:</strong> <code>{username}</code></p>
            
            {passwordSection}
            
            <p>You can now login to the application using your credentials.</p>
            
            <p><strong>Getting Started:</strong></p>
            <ul>
                <li>Open the Reconnect Progress Tracker application</li>
                <li>Enter your username and password</li>
                <li>Complete your profile information if needed</li>
                <li>If using a temporary password, change it immediately</li>
            </ul>
            
            <p>If you have any questions or need assistance, please contact your system administrator.</p>
        </div>
        
        <div class='footer'>
            <p>This is an automated message from Reconnect Progress Tracker.<br>
            Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}