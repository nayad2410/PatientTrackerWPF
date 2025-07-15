using PatientTrackerWPF.Data;
using PatientTrackerWPF.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BCrypt.Net;

namespace PatientTrackerWPF.Services
{
    public class AuthenticationService
    {
        private readonly EmailService _emailService;
        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 30;

        public User? CurrentUser { get; private set; }

        public AuthenticationService(EmailService emailService)
        {
            _emailService = emailService;
        }
        public class AuthResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public User? User { get; set; }
        }

        public async Task<AuthResult> LoginAsync(string username, string password)
        {
            try
            {
                using var context = new AppDbContext();
                var user = await context.Users
                    .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

                if (user == null)
                {
                    return new AuthResult { Success = false, Message = "Invalid username or password." };
                }

                if (user.IsLocked)
                {
                    return new AuthResult { Success = false, Message = $"Account is locked until {user.LockedUntil:yyyy-MM-dd HH:mm}." };
                }

                if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    user.FailedLoginAttempts++;

                    if (user.FailedLoginAttempts >= MaxFailedAttempts)
                    {
                        user.LockedUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                    }

                    await context.SaveChangesAsync();

                    return new AuthResult { Success = false, Message = "Invalid username or password." };
                }

                user.FailedLoginAttempts = 0;
                user.LockedUntil = null;
                user.LastLogin = DateTime.UtcNow;
                await context.SaveChangesAsync();

                CurrentUser = user;

                return new AuthResult { Success = true, Message = "Login successful.", User = user };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, Message = $"Login error: {ex.Message}" };
            }
        }

        public async Task<AuthResult> CreateUserAsync(string username, string fullName, string email, string password, string role = "User", string createdBy = "System")
        {
            try
            {
                using var context = new AppDbContext();

                if (await context.Users.AnyAsync(u => u.Username == username))
                    return new AuthResult { Success = false, Message = "Username already exists." };

                if (await context.Users.AnyAsync(u => u.Email == email))
                    return new AuthResult { Success = false, Message = "Email already exists." };

                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

                var user = new User
                {
                    Username = username,
                    FullName = fullName,
                    Email = email,
                    PasswordHash = hashedPassword,
                    Role = role
                };

                context.Users.Add(user);
                await context.SaveChangesAsync();

                return new AuthResult { Success = true, Message = "User created successfully.", User = user };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, Message = $"Error creating user: {ex.Message}" };
            }
        }

        public async Task<AuthResult> ResetPasswordAsync(string email)
        {
            try
            {
                using var context = new AppDbContext();
                var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

                if (user == null)
                    return new AuthResult { Success = false, Message = "No account found with that email address." };

                user.PasswordResetToken = GenerateResetToken();
                user.PasswordResetExpires = DateTime.UtcNow.AddHours(24);

                await context.SaveChangesAsync();

                bool emailSent = await _emailService.SendPasswordResetEmailAsync(user.Email, user.PasswordResetToken, user.FullName);

                if (!emailSent)
                {
                    return new AuthResult
                    {
                        Success = false,
                        Message = "Reset token generated but email failed to send."
                    };
                }

                return new AuthResult
                {
                    Success = true,
                    Message = "Password reset email sent successfully."
                };

            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, Message = $"Error initiating password reset: {ex.Message}" };
            }
        }

        public async Task<AuthResult> ChangePasswordWithTokenAsync(string email, string resetToken, string newPassword)
        {
            try
            {
                using var context = new AppDbContext();
                var user = await context.Users.FirstOrDefaultAsync(u =>
                    u.Email == email &&
                    u.PasswordResetToken == resetToken &&
                    u.PasswordResetExpires > DateTime.UtcNow);

                if (user == null)
                    return new AuthResult { Success = false, Message = "Invalid or expired reset token." };

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
                user.PasswordResetToken = null;
                user.PasswordResetExpires = null;
                user.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();

                return new AuthResult { Success = true, Message = "Password reset successfully." };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, Message = $"Error resetting password: {ex.Message}" };
            }
        }

        public async Task<AuthResult> ChangePasswordAsync(string currentPassword, string newPassword)
        {
            if (CurrentUser == null)
                return new AuthResult { Success = false, Message = "No user is currently logged in." };

            try
            {
                using var context = new AppDbContext();
                var user = await context.Users.FindAsync(CurrentUser.Id);

                if (user == null)
                    return new AuthResult { Success = false, Message = "User not found." };

                if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                    return new AuthResult { Success = false, Message = "Current password is incorrect." };

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
                user.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();

                return new AuthResult { Success = true, Message = "Password changed successfully." };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, Message = $"Error changing password: {ex.Message}" };
            }
        }

        public async Task<bool> MigrateUserPasswordToBCryptAsync(string username, string plainTextPassword)
        {
            try
            {
                using var context = new AppDbContext();
                var user = await context.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null) return false;

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainTextPassword, workFactor: 12);
                user.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Logout()
        {
            CurrentUser = null;
        }

        public bool IsAuthenticated => CurrentUser != null;

        public bool HasRole(string role) => CurrentUser?.Role == role;

        public string GetCurrentUsername() => CurrentUser?.Username ?? "Unknown";

        public string GetCurrentUserFullName() => CurrentUser?.FullName ?? CurrentUser?.Username ?? "Unknown User";

        private string GenerateResetToken()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")[..32];
        }
    }
}
