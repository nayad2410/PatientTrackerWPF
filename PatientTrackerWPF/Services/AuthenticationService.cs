using PatientTrackerWPF.Data;
using PatientTrackerWPF.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading;


namespace PatientTrackerWPF.Services
{
    public class AuthenticationService
    {
        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 30;

        public User? CurrentUser { get; private set; }

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

                // Check if account is locked
                if (user.IsLocked)
                {
                    return new AuthResult { Success = false, Message = $"Account is locked until {user.LockedUntil:yyyy-MM-dd HH:mm}." };
                }

                // Verify password
                if (!VerifyPassword(password, user.PasswordHash, user.Salt))
                {
                    // Increment failed attempts
                    user.FailedLoginAttempts++;

                    if (user.FailedLoginAttempts >= MaxFailedAttempts)
                    {
                        user.LockedUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                    }

                    await context.SaveChangesAsync();

                    return new AuthResult { Success = false, Message = "Invalid username or password." };
                }

                // Successful login - reset failed attempts and update last login
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

                // Check if username already exists
                if (await context.Users.AnyAsync(u => u.Username == username))
                {
                    return new AuthResult { Success = false, Message = "Username already exists." };
                }

                // Check if email already exists
                if (await context.Users.AnyAsync(u => u.Email == email))
                {
                    return new AuthResult { Success = false, Message = "Email already exists." };
                }

                var (hash, salt) = HashPassword(password);

                var user = new User
                {
                    Username = username,
                    FullName = fullName,
                    Email = email,
                    PasswordHash = hash,
                    Salt = salt,
                    Role = role,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow
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
                {
                    return new AuthResult { Success = false, Message = "No account found with that email address." };
                }

                // Generate reset token
                user.PasswordResetToken = GenerateResetToken();
                user.PasswordResetExpires = DateTime.UtcNow.AddHours(24); // Token expires in 24 hours

                await context.SaveChangesAsync();

                // In a real application, you would send an email here
                // For now, we'll just show the token (for demo purposes)
                return new AuthResult
                {
                    Success = true,
                    Message = $"Password reset initiated. Reset token: {user.PasswordResetToken}\n(In production, this would be sent via email)"
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
                {
                    return new AuthResult { Success = false, Message = "Invalid or expired reset token." };
                }

                var (hash, salt) = HashPassword(newPassword);
                user.PasswordHash = hash;
                user.Salt = salt;
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

        public void Logout()
        {
            CurrentUser = null;
        }

        public bool IsAuthenticated => CurrentUser != null;

        public bool HasRole(string role)
        {
            return CurrentUser?.Role == role;
        }

        public string GetCurrentUsername()
        {
            return CurrentUser?.Username ?? "Unknown";
        }

        public string GetCurrentUserFullName()
        {
            return CurrentUser?.FullName ?? "Unknown User";
        }

        private (string hash, string salt) HashPassword(string password)
        {
            var salt = GenerateSalt();
            var hash = HashPassword(password, salt);
            return (hash, salt);
        }

        private string HashPassword(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            var saltedPassword = password + salt;
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
            return Convert.ToBase64String(hashBytes);
        }

        private bool VerifyPassword(string password, string hash, string salt)
        {
            var computedHash = HashPassword(password, salt);
            return computedHash == hash;
        }

        private string GenerateSalt()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private string GenerateResetToken()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")[..32];
        }
    }
}