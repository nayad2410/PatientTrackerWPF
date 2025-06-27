using PatientTrackerWPF.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PatientTrackerWPF
{
    public partial class LoginWindow : Window
    {
        private readonly AuthenticationService authService;

        public LoginWindow()
        {
            InitializeComponent();
            authService = new AuthenticationService();

            // Set focus to username box
            Loaded += (s, e) => UsernameTextBox.Focus();

            // Handle Enter key in password box
            PasswordTextBox.KeyDown += PasswordTextBox_KeyDown;
            UsernameTextBox.KeyDown += UsernameTextBox_KeyDown;
        }

        private void UsernameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PasswordTextBox.Focus();
            }
        }

        private void PasswordTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoginBtn_Click(sender, e);
            }
        }

        private async void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text.Trim();
            var password = PasswordTextBox.Password;

            if (string.IsNullOrWhiteSpace(username))
            {
                ShowStatus("Please enter your username.", isError: true);
                UsernameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowStatus("Please enter your password.", isError: true);
                PasswordTextBox.Focus();
                return;
            }

            // TEMPORARY: For testing without database - remove when database is ready
            if (username == "test" && password == "test")
            {
                ShowStatus("Login successful!", isError: false);
                await Task.Delay(500);

                // Open main window without auth service
                var mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
                return;
            }

            // Real authentication when database is available
            await PerformLoginAsync(username, password);
        }

        private async Task PerformLoginAsync(string username, string password)
        {
            try
            {
                ShowLoading(true);
                ShowStatus("Authenticating...", isError: false);

                var result = await authService.LoginAsync(username, password);

                if (result.Success)
                {
                    ShowStatus($"Welcome, {result.User?.FullName}!", isError: false);

                    // Small delay to show success message
                    await Task.Delay(500);

                    // Open main window and close login window
                    var mainWindow = new MainWindow(authService);
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    ShowStatus(result.Message, isError: true);
                    PasswordTextBox.Clear();
                    PasswordTextBox.Focus();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Login error: {ex.Message}", isError: true);
                PasswordTextBox.Clear();
                PasswordTextBox.Focus();
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void ForgotPasswordBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var passwordResetWindow = new PasswordResetWindow();
                passwordResetWindow.Owner = this;
                var result = passwordResetWindow.ShowDialog();

                if (result == true)
                {
                    ShowStatus("Password has been reset successfully. Please login with your new password.", isError: false);
                    UsernameTextBox.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening password reset window: {ex.Message}",
                               "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateAccountBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var createAccountWindow = new CreateAccountWindow();
                createAccountWindow.Owner = this;
                var result = createAccountWindow.ShowDialog();

                if (result == true)
                {
                    ShowStatus("Account created successfully! Please login with your new credentials.", isError: false);
                    UsernameTextBox.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening account creation window: {ex.Message}",
                               "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            LoginBtn.IsEnabled = !isLoading;
            UsernameTextBox.IsEnabled = !isLoading;
            PasswordTextBox.IsEnabled = !isLoading;
            ForgotPasswordBtn.IsEnabled = !isLoading;
            CreateAccountBtn.IsEnabled = !isLoading;
        }
    }
}