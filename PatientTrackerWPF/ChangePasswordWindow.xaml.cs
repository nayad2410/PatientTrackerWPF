using PatientTrackerWPF.Services;
using System;
using System.Windows;
using System.Windows.Input;

namespace PatientTrackerWPF
{
    public partial class ChangePasswordWindow : Window
    {
        private readonly AuthenticationService _authService;

        public ChangePasswordWindow(AuthenticationService authService)
        {
            InitializeComponent();
            _authService = authService;

            // Set focus to current password box
            Loaded += (s, e) => CurrentPasswordBox.Focus();

            // Handle Enter key events for smoother UX
            CurrentPasswordBox.KeyDown += (s, e) => {
                if (e.Key == Key.Enter) NewPasswordBox.Focus();
            };
            NewPasswordBox.KeyDown += (s, e) => {
                if (e.Key == Key.Enter) ConfirmPasswordBox.Focus();
            };
            ConfirmPasswordBox.KeyDown += (s, e) => {
                if (e.Key == Key.Enter) ChangePassword_Click(s, e);
            };
        }

        private async void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            var currentPassword = CurrentPasswordBox.Password;
            var newPassword = NewPasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            // Validation
            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                ShowStatus("Please enter your current password.", isError: true);
                CurrentPasswordBox.Focus();
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
                ShowStatus("New passwords do not match.", isError: true);
                ConfirmPasswordBox.Focus();
                return;
            }

            if (currentPassword == newPassword)
            {
                ShowStatus("New password must be different from current password.", isError: true);
                NewPasswordBox.Focus();
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
                ShowStatus("Changing password...", isError: false);

                var result = await _authService.ChangePasswordAsync(currentPassword, newPassword);

                if (result.Success)
                {
                    ShowStatus("Password changed successfully!", isError: false);

                    // Small delay to show success message
                    await System.Threading.Tasks.Task.Delay(1500);

                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowStatus(result.Message, isError: true);
                    CurrentPasswordBox.Focus();
                    CurrentPasswordBox.SelectAll();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error changing password: {ex.Message}", isError: true);
            }
            finally
            {
                ShowLoading(false);
            }
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
            ChangePasswordBtn.IsEnabled = !isLoading;
            CancelBtn.IsEnabled = !isLoading;
            CurrentPasswordBox.IsEnabled = !isLoading;
            NewPasswordBox.IsEnabled = !isLoading;
            ConfirmPasswordBox.IsEnabled = !isLoading;
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