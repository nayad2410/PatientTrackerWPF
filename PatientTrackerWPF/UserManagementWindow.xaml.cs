using Microsoft.EntityFrameworkCore;
using PatientTrackerWPF.Data;
using PatientTrackerWPF.Models;
using PatientTrackerWPF.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PatientTrackerWPF
{
    public partial class UserManagementWindow : Window
    {
        private readonly AuthenticationService _authService;
        private readonly AuditService _auditService;
        private readonly AppDbContext _dbContext;
        private List<User> _users = new List<User>();

        public UserManagementWindow(AuthenticationService authService, AuditService auditService, AppDbContext dbContext)
        {
            InitializeComponent();
            _authService = authService;
            _auditService = auditService;
            _dbContext = dbContext;

            // Set default role
            RoleComboBox.SelectedIndex = 2; // Nurse by default

            _ = LoadUsersAsync();
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                _users = await _dbContext.Users
                    .OrderBy(u => u.Username)
                    .ToListAsync();

                UsersGrid.ItemsSource = null;
                UsersGrid.ItemsSource = _users;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading users: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddUser_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(UsernameBox.Text) ||
                string.IsNullOrWhiteSpace(FullNameBox.Text) ||
                string.IsNullOrWhiteSpace(EmailBox.Text) ||
                RoleComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please fill in all fields.", "Validation Error",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Validate email format
                if (!IsValidEmail(EmailBox.Text))
                {
                    MessageBox.Show("Please enter a valid email address.", "Invalid Email",
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedRole = (RoleComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                var defaultPassword = "Welcome123!"; // Default password

                // Create user
                var result = await _authService.CreateUserAsync(
                    UsernameBox.Text.Trim(),
                    FullNameBox.Text.Trim(),
                    EmailBox.Text.Trim(),
                    defaultPassword,
                    selectedRole,
                    _authService.GetCurrentUsername()
                );

                if (result.Success)
                {
                    await _auditService.LogActionAsync("CREATE_USER", null,
                        $"Created user account: {UsernameBox.Text}");

                    MessageBox.Show($"User '{UsernameBox.Text}' created successfully!\n\n" +
                                   $"Default Password: {defaultPassword}\n\n" +
                                   "Please inform the user to change their password on first login.",
                                   "User Created", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Clear form
                    UsernameBox.Clear();
                    FullNameBox.Clear();
                    EmailBox.Clear();
                    RoleComboBox.SelectedIndex = 2; // Reset to Nurse

                    // Reload users
                    await LoadUsersAsync();
                }
                else
                {
                    MessageBox.Show($"Failed to create user: {result.Message}", "Error",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating user: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var user = button?.Tag as User;

            if (user == null) return;

            // Prevent deleting yourself
            if (user.Username == _authService.GetCurrentUsername())
            {
                MessageBox.Show("You cannot delete your own account while logged in.",
                               "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Prevent deleting the last admin
            if (user.Role == "Admin")
            {
                var adminCount = _users.Count(u => u.Role == "Admin" && u.IsActive);
                if (adminCount <= 1)
                {
                    MessageBox.Show("Cannot delete the last administrator account.",
                                   "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Confirm deletion
            var result = MessageBox.Show(
                $"Are you sure you want to delete the user account:\n\n" +
                $"Username: {user.Username}\n" +
                $"Full Name: {user.FullName}\n" +
                $"Role: {user.Role}\n\n" +
                "This action cannot be undone.",
                "Confirm Delete User",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Remove user from database
                    _dbContext.Users.Remove(user);
                    await _dbContext.SaveChangesAsync();

                    await _auditService.LogActionAsync("DELETE_USER", null,
                        $"Deleted user account: {user.Username}");

                    MessageBox.Show($"User '{user.Username}' deleted successfully.",
                                   "User Deleted", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Reload users
                    await LoadUsersAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting user: {ex.Message}", "Error",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var user = button?.Tag as User;

            if (user == null) return;

            var result = MessageBox.Show(
                $"Reset password for user '{user.Username}'?\n\n" +
                "The new password will be: Welcome123!\n\n" +
                "The user will need to change this on next login.",
                "Reset Password",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Reset password
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Welcome123!", workFactor: 12);
                    user.UpdatedAt = DateTime.UtcNow;

                    await _dbContext.SaveChangesAsync();

                    await _auditService.LogActionAsync("RESET_PASSWORD", null,
                        $"Reset password for user: {user.Username}");

                    MessageBox.Show($"Password reset successfully for '{user.Username}'.\n\n" +
                                   "New Password: Welcome123!",
                                   "Password Reset", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error resetting password: {ex.Message}", "Error",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void RefreshUsers_Click(object sender, RoutedEventArgs e)
        {
            await LoadUsersAsync();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}