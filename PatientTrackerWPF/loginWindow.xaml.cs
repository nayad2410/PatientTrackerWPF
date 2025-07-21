using PatientTrackerWPF.Constants;
using PatientTrackerWPF.Services;
using PatientTrackerWPF.Utilities;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PatientTrackerWPF
{
    public partial class LoginWindow : Window
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly AuthenticationService _authService;

        // DI Constructor
        public LoginWindow(ICurrentUserService currentUserService, AuthenticationService authService)
        {
            InitializeComponent();
            _currentUserService = currentUserService;
            _authService = authService;

            // Show researcher presentation information
            ShowResearcherPresentationInfo();

            // TEMPORARY: Generate fresh admin hash
            /*      _authService.GenerateAndTestAdminHash();*/

            Loaded += (s, e) => UsernameTextBox.Focus();
            PasswordTextBox.KeyDown += PasswordTextBox_KeyDown;
            UsernameTextBox.KeyDown += UsernameTextBox_KeyDown;
        }

        private void ShowResearcherPresentationInfo()
        {
            var presentationInfo = ResearcherPresentationManager.GetResearcherPresentationInfo();

            // Check if TestAccountsText exists, if not, create a simple message
            // You might need to add this TextBlock to your XAML or remove this if not needed
            try
            {
                var testAccountsText = FindName("TestAccountsText") as TextBlock;
                if (testAccountsText != null)
                {
                    testAccountsText.Text = presentationInfo;
                }
                else
                {
                    // If the element doesn't exist, just log the info
                    System.Diagnostics.Debug.WriteLine("Researcher presentation info: " + presentationInfo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting presentation info: {ex.Message}");
            }
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
            try
            {
                // FIXED: Use the correct element names from your XAML
                var username = UsernameTextBox.Text.Trim();
                var password = PasswordTextBox.Password;

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Please enter both username and password.",
                                   "Login Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = await _authService.LoginAsync(username, password);

                if (result.Success)
                {
                    // Check if this is a researcher account
                    if (result.User?.Role == UserRoles.Researcher)
                    {
                        var welcomeResult = MessageBox.Show(
                            "🎯 RESEARCHER ACCOUNT LOGIN\n\n" +
                            "Perfect choice for presentations!\n\n" +
                            "Your researcher account provides:\n" +
                            "✅ Full access to all analytical features\n" +
                            "✅ Professional report generation\n" +
                            "✅ Data export capabilities\n" +
                            "✅ Clinical metrics and outcomes\n" +
                            "✅ Read-only data protection\n\n" +
                            "Ready to showcase the system's research capabilities?",
                            "Researcher Account - Presentation Ready",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }

                    // Check if this is a test account (if any exist)
                    if (result.User?.Role == UserRoles.Test)
                    {
                        var demoResult = MessageBox.Show(
                            "🎭 TEST ACCOUNT DETECTED\n\n" +
                            "You are logging in with a test account.\n" +
                            "This is perfect for demonstrations.\n\n" +
                            "• No real patient data will be saved\n" +
                            "• All features are available for demonstration\n" +
                            "• Data will reset when you restart the application\n\n" +
                            "Continue with test login?",
                            "Test Account Login",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (demoResult == MessageBoxResult.No)
                        {
                            _authService.Logout();
                            return;
                        }
                    }

                    // Set current user for the service
                    _currentUserService.SetCurrentUser(result.User);

                    // Show main window
                    var mainWindow = App.GetService<MainWindow>();
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show(result.Message, "Login Failed",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Login error: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task PerformLoginAsync(string username, string password)
        {
            try
            {
                ShowLoading(true);
                ShowStatus("Authenticating...", isError: false);

                var result = await _authService.LoginAsync(username, password);

                if (result.Success && result.User != null)
                {
                    ShowStatus($"Welcome, {result.User.FullName ?? result.User.Username}!", isError: false);
                    _currentUserService.SetCurrentUser(result.User);

                    // Small delay to show success message
                    await Task.Delay(500);

                    // Get MainWindow from DI container
                    var mainWindow = App.GetService<MainWindow>();
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
                // You can also use DI for other windows if needed
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
            try
            {
                var statusText = FindName("StatusMessageText") as TextBlock;
                if (statusText != null)
                {
                    statusText.Text = message;
                    statusText.Foreground = isError ?
                        System.Windows.Media.Brushes.Red :
                        System.Windows.Media.Brushes.Green;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing status: {ex.Message}");
            }
        }

        private void ShowLoading(bool isLoading)
        {
            try
            {
                var loadingGrid = FindName("LoadingGrid") as FrameworkElement;
                var loginBtn = FindName("LoginBtn") as Button;

                if (loadingGrid != null)
                    loadingGrid.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;

                if (loginBtn != null)
                    loginBtn.IsEnabled = !isLoading;

                UsernameTextBox.IsEnabled = !isLoading;
                PasswordTextBox.IsEnabled = !isLoading;

                var forgotBtn = FindName("ForgotPasswordBtn") as Button;
                if (forgotBtn != null)
                    forgotBtn.IsEnabled = !isLoading;

                var createBtn = FindName("CreateAccountBtn") as Button;
                if (createBtn != null)
                    createBtn.IsEnabled = !isLoading;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing loading: {ex.Message}");
            }
        }
    }
}