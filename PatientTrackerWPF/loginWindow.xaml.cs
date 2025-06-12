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
using PatientTrackerWPF.Data;
using PatientTrackerWPF.Models;

namespace PatientTrackerWPF
{
    /// <summary>
    /// Interaction logic for loginWindow.xaml
    /// </summary>
    public partial class loginWindow : Window
    {
        public loginWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameBox.Text;
            string password = PasswordBox.Password;
            if (username =="admin" && password == "admin123")
            {
                MessageBox.Show("Login successful!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            
                // Optionally, open the main application window here
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show(); // Show the main application window
                this.Close(); // Close the login window

            }
            else
            {
                MessageBox.Show("Invalid username or password.", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            /*       string username = UsernameTextBox.Text.Trim();
                   string password = PasswordBox.Password.Trim();
                   if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                   {
                       MessageBox.Show("Please enter both username and password.", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                       return;
                   }
                   // Here you would typically call your authentication service
                   // For example:
                   // var authService = new AuthenticationService();
                   // var result = await authService.LoginAsync(username, password);
                   // Simulating a successful login for demonstration purposes
                   bool loginSuccess = true; // Replace with actual login logic
                   if (loginSuccess)
                   {
                       MessageBox.Show("Login successful!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                       this.Close(); // Close the login window
                       // Optionally, open the main application window here
                   }
                   else
                   {
                       MessageBox.Show("Invalid username or password.", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                   }*/


        }

        private void ForgotPasswordButton_Click(object sender, RoutedEventArgs e)
        {
          
        }

        private void CreateAccountButton_Click(object sender, RoutedEventArgs e)
        {
      
        }


    }
}
