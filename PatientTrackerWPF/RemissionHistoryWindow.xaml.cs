using PatientTrackerWPF.Services;
using System;
using System.IO;
using System.Windows;

namespace PatientTrackerWPF
{
    /// <summary>
    /// Interaction logic for RemissionHistoryWindow.xaml
    /// </summary>
    public partial class RemissionHistoryWindow : Window
    {
        private readonly RemissionTrackingService.AllTimeRemissionAnalysis analysis;

        public RemissionHistoryWindow(RemissionTrackingService.AllTimeRemissionAnalysis remissionAnalysis)
        {
            InitializeComponent();
            analysis = remissionAnalysis;
            EnableKeyboardShortcuts();
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                // Update summary metrics with proper null checking
                AllTimeRemissionRateText.Text = $"{analysis.AllTimeRemissionRate:F1}%";
                AllTimeRemissionCountText.Text = $"({analysis.PatientsWhoEverReachedRemission}/{analysis.TotalEligiblePatients} patients)";
                CurrentRemissionText.Text = analysis.CurrentlyInRemission.ToString();
                LostRemissionText.Text = analysis.LostRemission.ToString();
                AvgDurationText.Text = $"{analysis.AverageRemissionDuration:F0} days";

                // Load remission periods data
                if (analysis.AllRemissionPeriods != null)
                {
                    RemissionPeriodsGrid.ItemsSource = analysis.AllRemissionPeriods;
                }
                else
                {
                    // Show empty message if no data
                    MessageBox.Show("No remission data available for analysis.", "Information",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading remission data: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportRemissionReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowLoading(true);

                var remissionService = new RemissionTrackingService();
                var report = remissionService.GenerateRemissionReport(analysis);

                // Open Save File Dialog
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Save Remission Report",
                    FileName = $"AllTimeRemissionAnalysis_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                    Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    DefaultExt = ".txt"
                };

                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    File.WriteAllText(dialog.FileName, report);

                    MessageBox.Show($"✅ All-time remission report exported successfully!\n\nLocation: {dialog.FileName}",
                                    "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error exporting report: {ex.Message}", "Export Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowLoading(false);
            }
        }


        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ShowLoading(bool isLoading)
        {
            // Disable/enable controls during export
            ExportRemissionBtn.IsEnabled = !isLoading;
            RemissionPeriodsGrid.IsEnabled = !isLoading;

            if (isLoading)
            {
                this.Cursor = System.Windows.Input.Cursors.Wait;
            }
            else
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        // Handle window closing event
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Clean up any resources if needed
            base.OnClosing(e);
        }

        // Optional: Add keyboard shortcut for export (Ctrl+E)
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.E &&
                (e.KeyboardDevice.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                ExportRemissionReport_Click(sender, new RoutedEventArgs());
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close_Click(sender, new RoutedEventArgs());
            }
        }

        // Add this to the constructor to enable keyboard shortcuts
        private void EnableKeyboardShortcuts()
        {
            this.KeyDown += Window_KeyDown;
        }

        private void RemissionPeriodsGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}