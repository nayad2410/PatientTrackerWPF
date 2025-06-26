using PatientTrackerWPF.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PatientTrackerWPF
{
    public partial class CreateAccountWindow : Window
    {
        private readonly AuthenticationService authService;

        public CreateAccountWindow()
        {
            InitializeComponent();
            authService = new AuthenticationService();

            // Set focus to username box
            Loaded += (s, e) => UsernameTextBox.Focus();

            // Handle Enter key events
            UsernameTextBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) FullNameTextBox.Focus(); };
            FullNameTextBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) EmailTextBox.Focus(); };
            EmailTextBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) RoleComboBox.Focus(); };
            RoleComboBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) PasswordBox.Focus(); };
            PasswordBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) ConfirmPasswordBox.Focus(); };
            ConfirmPasswordBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) CreateAccount_Click(s, e); };

            // Populate role ComboBox
            RoleComboBox.Items.Add("User");
            RoleComboBox.Items.Add("Technician");
            RoleComboBox.Items.Add("Doctor");
            RoleComboBox.Items.Add("Researcher");
            RoleComboBox.Items.Add("Admin");
            RoleComboBox.SelectedIndex = 0; // Default to "User"
        }

        private async void CreateAccount_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text.Trim();
            var fullName = FullNameTextBox.Text.Trim();
            var email = EmailTextBox.Text.Trim();
            var password = PasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;
            var role = RoleComboBox.SelectedItem?.ToString() ?? "User";

            // Validation
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowStatus("Please enter a username.", isError: true);
                UsernameTextBox.Focus();
                return;
            }

            if (username.Length < 3)
            {
                ShowStatus("Username must be at least 3 characters long.", isError: true);
                UsernameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                ShowStatus("Please enter your full name.", isError: true);
                FullNameTextBox.Focus();
                return;
            }

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

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowStatus("Please enter a password.", isError: true);
                PasswordBox.Focus();
                return;
            }

            if (!IsValidPassword(password))
            {
                ShowStatus("Password must be at least 8 characters long and contain uppercase, lowercase, number, and special character.", isError: true);
                PasswordBox.Focus();
                return;
            }

            if (password != confirmPassword)
            {
                ShowStatus("Passwords do not match.", isError: true);
                ConfirmPasswordBox.Focus();
                return;
            }

            try
            {
                ShowLoading(true);
                ShowStatus("Creating account...", isError: false);

                var result = await authService.CreateUserAsync(username, fullName, email, password, role, "Self-Registration");

                if (result.Success)
                {
                    ShowStatus($"Account created successfully! Welcome, {result.User?.FullName}!", isError: false);

                    // Small delay to show success message
                    await Task.Delay(1500);

                    MessageBox.Show($"Account created successfully!\n\nUsername: {username}\nRole: {role}\n\nYou can now login with your credentials.",
                                   "Account Created", MessageBoxButton.OK, MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowStatus(result.Message, isError: true);

                    // Focus appropriate field based on error
                    if (result.Message.Contains("username", StringComparison.OrdinalIgnoreCase))
                        UsernameTextBox.Focus();
                    else if (result.Message.Contains("email", StringComparison.OrdinalIgnoreCase))
                        EmailTextBox.Focus();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error creating account: {ex.Message}", isError: true);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void CreateDefaultAccounts_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("This will create 3 default accounts:\n\n" +
                                        "• admin / Admin123! (Administrator)\n" +
                                        "• technician / Tech123! (Technician)\n" +
                                        "• provider / Provider123! (Doctor)\n\n" +
                                        "These passwords should be changed immediately after first login.\n\n" +
                                        "Continue?",
                                        "Create Default Accounts",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                CreateDefaultAccountsAsync();
            }
        }

        private async void CreateDefaultAccountsAsync()
        {
            try
            {
                ShowLoading(true);
                ShowStatus("Creating default accounts...", isError: false);

                var accounts = new[]
                {
                    new { Username = "admin", FullName = "System Administrator", Email = "admin@mentalhealth.clinic", Password = "Admin123!", Role = "Admin" },
                    new { Username = "technician", FullName = "Clinical Technician", Email = "technician@mentalhealth.clinic", Password = "Tech123!", Role = "Technician" },
                    new { Username = "provider", FullName = "Healthcare Provider", Email = "provider@mentalhealth.clinic", Password = "Provider123!", Role = "Doctor" }
                };

                int successCount = 0;
                string messages = "";

                foreach (var account in accounts)
                {
                    var result = await authService.CreateUserAsync(
                        account.Username,
                        account.FullName,
                        account.Email,
                        account.Password,
                        account.Role,
                        "System-DefaultAccounts");

                    if (result.Success)
                    {
                        successCount++;
                        messages += $"✓ Created: {account.Username} ({account.Role})\n";
                    }
                    else
                    {
                        messages += $"✗ Failed: {account.Username} - {result.Message}\n";
                    }
                }

                ShowStatus($"Default accounts creation completed. {successCount}/3 accounts created.",
                          isError: successCount < 3);

                MessageBox.Show($"Default Accounts Creation Results:\n\n{messages}\n" +
                               "Important: Please change these default passwords immediately after first login!",
                               "Default Accounts Created",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error creating default accounts: {ex.Message}", isError: true);
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
            CreateAccountBtn.IsEnabled = !isLoading;
            CreateDefaultAccountsBtn.IsEnabled = !isLoading;
            UsernameTextBox.IsEnabled = !isLoading;
            FullNameTextBox.IsEnabled = !isLoading;
            EmailTextBox.IsEnabled = !isLoading;
            PasswordBox.IsEnabled = !isLoading;
            ConfirmPasswordBox.IsEnabled = !isLoading;
            RoleComboBox.IsEnabled = !isLoading;
            CancelBtn.IsEnabled = !isLoading;
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