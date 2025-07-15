using PatientTrackerWPF.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PatientTrackerWPF
{
    /// <summary>
    /// Interaction logic for PasswordResetWindow.xaml
    /// </summary>
    public partial class PasswordResetWindow : Window
    {
        private readonly AuthenticationService authService;
        private bool isResetMode = false;
        private string currentEmail = "";

        public PasswordResetWindow()
        {
            InitializeComponent();
            authService = App.GetService<AuthenticationService>();
            // Set focus to email box
            Loaded += (s, e) => EmailTextBox.Focus();

            // Handle Enter key events
            EmailTextBox.KeyDown += EmailTextBox_KeyDown;
            ResetTokenTextBox.KeyDown += ResetTokenTextBox_KeyDown;
            NewPasswordBox.KeyDown += NewPasswordBox_KeyDown;
            ConfirmPasswordBox.KeyDown += ConfirmPasswordBox_KeyDown;
        }
        private void EmailTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !isResetMode)
            {
                SendResetEmail_Click(sender, e);
            }
        }

        private void ResetTokenTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NewPasswordBox.Focus();
            }
        }

        private void NewPasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmPasswordBox.Focus();
            }
        }

        private void ConfirmPasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && isResetMode)
            {
                ResetPassword_Click(sender, e);
            }
        }

        private async void SendResetEmail_Click(object sender, RoutedEventArgs e)
        {
            var email = EmailTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                ShowStatus("Please enter your email address.", isError: true);
                EmailTextBox.Focus();
                return;
            }

            if (!IsValidEmail(email))
            {
                ShowStatus("Please enter a valid email address.", isError: true);
                EmailTextBox.Focus();
                return;
            }

            try
            {
                ShowLoading(true);
                ShowStatus("Sending password reset email...", isError: false);

                var result = await authService.ResetPasswordAsync(email);

                if (result.Success)
                {
                    currentEmail = email;
                    ShowStatus("Password reset email sent! Check your email for the reset token.", isError: false);

                    // Switch to reset mode
                    SwitchToResetMode();
                }
                else
                {
                    ShowStatus(result.Message, isError: true);
                    EmailTextBox.Focus();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error sending reset email: {ex.Message}", isError: true);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            var token = ResetTokenTextBox.Text.Trim();
            var newPassword = NewPasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(token))
            {
                ShowStatus("Please enter the reset token from your email.", isError: true);
                ResetTokenTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                ShowStatus("Please enter a new password.", isError: true);
                NewPasswordBox.Focus();
                return;
            }

            if (newPassword != confirmPassword)
            {
                ShowStatus("Passwords do not match.", isError: true);
                ConfirmPasswordBox.Focus();
                return;
            }

            if (!IsValidPassword(newPassword))
            {
                ShowStatus("Password must be at least 8 characters long and contain uppercase, lowercase, number, and special character.", isError: true);
                NewPasswordBox.Focus();
                return;
            }

            try
            {
                ShowLoading(true);
                ShowStatus("Resetting password...", isError: false);

                var result = await authService.ChangePasswordWithTokenAsync(currentEmail, token, newPassword);

                if (result.Success)
                {
                    ShowStatus("Password reset successfully! You can now login with your new password.", isError: false);

                    // Small delay to show success message
                    await Task.Delay(2000);

                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowStatus(result.Message, isError: true);
                    ResetTokenTextBox.Focus();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error resetting password: {ex.Message}", isError: true);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void SwitchToResetMode()
        {
            isResetMode = true;

            // Hide initial form elements
            InitialFormPanel.Visibility = Visibility.Collapsed;

            // Show reset form elements
            ResetFormPanel.Visibility = Visibility.Visible;

            // Update instructions
            InstructionsText.Text = $"Enter the reset token sent to {currentEmail} and choose a new password:";

            // Focus on token field
            ResetTokenTextBox.Focus();
        }

        private void BackToEmail_Click(object sender, RoutedEventArgs e)
        {
            isResetMode = false;
            currentEmail = "";

            // Show initial form elements
            InitialFormPanel.Visibility = Visibility.Visible;

            // Hide reset form elements
            ResetFormPanel.Visibility = Visibility.Collapsed;

            // Reset instructions
            InstructionsText.Text = "Enter your email address to receive a password reset link:";

            // Clear fields
            ResetTokenTextBox.Clear();
            NewPasswordBox.Clear();
            ConfirmPasswordBox.Clear();
            StatusMessageText.Text = "";

            // Focus on email field
            EmailTextBox.Focus();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowStatus(string message, bool isError)
        {
            StatusMessageText.Text = message;
            StatusMessageText.Foreground = isError ?
                System.Windows.Media.Brushes.Red :
                System.Windows.Media.Brushes.Green;
        }

        private void ShowLoading(bool isLoading)
        {
            LoadingGrid.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            SendResetEmailBtn.IsEnabled = !isLoading;
            ResetPasswordBtn.IsEnabled = !isLoading;
            EmailTextBox.IsEnabled = !isLoading;
            ResetTokenTextBox.IsEnabled = !isLoading;
            NewPasswordBox.IsEnabled = !isLoading;
            ConfirmPasswordBox.IsEnabled = !isLoading;
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

        private bool IsValidPassword(string password)
        {
            if (password.Length < 8) return false;

            bool hasUpper = false, hasLower = false, hasDigit = false, hasSpecial = false;

            foreach (char c in password)
            {
                if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsLower(c)) hasLower = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else if (!char.IsLetterOrDigit(c)) hasSpecial = true;
            }

            return hasUpper && hasLower && hasDigit && hasSpecial;
        }
    }

}

