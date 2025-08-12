#nullable disable
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using PatientTrackerWPF.Constants;
using PatientTrackerWPF.Data;
using PatientTrackerWPF.Helper;
using PatientTrackerWPF.Models;
using PatientTrackerWPF.Services;
using PatientTrackerWPF.Utilities;
using PdfSharp.Drawing;
using PdfSharp.Xps;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Xps.Packaging;
using static SkiaSharp.HarfBuzz.SKShaper;
using static System.Net.Mime.MediaTypeNames;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

// These for the professional report 
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingBrushes = System.Drawing.Brushes;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingFontStyle = System.Drawing.FontStyle;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPens = System.Drawing.Pens;
using DrawingPointF = System.Drawing.PointF;
using DrawingRectangle = System.Drawing.Rectangle;
using Path = System.IO.Path;
using Separator = LiveCharts.Wpf.Separator;

namespace PatientTrackerWPF
{
    [SupportedOSPlatform("windows")]
    public partial class MainWindow : Window
    {
        // ─── Fields ────────────────────────────────────────────────────────────
        private string filterId = "";
        private Dictionary<string, List<ScoreEntry>> patientData = new();
        private ClinicalMetricsService metricsService;
        private RemissionTrackingService remissionService;
        private readonly AuthenticationService authService;
        private readonly ICurrentUserService currentUserService;
        private ClinicalMetricsService.ClinicalMetrics? currentMetrics;
        private List<ScoreEntry> currentPatientEntries = new List<ScoreEntry>();

        private User currentUser => authService?.CurrentUser ?? currentUserService?.CurrentUser;

        // ─── Chart Collections ────────────────────────────────────────────────
        public SeriesCollection ScoreSeriesCollection { get; set; } = new SeriesCollection();
        public ChartValues<DateTimePoint> Phq9Values { get; set; } = new();
        public ChartValues<DateTimePoint> Gad7Values { get; set; } = new();
        public ChartValues<DateTimePoint> Bdi2Values { get; set; } = new();
        public ChartValues<DateTimePoint> Pcl5Values { get; set; } = new();
        public ChartValues<DateTimePoint> YbocsValues { get; set; } = new();

        private readonly AppDbContext _dbContext;

        private readonly AuditService _auditService;
        private readonly EncryptionService _encryptionService;
        private bool isInEditMode = false;
        private ScoreEntry editingEntry = null;

        public MainWindow(
            AuthenticationService authenticationService,
            ICurrentUserService currentUserService,
            ClinicalMetricsService clinicalMetricsService,
            RemissionTrackingService remissionTrackingService,
            AppDbContext dbContext)
        {
            InitializeComponent();

            // Assign injected services
            authService = authenticationService;
            this.currentUserService = currentUserService;
            metricsService = clinicalMetricsService;
            remissionService = remissionTrackingService;
            _auditService = App.GetService<AuditService>();
            _encryptionService = App.GetService<EncryptionService>();
            _dbContext = dbContext;

            if (!Resources.Contains("ScoreConverter"))
            {
                Resources.Add("ScoreConverter", new ScoreConverter());
            }

            DataContext = this;
            InitializeChart();
            SetupResponsiveLayout();
            UpdateUserDisplay();

            // Always load from database
            _ = Task.Run(async () =>
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    await LoadAllPatientsFromDatabase();
                });
            });
        }

        private void UpdateUserDisplay()
        {
            try
            {
                // Update user display - with null checks
                if (authService?.CurrentUser != null)
                {
                    CurrentUserText.Text = authService.GetCurrentUserFullName();
                    UserRoleText.Text = authService.CurrentUser?.Role ?? "Unknown Role";
                    // Apply role-based permissions when user display updates
                    ApplyRoleBasedPermissions();
                }
                else if (currentUserService?.CurrentUser != null)
                {
                    CurrentUserText.Text = currentUserService.CurrentUser.FullName ?? currentUserService.CurrentUser.Username ?? "Unknown User";
                    UserRoleText.Text = currentUserService.CurrentUser.Role ?? "Unknown Role";
                    // Apply role-based permissions when user display updates
                    ApplyRoleBasedPermissions();
                }
                else
                {
                    CurrentUserText.Text = "Unknown User";
                    UserRoleText.Text = "Unknown Role";
                    this.Title = "Reconnect Progress Tracker";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateUserDisplay: {ex.Message}");
                // Set fallback values
                CurrentUserText.Text = "System User";
                UserRoleText.Text = "User";
                this.Title = "Reconnect Progress Tracker";
            }
        }

        private void ApplyRoleBasedPermissions()
        {
            if (currentUser == null) return;

            try
            {
                if (RoleHelper.IsAdmin(currentUser))
                {
                    // Optional: Show admin capabilities notice
                    System.Diagnostics.Debug.WriteLine("Admin user detected - full system access granted");
                    // You could add a subtle admin indicator to your UI
                    this.Title += " [ADMINISTRATOR]";
                }

                // Apply UI visibility based on role
                ApplyUIPermissions();

                // Show role information
                UpdateRoleDisplay();

                System.Diagnostics.Debug.WriteLine($"Applied permissions for {currentUser.Username} ({currentUser.Role})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying role permissions: {ex.Message}");
            }
        }

        private void ApplyUIPermissions()
        {
            var user = currentUser;

            // Create Account / User Management (Admin only)
            HideElementIfNotPermitted("CreateAccountButton", RoleHelper.CanManageUsers(user));
            HideElementIfNotPermitted("UserManagementButton", RoleHelper.CanManageUsers(user));
            HideElementIfNotPermitted("AdminMenuItem", RoleHelper.CanManageUsers(user));
            BackupNowButton.Visibility = RoleHelper.IsAdmin(currentUser) ? Visibility.Visible : Visibility.Collapsed;
            ImportCsvButton.Visibility = RoleHelper.IsAdmin(currentUser) ? Visibility.Visible : Visibility.Collapsed;
            DeletePatientButton.Visibility = (RoleHelper.IsAdmin(currentUser) || RoleHelper.IsDoctor(currentUser))
                ? Visibility.Visible : Visibility.Collapsed;

            // Data Entry Controls
            var canAddData = RoleHelper.CanAddData(user);
            var canEditData = RoleHelper.CanEditData(user);

            SetElementEnabled("PatientIdBox", canAddData);
            SetElementEnabled("Phq9Box", canAddData);
            SetElementEnabled("Gad7Box", canAddData);
            SetElementEnabled("Bdi2Box", canAddData);
            SetElementEnabled("PCL5Total", canAddData);
            SetElementEnabled("YBOCS", canAddData);
            SetElementEnabled("NoteBox", canAddData);
            SetElementEnabled("DatePicker", canAddData);

            HideElementIfNotPermitted("AddScoreButton", canAddData);

            // Export Controls
            var canExport = RoleHelper.CanExportData(user);
            HideElementIfNotPermitted("ExportToCsvButton", canExport);
            HideElementIfNotPermitted("ExportToPngButton", canExport);
            HideElementIfNotPermitted("ExportMetricsButton", canExport);

            // Report Generation
            var canGenerateReports = RoleHelper.CanGenerateReports(user);
            HideElementIfNotPermitted("GenerateProfessionalReportButton", canGenerateReports);

            // Special handling for Researchers (read-only mode)
            if (RoleHelper.IsResearcher(user))
            {
                MakeDataEntryReadOnlyForResearcher();
            }
        }

        private void HideElementIfNotPermitted(string elementName, bool hasPermission)
        {
            var element = FindName(elementName) as FrameworkElement;
            if (element != null)
            {
                element.Visibility = hasPermission ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SetElementEnabled(string elementName, bool isEnabled)
        {
            var element = FindName(elementName) as Control;
            if (element != null)
            {
                element.IsEnabled = isEnabled;
            }
        }

        private void MakeDataEntryReadOnlyForResearcher()
        {
            // Make text boxes read-only for researchers
            var textBoxes = new[] { "PatientIdBox", "Phq9Box", "Gad7Box", "Bdi2Box", "PCL5Total", "YBOCS", "NoteBox" };
            foreach (var name in textBoxes)
            {
                var textBox = FindName(name) as TextBox;
                if (textBox != null)
                {
                    textBox.IsReadOnly = true;
                    textBox.Background = Brushes.LightGray;
                }
            }

            var datePicker = FindName("DatePicker") as DatePicker;
            if (datePicker != null)
            {
                datePicker.IsEnabled = false;
            }

            // Make data grid read-only
            if (ScoresGrid != null)
            {
                ScoresGrid.IsReadOnly = true;
            }
        }

        private void UpdateRoleDisplay()
        {
            var user = currentUser;
            if (user == null) return;

            // Update role information in UI
            if (CurrentUserText != null) CurrentUserText.Text = user.FullName ?? user.Username;
            if (UserRoleText != null)
            {
                UserRoleText.Text = user.Role;
                UserRoleText.ToolTip = UserRoles.GetRoleDescription(user.Role);
            }

            // Update status bar if you have one
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            var user = currentUser;
            if (user == null) return;

            // Update status bar elements if they exist
            var statusText = FindName("StatusText") as TextBlock;
            var statusRoleText = FindName("StatusRoleText") as TextBlock;
            var statusPermissionsText = FindName("StatusPermissionsText") as TextBlock;

            if (statusText != null)
            {
                statusText.Text = "Ready";
            }

            if (statusRoleText != null)
            {
                statusRoleText.Text = user.Role;
            }

            if (statusPermissionsText != null)
            {
                statusPermissionsText.Text = RoleHelper.GetPermissionSummary(user);
            }
        }

        // ─── Responsive Layout ────────────────────────────────────────────────
        private void SetupResponsiveLayout()
        {
            MinWidth = 800;
            MinHeight = 600;
            SizeChanged += (s, e) =>
            {
                if (e.NewSize.Width < 1000)
                {
                    InputFieldsPanel.Orientation = Orientation.Vertical;
                }
                else
                {
                    InputFieldsPanel.Orientation = Orientation.Horizontal;
                }
            };
        }

        // ─── Initialize Chart ─────────────────────────────────────────────────
        private void InitializeChart()
        {
            ScoreSeriesCollection = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "PHQ-9",
                    Values = Phq9Values,
                    PointGeometry = DefaultGeometries.Circle,
                    StrokeThickness = 3,
                    LineSmoothness = 0,
                    Fill = Brushes.Transparent,
                    Stroke = Brushes.MediumBlue,
                    PointGeometrySize = 8
                },
                new LineSeries
                {
                    Title = "GAD-7",
                    Values = Gad7Values,
                    PointGeometry = DefaultGeometries.Circle,
                    StrokeThickness = 3,
                    LineSmoothness = 0,
                    Fill = Brushes.Transparent,
                    Stroke = Brushes.ForestGreen,
                    PointGeometrySize = 8
                },
                new LineSeries
                {
                    Title = "BDI-II",
                    Values = Bdi2Values,
                    PointGeometry = DefaultGeometries.Circle,
                    StrokeThickness = 3,
                    LineSmoothness = 0,
                    Fill = Brushes.Transparent,
                    Stroke = Brushes.OrangeRed,
                    PointGeometrySize = 8
                },
                new LineSeries
                {
                    Title = "PCL-5",
                    Values = Pcl5Values,
                    PointGeometry = DefaultGeometries.Circle,
                    StrokeThickness = 3,
                    LineSmoothness = 0,
                    Fill = Brushes.Transparent,
                    Stroke = Brushes.DarkCyan,
                    PointGeometrySize = 8
                },
                new LineSeries
                {
                    Title = "Y-BOCS",
                    Values = YbocsValues,
                    PointGeometry = DefaultGeometries.Circle,
                    StrokeThickness = 3,
                    LineSmoothness = 0,
                    Fill = Brushes.Transparent,
                    Stroke = Brushes.Purple,
                    PointGeometrySize = 8
                }
            };

            // Set up initial axes
            var today = DateTime.Today;
            PatientProgressChart.AxisX.Clear();
            PatientProgressChart.AxisX.Add(new Axis
            {
                Title = "Date",
                LabelFormatter = v => new DateTime((long)v).ToString("MM/dd"),
                MinValue = today.AddDays(-7).Ticks,
                MaxValue = today.AddDays(7).Ticks,
                Separator = new Separator { Step = TimeSpan.FromDays(1).Ticks, IsEnabled = true }
            });

            PatientProgressChart.AxisY.Clear();
            PatientProgressChart.AxisY.Add(new Axis
            {
                Title = "Score",
                MinValue = 0,
                MaxValue = 80,
                Separator = new Separator { Step = 10, IsEnabled = true }
            });

            ScoresGrid.ItemsSource = new List<ScoreEntry>();
        }

        // ─── Add Score Click with Score Validation ──────────────────────────────────────────────────
        private async void AddScore_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("AddScore_Click Started");

            // Check permissions
            if (!RoleHelper.CanAddData(currentUser) && !isInEditMode)
            {
                MessageBox.Show("You don't have permission to add patient data.",
                               "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var id = PatientIdBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                MessageBox.Show("Please enter a Patient ID.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Processing Patient ID: '{id}' | Edit Mode: {isInEditMode}");

            // Validate score ranges
            var validationErrors = new List<string>();

            if (!string.IsNullOrWhiteSpace(Phq9Box.Text))
            {
                if (!int.TryParse(Phq9Box.Text, out int phq9) || phq9 < 0 || phq9 > 27)
                    validationErrors.Add("PHQ-9 must be between 0 and 27");
            }

            if (!string.IsNullOrWhiteSpace(Gad7Box.Text))
            {
                if (!int.TryParse(Gad7Box.Text, out int gad7) || gad7 < 0 || gad7 > 21)
                    validationErrors.Add("GAD-7 must be between 0 and 21");
            }

            if (!string.IsNullOrWhiteSpace(Bdi2Box.Text))
            {
                if (!int.TryParse(Bdi2Box.Text, out int bdi2) || bdi2 < 0 || bdi2 > 63)
                    validationErrors.Add("BDI-II must be between 0 and 63");
            }

            if (!string.IsNullOrWhiteSpace(PCL5Total.Text))
            {
                if (!int.TryParse(PCL5Total.Text, out int pcl5) || pcl5 < 0 || pcl5 > 80)
                    validationErrors.Add("PCL-5 must be between 0 and 80");
            }

            if (!string.IsNullOrWhiteSpace(YBOCS.Text))
            {
                if (!int.TryParse(YBOCS.Text, out int ybocs) || ybocs < 0 || ybocs > 40)
                    validationErrors.Add("Y-BOCS must be between 0 and 40");
            }

            if (validationErrors.Any())
            {
                MessageBox.Show(
                    "Please correct the following errors:\n\n" + string.Join("\n", validationErrors),
                    "Invalid Score Values",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var selectedDate = DatePicker.SelectedDate ?? DateTime.Today;
            System.Diagnostics.Debug.WriteLine($"Selected Date: {selectedDate:yyyy-MM-dd}");

            try
            {
                System.Diagnostics.Debug.WriteLine("DATABASE OPERATION START");

                // Clear change tracker to avoid stale data
                _dbContext.ChangeTracker.Clear();
                System.Diagnostics.Debug.WriteLine("Change tracker cleared");

                if (isInEditMode && editingEntry != null)
                {
                    // EDIT MODE: Update the specific existing record by ID
                    System.Diagnostics.Debug.WriteLine($"EDIT MODE: Updating existing entry ID {editingEntry.Id}");

                    var existingEntry = await _dbContext.ScoreEntries
                        .FirstOrDefaultAsync(e => e.Id == editingEntry.Id);

                    if (existingEntry != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Found existing entry for update: ID {existingEntry.Id}");

                        // Update the existing entry
                        existingEntry.PHQ9 = TryParseOrNull(Phq9Box.Text);
                        existingEntry.GAD7 = TryParseOrNull(Gad7Box.Text);
                        existingEntry.BDI2 = TryParseOrNull(Bdi2Box.Text);
                        existingEntry.PCL5 = TryParseOrNull(PCL5Total.Text);
                        existingEntry.YBOCS = TryParseOrNull(YBOCS.Text);
                        existingEntry.Note = NoteBox.Text.Trim();

                        // Mark as modified
                        var entityEntry = _dbContext.Entry(existingEntry);
                        entityEntry.State = EntityState.Modified;

                        System.Diagnostics.Debug.WriteLine($"Entity marked as Modified, State: {entityEntry.State}");
                    }
                    else
                    {
                        MessageBox.Show("Error: Original entry not found for update.", "Update Error",
                                       MessageBoxButton.OK, MessageBoxImage.Error);
                        ResetEditMode();
                        return;
                    }
                }
                else
                {
                    // NORMAL MODE: Check for existing entry with FRESH query
                    var existingEntry = await _dbContext.ScoreEntries
                        .FirstOrDefaultAsync(e => e.PatientId == id && e.Date.Date == selectedDate.Date);

                    System.Diagnostics.Debug.WriteLine("NORMAL MODE: Add/Update Logic");
                    System.Diagnostics.Debug.WriteLine($"Looking for Patient: '{id}', Date: {selectedDate.Date:yyyy-MM-dd}");
                    System.Diagnostics.Debug.WriteLine($"existingEntry found: {existingEntry != null}");

                    if (existingEntry != null)
                    {
                        System.Diagnostics.Debug.WriteLine("UPDATING EXISTING ENTRY");

                        // Confirm with user
                        var result = MessageBox.Show(
                            $"⚠️ DUPLICATE ENTRY DETECTED\n\n" +
                            $"There is already a score entry for:\n" +
                            $"Patient: {id}\n" +
                            $"Date: {selectedDate:yyyy-MM-dd}\n\n" +
                            $"Would you like to update the existing entry?",
                            "Update Existing Entry?",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.No)
                        {
                            System.Diagnostics.Debug.WriteLine("User chose NOT to update existing entry");
                            return;
                        }

                        // Update existing entry
                        existingEntry.PHQ9 = TryParseOrNull(Phq9Box.Text);
                        existingEntry.GAD7 = TryParseOrNull(Gad7Box.Text);
                        existingEntry.BDI2 = TryParseOrNull(Bdi2Box.Text);
                        existingEntry.PCL5 = TryParseOrNull(PCL5Total.Text);
                        existingEntry.YBOCS = TryParseOrNull(YBOCS.Text);
                        existingEntry.Note = NoteBox.Text.Trim();

                        // Mark as modified
                        var entityEntry = _dbContext.Entry(existingEntry);
                        entityEntry.State = EntityState.Modified;

                        System.Diagnostics.Debug.WriteLine($"Entity State: {entityEntry.State}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("CREATING NEW ENTRY");

                        // Create new entity
                        var newEntry = new ScoreEntry
                        {
                            PatientId = id,
                            PHQ9 = TryParseOrNull(Phq9Box.Text),
                            GAD7 = TryParseOrNull(Gad7Box.Text),
                            BDI2 = TryParseOrNull(Bdi2Box.Text),
                            PCL5 = TryParseOrNull(PCL5Total.Text),
                            YBOCS = TryParseOrNull(YBOCS.Text),
                            Note = NoteBox.Text.Trim(),
                            Date = selectedDate
                        };

                        _dbContext.ScoreEntries.Add(newEntry);
                        System.Diagnostics.Debug.WriteLine($"Added new entity for patient {id}");
                    }
                }

                // Save changes (UpdateAuditFields will be called automatically)
                System.Diagnostics.Debug.WriteLine("CALLING SaveChangesAsync");
                var changeCount = await _dbContext.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine($"SaveChanges returned: {changeCount} changes");

                // Log the action
                string action = isInEditMode ? "UPDATE_SCORE" : "CREATE_SCORE";
                await _auditService.LogActionAsync(action, id, $"Saved scores for patient {id}");

                // At the very end, after successful save:
                if (changeCount > 0)  // If data was saved successfully
                {
                    lastDataModification = DateTime.Now;  // Mark data as changed

                    // Auto-recalculate if this patient is currently selected
                    if (PatientSelector.SelectedItem?.ToString() == id)
                    {
                        await RecalculateAndRefreshAsync(id);
                    }
                }

                // Reset edit mode if we were editing
                if (isInEditMode)
                {
                    ResetEditMode();
                }

                // Refresh UI
                await LoadAllPatientsFromDatabase();

                // Update patient selector if needed
                if (!PatientSelector.Items.Contains(id))
                {
                    PatientSelector.Items.Add(id);
                }
                PatientSelector.SelectedItem = id;

                // Clear input fields
                ClearInputFields();

                string successMessage = isInEditMode ?
                    "✅ Patient data updated successfully!" :
                    "✅ Patient data saved successfully!";

                MessageBox.Show(successMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                System.Diagnostics.Debug.WriteLine("AddScore_Click Completed Successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in AddScore_Click");
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");

                // Reset edit mode on error
                if (isInEditMode)
                {
                    ResetEditMode();
                }

                MessageBox.Show($"❌ Error saving to database: {ex.Message}", "Database Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetEditMode()
        {
            isInEditMode = false;
            editingEntry = null;
            AddScoreButton.Content = "Add Score";
            AddScoreButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 130, 180)); // Steel Blue
            System.Diagnostics.Debug.WriteLine("Edit mode reset");
        }

        private void ClearInputFields()
        {
            Phq9Box.Clear();
            Gad7Box.Clear();
            Bdi2Box.Clear();
            PCL5Total.Clear();
            YBOCS.Clear();
            NoteBox.Clear();
            PatientIdBox.Clear();
            DatePicker.SelectedDate = DateTime.Today;
        }

        private async Task LoadAllPatientsFromDatabase()
        {
            try
            {
                var allEntries = await _dbContext.ScoreEntries
                    .OrderBy(e => e.PatientId)
                    .ThenBy(e => e.Date)
                    .ToListAsync();

                patientData.Clear();
                PatientSelector.Items.Clear();

                foreach (var group in allEntries.GroupBy(e => e.PatientId))
                {
                    patientData[group.Key] = group.ToList();
                }

                foreach (var patientId in patientData.Keys.OrderBy(x => x))
                {
                    PatientSelector.Items.Add(patientId);
                }

                System.Diagnostics.Debug.WriteLine($"Loaded {patientData.Keys.Count} patients from database");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading patients: {ex.Message}");
                MessageBox.Show($"Error loading data: {ex.Message}", "Database Error",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to logout?",
                "Confirm Logout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Perform logout
                authService?.Logout();
                currentUserService?.ClearCurrentUser();

                // Clear any sensitive data
                patientData.Clear();
                currentPatientEntries.Clear();
                currentMetrics = null;

                // Clear UI
                PatientSelector.Items.Clear();
                ScoresGrid.ItemsSource = null;

                // Clear input fields
                PatientIdBox.Clear();
                Phq9Box.Clear();
                Gad7Box.Clear();
                Bdi2Box.Clear();
                PCL5Total.Clear();
                YBOCS.Clear();
                NoteBox.Clear();

                // Reset chart
                Phq9Values.Clear();
                Gad7Values.Clear();
                Bdi2Values.Clear();
                Pcl5Values.Clear();
                YbocsValues.Clear();

                // Show logout message
                MessageBox.Show("You have been logged out successfully.", "Logged Out",
                               MessageBoxButton.OK, MessageBoxImage.Information);

                // Close this window and show login window from DI
                var loginWindow = App.GetService<LoginWindow>();
                loginWindow.Show();
                this.Close();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (authService?.IsAuthenticated == true)
            {
                var result = MessageBox.Show(
                    "Are you sure you want to exit? You will be logged out.",
                    "Confirm Exit",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                // Clean logout
                authService?.Logout();
            }

            base.OnClosing(e);
        }

        private int? TryParseOrNull(string txt)
            => int.TryParse(txt, out var v) ? v : null;  // Return null instead of -1

        // ─── Update Chart ────────────────────────────────────────────────────
        private void UpdateChartForPatient(string patientId)
        {
            try
            {
                if (!patientData.ContainsKey(patientId)) return;

                var scores = patientData[patientId].OrderBy(s => s.Date).ToList();

                currentPatientEntries = scores;

                ScoresGrid.ItemsSource = null;
                ScoresGrid.ItemsSource = scores;

                // Clear chart data
                Phq9Values.Clear(); Gad7Values.Clear();
                Bdi2Values.Clear(); Pcl5Values.Clear();
                YbocsValues.Clear();

                // Add null checks for chart and axis
                if (PatientProgressChart?.AxisX == null || PatientProgressChart.AxisX.Count == 0)
                {
                    return; // Exit safely if chart is not ready
                }

                if (scores.Count == 0)
                {
                    // Reset to default ±7-day window
                    var t = DateTime.Today;
                    PatientProgressChart.AxisX[0].MinValue = t.AddDays(-7).Ticks;
                    PatientProgressChart.AxisX[0].MaxValue = t.AddDays(7).Ticks;

                    //Check separator exists before setting
                    if (PatientProgressChart.AxisX[0].Separator != null)
                    {
                        PatientProgressChart.AxisX[0].Separator.Step = TimeSpan.FromDays(1).Ticks;
                    }
                    return;
                }

                // Only add data points when scores exist (not null)
                foreach (var entry in scores)
                {
                    // Only add data points for actual scores, skip null values
                    if (entry.PHQ9.HasValue)
                        Phq9Values.Add(new DateTimePoint(entry.Date, (double)entry.PHQ9.Value));

                    if (entry.GAD7.HasValue)
                        Gad7Values.Add(new DateTimePoint(entry.Date, (double)entry.GAD7.Value));

                    if (entry.BDI2.HasValue)
                        Bdi2Values.Add(new DateTimePoint(entry.Date, (double)entry.BDI2.Value));

                    if (entry.PCL5.HasValue)
                        Pcl5Values.Add(new DateTimePoint(entry.Date, (double)entry.PCL5.Value));

                    if (entry.YBOCS.HasValue)
                        YbocsValues.Add(new DateTimePoint(entry.Date, (double)entry.YBOCS.Value));
                }

                // Set axis range with minimal padding
                var firstDate = scores.First().Date;
                var lastDate = scores.Last().Date;

                if (firstDate == lastDate)
                {
                    // Single date - add minimal padding
                    firstDate = firstDate.AddDays(-0.5);
                    lastDate = lastDate.AddDays(0.5);
                }
                else
                {
                    // Multiple dates - add very small padding
                    var padding = Math.Max(0.2, (lastDate - firstDate).TotalDays * 0.02);
                    firstDate = firstDate.AddDays(-padding);
                    lastDate = lastDate.AddDays(padding);
                }

                PatientProgressChart.AxisX[0].MinValue = firstDate.Ticks;
                PatientProgressChart.AxisX[0].MaxValue = lastDate.Ticks;

                // Smart date separator with error handling
                SetSmartDateSeparator(scores, firstDate, lastDate);

                PatientProgressChart.Update(true, true);

                // Update notes overlay
                Dispatcher.BeginInvoke(
                    (Action)(() => UpdateChartNotesForPatient(patientId)),
                    DispatcherPriority.Loaded
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating chart: {ex.Message}", "Chart Error");
            }
        }

        private void SetSmartDateSeparator(List<ScoreEntry> scores, DateTime firstDate, DateTime lastDate)
        {
            try
            {
                // Add null checks
                if (PatientProgressChart?.AxisX == null || PatientProgressChart.AxisX.Count == 0)
                {
                    return; // Exit safely if chart or axis is not ready
                }

                var xAxis = PatientProgressChart.AxisX[0];
                if (xAxis?.Separator == null)
                {
                    return; // Exit safely if separator is null
                }

                if (scores.Count == 1)
                {
                    // Single entry - show the entry date
                    xAxis.Separator.Step = TimeSpan.FromDays(1).Ticks;
                }
                else if (scores.Count <= 3)
                {
                    // Few entries - try to show each entry date
                    var minGap = scores.Skip(1).Select((entry, i) => (entry.Date - scores[i].Date).TotalDays).Min();
                    var step = Math.Max(1, Math.Floor(minGap));
                    xAxis.Separator.Step = TimeSpan.FromDays(step).Ticks;
                }
                else if (scores.Count <= 7)
                {
                    // Medium number of entries - show every other entry approximately
                    var totalDays = (lastDate - firstDate).TotalDays;
                    var step = Math.Max(1, Math.Ceiling(totalDays / 4));
                    xAxis.Separator.Step = TimeSpan.FromDays(step).Ticks;
                }
                else
                {
                    // Many entries - show strategic dates to avoid crowding
                    var totalDays = (lastDate - firstDate).TotalDays;

                    if (totalDays <= 30)
                    {
                        // Within a month - show weekly
                        xAxis.Separator.Step = TimeSpan.FromDays(7).Ticks;
                    }
                    else if (totalDays <= 90)
                    {
                        // Within 3 months - show bi-weekly
                        xAxis.Separator.Step = TimeSpan.FromDays(14).Ticks;
                    }
                    else
                    {
                        // Longer periods - show monthly
                        xAxis.Separator.Step = TimeSpan.FromDays(30).Ticks;
                    }
                }
            }
            catch (Exception ex)
            {
                // Graceful error handling - just log and continue
                System.Diagnostics.Debug.WriteLine($"Error setting date separator: {ex.Message}");
                // Don't show error to user, just use default separator
            }
        }

        // ─── Notes Overlay ───────────────────────────────────────────────────
        private void UpdateChartNotesForPatient(string patientId)
        {
            ChartNotesCanvas.Children.Clear();

            if (!patientData.ContainsKey(patientId)) return;
            var notes = patientData[patientId]
                .Where(s => !string.IsNullOrWhiteSpace(s.Note))
                .OrderBy(s => s.Date)
                .ToList();

            if (notes.Count == 0) return;

            var axis = PatientProgressChart.AxisX[0];
            double chartW = PatientProgressChart.ActualWidth;
            double chartH = PatientProgressChart.ActualHeight;

            const double noteWidth = 140;
            const double noteHeight = 50;
            const double margin = 10;
            const int maxNotesPerRow = 4;

            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];

                // Calculate position
                double dateX = (note.Date.Ticks - axis.MinValue) / (double)(axis.MaxValue - axis.MinValue) * chartW;

                // Smart positioning to avoid overlaps
                int row = i / maxNotesPerRow;
                int col = i % maxNotesPerRow;

                double x = Math.Max(margin, Math.Min(chartW - noteWidth - margin,
                    dateX - noteWidth/2 + (col - 2) * (noteWidth * 0.3)));
                double y = margin + row * (noteHeight + 10);

                if (y + noteHeight > chartH - margin) y = chartH - noteHeight - margin;

                var noteBox = CreateNoteBox(note, noteWidth, noteHeight, i);

                Canvas.SetLeft(noteBox, x);
                Canvas.SetTop(noteBox, y);
                ChartNotesCanvas.Children.Add(noteBox);
            }
        }

        private Border CreateNoteBox(ScoreEntry note, double width, double height, int colorIndex)
        {
            var box = new Border
            {
                Background = new SolidColorBrush(GetNoteBoxColor(colorIndex)),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Width = width,
                Height = height,
                Cursor = Cursors.Hand
            };

            box.MouseLeftButtonUp += (s, e) => MessageBox.Show(note.Note, "Treatment Note");

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = note.Date.ToString("MMM dd"),
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 2)
            });

            // TRUNCATE TO 20 CHARACTERS
            string displayText = note.Note.Length > 20 ? note.Note.Substring(0, 20) + "..." : note.Note;

            stack.Children.Add(new TextBlock
            {
                Text = displayText,
                FontSize = 9,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = Brushes.DarkGray,
                MaxHeight = 30
            });

            box.Child = stack;
            return box;
        }

        private System.Windows.Media.Color GetNoteBoxColor(int index)
        {
            var colors = new[]
            {
                System.Windows.Media.Color.FromRgb(255, 255, 204), // Light Yellow
                System.Windows.Media.Color.FromRgb(230, 243, 255), // Light Blue  
                System.Windows.Media.Color.FromRgb(240, 248, 230), // Light Green
                System.Windows.Media.Color.FromRgb(255, 240, 245), // Light Pink
                System.Windows.Media.Color.FromRgb(245, 245, 220), // Beige
                System.Windows.Media.Color.FromRgb(230, 230, 250), // Lavender
                System.Windows.Media.Color.FromRgb(240, 255, 240), // Honeydew
                System.Windows.Media.Color.FromRgb(255, 250, 240), // Floral White
                System.Windows.Media.Color.FromRgb(255, 228, 225), // Misty Rose
                System.Windows.Media.Color.FromRgb(240, 248, 255), // Alice Blue
                System.Windows.Media.Color.FromRgb(250, 240, 230), // Linen
                System.Windows.Media.Color.FromRgb(245, 255, 250)  // Mint Cream
            };
            return colors[index % colors.Length];
        }

        private async void GenerateProfessionalReport_Click(object sender, RoutedEventArgs e)
        {
            var patientId = PatientSelector.Text?.Trim();
            if (string.IsNullOrWhiteSpace(patientId))
            {
                MessageBox.Show("Please select a valid patient.");
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                List<ScoreEntry> entries;

                System.Diagnostics.Debug.WriteLine("Production mode: Using database data for report");

                entries = await _dbContext.ScoreEntries
                    .Where(e => e.PatientId == patientId)
                    .OrderBy(e => e.Date)
                    .ToListAsync();

                if (!entries.Any())
                {
                    MessageBox.Show($"No data available for patient {patientId} in database.");
                    return;
                }

                // Create professional clinical report
                var reportImage = CreateProfessionalClinicalReport(patientId, entries);

                // Save as PNG
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"ReconnectClinicalReport_{patientId}_{DateTime.Now:yyyyMMdd_HHmm}.png",
                    Filter = "PNG Image|*.png"
                };

                if (dialog.ShowDialog() == true)
                {
                    reportImage.Save(dialog.FileName, ImageFormat.Png);
                    reportImage.Dispose();

                    MessageBox.Show($"Professional clinical report generated successfully!\n\nFile: {dialog.FileName}",
                                   "Report Generated", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    reportImage.Dispose();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating report: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        [SupportedOSPlatform("windows")]
        private DrawingBitmap CreateProfessionalClinicalReport(string patientId, List<ScoreEntry> entries)
        {
            // Calculate dynamic height based on actual data
            int baseHeight = 800;  // Base height for header, chart, etc.
            int rowHeight = 22;
            int noteLineHeight = 40;  // Estimated height per note entry
            int tableRows = entries.Count;  // ALL entries, not limited
            int notesCount = entries.Count(e => !string.IsNullOrWhiteSpace(e.Note));

            // Calculate total height needed
            int tableHeight = tableRows * rowHeight + 100;  // +100 for headers
            int notesHeight = notesCount * noteLineHeight + 100;  // +100 for section header
            int totalHeight = baseHeight + tableHeight + notesHeight + 200;  // +200 for footer and padding

            // Report dimensions - width stays same, height is dynamic
            const int width = 1200;
            int height = Math.Max(1600, totalHeight);  // Minimum 1600, but can grow

            const int marginLeft = 50;
            const int sectionSpacing = 30;
            const int headerHeight = 65;
            const int headerBackgroundHeight = 100;
            const int headerBottomMargin = 20;
            const double tableWidthRatio = 0.75;

            var bitmap = new DrawingBitmap(width, height);
            using var g = DrawingGraphics.FromImage(bitmap);

            // High quality rendering
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            // Background
            g.Clear(DrawingColor.White);

            // Fonts - adjusted sizes for better fit
            var titleFont = new DrawingFont("Arial", 14, DrawingFontStyle.Bold);
            var subtitleFont = new DrawingFont("Arial", 9, DrawingFontStyle.Bold);
            var headerFont = new DrawingFont("Arial", 14, DrawingFontStyle.Bold);
            var subHeaderFont = new DrawingFont("Arial", 11, DrawingFontStyle.Bold);
            var bodyFont = new DrawingFont("Arial", 10);
            var smallFont = new DrawingFont("Arial", 8);
            var cellFont = new DrawingFont("Arial", 8.5f);
            var tableHeaderFont = new DrawingFont("Arial", 9, DrawingFontStyle.Bold);
            var noteDateFont = new DrawingFont("Arial", 9, DrawingFontStyle.Bold);
            var noteTextFont = new DrawingFont("Arial", 9);

            // Colors
            var reconnectBlue = DrawingColor.FromArgb(43, 95, 117);
            var lightBlue = DrawingColor.FromArgb(230, 243, 255);
            var darkGray = DrawingColor.FromArgb(64, 64, 64);
            var lightGray = DrawingColor.FromArgb(240, 240, 240);

            var currentY = 40;

            // HEADER SECTION
            using (var headerBrush = new SolidBrush(reconnectBlue))
            using (var headerRect = new SolidBrush(lightBlue))
            {
                // Header background
                g.FillRectangle(headerRect, 0, 0, width, headerBackgroundHeight);
                g.FillRectangle(headerBrush, 0, 0, width, headerHeight);

                const string brandText = "RECONNECT";
                const int brandX = marginLeft - 5;
                const int brandY = 15;

                g.DrawString(brandText, titleFont, DrawingBrushes.White, brandX, brandY);

                var brandWidth = g.MeasureString(brandText, titleFont).Width;
                g.DrawString(" MENTAL HEALTH", subtitleFont, DrawingBrushes.LightGray,
                            brandX + brandWidth, brandY + 2);

                // Report title
                g.DrawString("Clinical Progress Report", headerFont, DrawingBrushes.White, width - 400, 15);
                g.DrawString("Mental Health Assessment Report", bodyFont, DrawingBrushes.LightGray, width - 400, 40);
            }

            currentY = headerBackgroundHeight + headerBottomMargin;

            // PATIENT INFO SECTION
            g.DrawString($"Patient ID: {patientId}", headerFont, new SolidBrush(reconnectBlue), marginLeft, currentY);
            currentY += 30;
            g.DrawString($"Report Generated: {DateTime.Now:MMMM dd, yyyy h:mm tt}", bodyFont, new SolidBrush(darkGray), marginLeft, currentY);
            currentY += 25;
            g.DrawString($"Assessment Period: {entries.First().Date:yyyy-MM-dd} to {entries.Last().Date:yyyy-MM-dd}",
                        bodyFont, new SolidBrush(darkGray), marginLeft, currentY);
            currentY += 20;
            g.DrawString($"Total Assessments: {entries.Count}", bodyFont, new SolidBrush(darkGray), marginLeft, currentY);
            currentY += sectionSpacing + 10;

            // DATA TABLE SECTION - NOW SHOWS ALL DATA
            g.DrawString($"Complete Assessment History ({entries.Count} records)", headerFont, new SolidBrush(reconnectBlue), marginLeft, currentY);
            currentY += 30;

            // Create table showing ALL entries
            var tableWidth = (int)((width - 100) * tableWidthRatio);
            var tableY = CreateCompleteDataTable(g, entries, marginLeft, currentY, tableWidth, cellFont, tableHeaderFont, rowHeight);
            currentY = tableY + 35;

            // PROGRESS CHART SECTION
            g.DrawString($"Progress Track: {entries.First().Date:yyyy-MM-dd} to {entries.Last().Date:yyyy-MM-dd}",
                        headerFont, new SolidBrush(reconnectBlue), marginLeft, currentY);
            currentY += 25;

            // Create progress chart with legend at bottom
            currentY = CreateProgressChart(g, entries, marginLeft, currentY, width - 100, 400);
            currentY += 20;

            // TREATMENT NOTES SECTION - NOW SHOWS ALL NOTES
            g.DrawString($"Complete Treatment Notes History", headerFont, new SolidBrush(reconnectBlue), marginLeft, currentY);
            currentY += 25;

            var allNotes = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Note))
                .OrderBy(e => e.Date)  // Changed to chronological order
                .ToList();

            if (allNotes.Any())
            {
                g.DrawString($"Total notes: {allNotes.Count}", smallFont, new SolidBrush(darkGray), marginLeft + 20, currentY);
                currentY += 20;

                var spaceWidth = g.MeasureString(" ", noteTextFont).Width;

                foreach (var entry in allNotes)
                {
                    // Check if we're getting close to the bottom - extend if needed
                    if (currentY > height - 100)
                    {
                        // We've run out of space - this shouldn't happen with dynamic height
                        // but just in case, break here
                        g.DrawString($"... and {allNotes.Count - allNotes.IndexOf(entry)} more notes",
                                    noteTextFont, new SolidBrush(darkGray), marginLeft + 20, currentY);
                        break;
                    }

                    // Date header
                    g.DrawString($"• {entry.Date:yyyy-MM-dd}:", noteDateFont,
                                new SolidBrush(reconnectBlue), marginLeft + 20, currentY);
                    currentY += 16;

                    // Wrapped note text
                    var noteText = entry.Note;
                    var maxWidth = width - 140;
                    var wrappedText = WrapTextOptimized(noteText, noteTextFont, maxWidth, g, spaceWidth);

                    foreach (var line in wrappedText)
                    {
                        g.DrawString($"  {line}", noteTextFont, new SolidBrush(darkGray), marginLeft + 40, currentY);
                        currentY += 18;
                    }
                    currentY += 5; // Space between notes
                }
            }
            else
            {
                g.DrawString("No treatment notes available.", noteTextFont, new SolidBrush(darkGray), marginLeft + 20, currentY);
                currentY += 20;
            }

            // FOOTER - Position at bottom of actual content, not fixed position
            currentY += 40;  // Add some space before footer
            using (var footerBrush = new SolidBrush(lightGray))
            {
                g.FillRectangle(footerBrush, 0, currentY - 10, width, 70);
                g.DrawString("This report is generated by Reconnect Mental Health Assessment System",
                            smallFont, new SolidBrush(darkGray), marginLeft, currentY + 10);
                g.DrawString($"Report ID: RPT-{patientId}-{DateTime.Now:yyyyMMddHHmm}",
                            smallFont, new SolidBrush(darkGray), marginLeft, currentY + 25);
                g.DrawString($"Page contains {entries.Count} assessment records and {allNotes.Count} clinical notes",
                            smallFont, new SolidBrush(darkGray), marginLeft, currentY + 40);
            }

            // Cleanup all fonts
            titleFont.Dispose();
            subtitleFont.Dispose();
            headerFont.Dispose();
            subHeaderFont.Dispose();
            bodyFont.Dispose();
            smallFont.Dispose();
            cellFont.Dispose();
            tableHeaderFont.Dispose();
            noteDateFont.Dispose();
            noteTextFont.Dispose();

            return bitmap;
        }

        // New method to create table with ALL data
        private int CreateCompleteDataTable(DrawingGraphics g, List<ScoreEntry> entries, int x, int y, int tableWidth,
                                      DrawingFont cellFont, DrawingFont headerFont, int rowHeight)
        {
            var reconnectBlue = DrawingColor.FromArgb(43, 95, 117);
            var lightGray = DrawingColor.FromArgb(240, 240, 240);
            var subtleGray = DrawingColor.FromArgb(220, 220, 220);

            // Show ALL entries, not limited
            var tableEntries = entries.OrderBy(e => e.Date).ToList();  // Chronological order

            // Column setup with proper width distribution
            var columns = new[] { "Date", "PHQ-9", "GAD-7", "BDI-II", "PCL-5", "Y-BOCS" };
            var baseColWidth = tableWidth / columns.Length;
            var remainder = tableWidth % columns.Length;

            var headerRowHeight = rowHeight + 4;
            var currentY = y;

            // Create reusable brushes and pens outside the loop
            using var altRowBrush = new SolidBrush(lightGray);
            using var separatorPen = new System.Drawing.Pen(subtleGray, 1);
            using var lightGrayPen = new System.Drawing.Pen(DrawingColor.LightGray, 1);
            using var whitePen = new System.Drawing.Pen(DrawingColor.White, 1);
            using var grayPen = new System.Drawing.Pen(DrawingColor.Gray, 1);
            using var blackBrush = new SolidBrush(DrawingColor.Black);

            // Draw header
            using (var headerBrush = new SolidBrush(reconnectBlue))
            {
                g.FillRectangle(headerBrush, x, currentY, tableWidth, headerRowHeight);

                var currentX = x;
                for (int i = 0; i < columns.Length; i++)
                {
                    var colWidth = baseColWidth + (i == columns.Length - 1 ? remainder : 0);

                    g.DrawString(columns[i], headerFont, DrawingBrushes.White,
                                currentX + 8, currentY + 6);

                    if (i < columns.Length - 1)
                    {
                        g.DrawLine(whitePen, currentX + colWidth, currentY,
                                  currentX + colWidth, currentY + headerRowHeight);
                    }

                    currentX += colWidth;
                }
            }
            currentY += headerRowHeight;

            // Draw ALL data rows
            for (int row = 0; row < tableEntries.Count; row++)
            {
                var entry = tableEntries[row];
                var isAlternate = row % 2 == 1;

                // Alternate row background
                if (isAlternate)
                {
                    g.FillRectangle(altRowBrush, x, currentY, tableWidth, rowHeight);
                }

                // Data values
                var values = new string[]
                {
            entry.Date.ToString("yyyy-MM-dd"),
            entry.PHQ9?.ToString() ?? "—",
            entry.GAD7?.ToString() ?? "—",
            entry.BDI2?.ToString() ?? "—",
            entry.PCL5?.ToString() ?? "—",
            entry.YBOCS?.ToString() ?? "—"
                };

                // Draw cells
                var currentX = x;
                for (int i = 0; i < values.Length; i++)
                {
                    var colWidth = baseColWidth + (i == values.Length - 1 ? remainder : 0);

                    g.DrawString(values[i], cellFont, blackBrush,
                                currentX + 8, currentY + 4);

                    if (i < values.Length - 1)
                    {
                        g.DrawLine(lightGrayPen, currentX + colWidth, currentY,
                                  currentX + colWidth, currentY + rowHeight);
                    }

                    currentX += colWidth;
                }

                // Draw row separator
                g.DrawLine(separatorPen, x, currentY + rowHeight,
                          x + tableWidth, currentY + rowHeight);

                currentY += rowHeight;
            }

            // Draw table border
            g.DrawRectangle(grayPen, x, y, tableWidth, currentY - y);

            // Add summary text below table
            currentY += 10;
            using (var summaryFont = new DrawingFont("Arial", 8, DrawingFontStyle.Italic))
            {
                g.DrawString($"Table shows all {tableEntries.Count} assessment records in chronological order",
                            summaryFont, new SolidBrush(DrawingColor.DarkGray), x, currentY);
            }

            return currentY + 10;
        }

        // Optimized text wrapping with cached space width
        private List<string> WrapTextOptimized(string text, DrawingFont font, int maxWidth, DrawingGraphics g, float spaceWidth)
        {
            var lines = new List<string>();
            var words = text.Split(' ');
            var currentLine = "";
            var currentWidth = 0f;

            foreach (var word in words)
            {
                var wordWidth = g.MeasureString(word, font).Width;
                var testWidth = currentWidth == 0 ? wordWidth : currentWidth + spaceWidth + wordWidth;

                if (testWidth > maxWidth && !string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                    currentLine = word;
                    currentWidth = wordWidth;
                }
                else
                {
                    if (string.IsNullOrEmpty(currentLine))
                    {
                        currentLine = word;
                        currentWidth = wordWidth;
                    }
                    else
                    {
                        currentLine += " " + word;
                        currentWidth = testWidth;
                    }
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }

            return lines;
        }

        private int CreateDataTable(DrawingGraphics g, List<ScoreEntry> entries, int x, int y, int tableWidth,
                            int maxRows, DrawingFont cellFont, DrawingFont headerFont, int rowHeight)
        {
            var reconnectBlue = DrawingColor.FromArgb(43, 95, 117);
            var lightGray = DrawingColor.FromArgb(240, 240, 240);
            var subtleGray = DrawingColor.FromArgb(220, 220, 220);

            // Take most recent entries for the table
            var tableEntries = entries.TakeLast(maxRows).ToList();

            // Column setup with proper width distribution
            var columns = new[] { "Date", "PHQ-9", "GAD-7", "BDI-II", "PCL-5", "Y-BOCS" };
            var baseColWidth = tableWidth / columns.Length;
            var remainder = tableWidth % columns.Length;

            var headerRowHeight = rowHeight + 4;
            var currentY = y;

            // FIXED: Create new pens instead of using system pens
            using var altRowBrush = new SolidBrush(lightGray);
            using var separatorPen = new System.Drawing.Pen(subtleGray, 1);
            using var lightGrayPen = new System.Drawing.Pen(DrawingColor.LightGray, 1);
            using var whitePen = new System.Drawing.Pen(DrawingColor.White, 1);
            using var grayPen = new System.Drawing.Pen(DrawingColor.Gray, 1);
            using var blackBrush = new SolidBrush(DrawingColor.Black);

            // Draw header
            using (var headerBrush = new SolidBrush(reconnectBlue))
            {
                g.FillRectangle(headerBrush, x, currentY, tableWidth, headerRowHeight);

                var currentX = x;
                for (int i = 0; i < columns.Length; i++)
                {
                    // Add remainder pixels to the last column for perfect fit
                    var colWidth = baseColWidth + (i == columns.Length - 1 ? remainder : 0);

                    g.DrawString(columns[i], headerFont, DrawingBrushes.White,
                                currentX + 8, currentY + 6);

                    // Draw column separator with new pen
                    if (i < columns.Length - 1)
                    {
                        g.DrawLine(whitePen, currentX + colWidth, currentY,
                                  currentX + colWidth, currentY + headerRowHeight);
                    }

                    currentX += colWidth;
                }
            }
            currentY += headerRowHeight;

            // Draw data rows
            for (int row = 0; row < tableEntries.Count; row++)
            {
                var entry = tableEntries[row];
                var isAlternate = row % 2 == 1;

                // Alternate row background
                if (isAlternate)
                {
                    g.FillRectangle(altRowBrush, x, currentY, tableWidth, rowHeight);
                }

                // Data values with ISO 8601 date format
                var values = new string[]
                {
            entry.Date.ToString("yyyy-MM-dd"),
            entry.PHQ9?.ToString() ?? "—",
            entry.GAD7?.ToString() ?? "—",
            entry.BDI2?.ToString() ?? "—",
            entry.PCL5?.ToString() ?? "—",
            entry.YBOCS?.ToString() ?? "—"
                };

                // Draw cells with proper column width distribution
                var currentX = x;
                for (int i = 0; i < values.Length; i++)
                {
                    var colWidth = baseColWidth + (i == values.Length - 1 ? remainder : 0);

                    g.DrawString(values[i], cellFont, blackBrush,
                                currentX + 8, currentY + 4);

                    // Draw column separator
                    if (i < values.Length - 1)
                    {
                        g.DrawLine(lightGrayPen, currentX + colWidth, currentY,
                                  currentX + colWidth, currentY + rowHeight);
                    }

                    currentX += colWidth;
                }

                // Draw subtle row separator
                g.DrawLine(separatorPen, x, currentY + rowHeight,
                          x + tableWidth, currentY + rowHeight);

                currentY += rowHeight;
            }

            // Draw table border with new pen
            g.DrawRectangle(grayPen, x, y, tableWidth, currentY - y);

            return currentY;
        }
        private int CreateProgressChart(DrawingGraphics g, List<ScoreEntry> entries,
                                        int x, int y, int chartWidth, int chartHeight)
        {
            if (!entries.Any()) return y + 50;

            // Keep inner plot area sane even if chartHeight is small
            var innerH = Math.Max(120, chartHeight - 100);
            var chartArea = new DrawingRectangle(x + 60, y + 40, chartWidth - 120, innerH);

            using var whiteBrush = new SolidBrush(DrawingColor.White);
            using var grayPen = new System.Drawing.Pen(DrawingColor.Gray, 1);
            using var lightGrayPen = new System.Drawing.Pen(DrawingColor.LightGray, 1);
            using var blackBrush = new SolidBrush(DrawingColor.Black);
            using var arialFont8 = new DrawingFont("Arial", 8);

            g.FillRectangle(whiteBrush, chartArea);
            g.DrawRectangle(grayPen, chartArea);

            // Build range
            var allScores = new List<int>();
            foreach (var e in entries)
            {
                if (e.PHQ9.HasValue) allScores.Add(e.PHQ9.Value);
                if (e.GAD7.HasValue) allScores.Add(e.GAD7.Value);
                if (e.BDI2.HasValue) allScores.Add(e.BDI2.Value);
                if (e.PCL5.HasValue) allScores.Add(e.PCL5.Value);
                if (e.YBOCS.HasValue) allScores.Add(e.YBOCS.Value);
            }

            double minScore = allScores.Any() ? Math.Max(0, allScores.Min() - 5) : 0;
            double maxScore = allScores.Any() ? Math.Min(80, allScores.Max() + 10) : 80;

            // ✅ avoid zero range (flat line)
            if (Math.Abs(maxScore - minScore) < 0.0001)
                maxScore = minScore + 1;

            double scoreRange = maxScore - minScore;

            // ✅ grid steps: at least 1
            int gridSteps = Math.Max(1, Math.Min(8, (int)Math.Ceiling(scoreRange / 10.0)));

            for (int i = 0; i <= gridSteps; i++)
            {
                var gridY = (float)(chartArea.Y + (i * chartArea.Height / (double)gridSteps));
                g.DrawLine(lightGrayPen, chartArea.X, gridY, chartArea.Right, gridY);

                var value = maxScore - (i * scoreRange / gridSteps);
                g.DrawString(((int)Math.Round(value)).ToString(), arialFont8, blackBrush,
                             chartArea.X - 30, gridY - 6);
            }

            // vertical grid
            for (int i = 0; i <= 10; i++)
            {
                var gridX = chartArea.X + (i * chartArea.Width / 10.0);
                g.DrawLine(lightGrayPen, (float)gridX, chartArea.Y, (float)gridX, chartArea.Bottom);
            }

            // series
            PlotAssessmentLineFixed(g, entries, chartArea, e => e.PHQ9, DrawingColor.Blue, "PHQ-9", minScore, maxScore);
            PlotAssessmentLineFixed(g, entries, chartArea, e => e.GAD7, DrawingColor.Green, "GAD-7", minScore, maxScore);
            PlotAssessmentLineFixed(g, entries, chartArea, e => e.BDI2, DrawingColor.Orange, "BDI-II", minScore, maxScore);
            PlotAssessmentLineFixed(g, entries, chartArea, e => e.PCL5, DrawingColor.DarkCyan, "PCL-5", minScore, maxScore);
            PlotAssessmentLineFixed(g, entries, chartArea, e => e.YBOCS, DrawingColor.Purple, "Y-BOCS", minScore, maxScore);

            // bottom elements
            var currentBottomY = chartArea.Bottom;

            int dateStep = Math.Max(1, entries.Count / 6);
            for (int i = 0; i < entries.Count; i += dateStep)
            {
                var ptX = chartArea.X + (i * chartArea.Width / Math.Max(1.0, entries.Count - 1));
                g.DrawString(entries[i].Date.ToString("dd-MMM"), arialFont8, blackBrush,
                             (float)ptX - 20, currentBottomY + 10);
            }
            currentBottomY += 25;

            // ✅ FIXED: Use the actual return value from legend method
            currentBottomY = DrawChartLegendHorizontal(g, chartArea.X, currentBottomY + 15, chartArea.Width);

            return currentBottomY + 10; // padding
        }

        private int DrawChartLegendHorizontal(DrawingGraphics g, int x, int y, int width)
        {
            var legendItems = new[]
            {
        ("PHQ-9", DrawingColor.Blue),
        ("GAD-7", DrawingColor.Green),
        ("BDI-II", DrawingColor.Orange),
        ("PCL-5", DrawingColor.DarkCyan),
        ("Y-BOCS", DrawingColor.Purple)
    };

            // Guard against zero width
            var safeWidth = Math.Max(300, width);
            var itemWidth = safeWidth / legendItems.Length;
            var currentX = x;
            const int legendHeight = 20;

            using (var arialFont = new DrawingFont("Arial", 9))
            using (var blackBrush = new SolidBrush(DrawingColor.Black))
            {
                foreach (var (label, color) in legendItems)
                {
                    // Draw line sample
                    using (var pen = new System.Drawing.Pen(color, 3))
                    {
                        g.DrawLine(pen, currentX, y, currentX + 20, y);
                    }

                    // Draw label with bounds checking
                    var labelX = Math.Min(currentX + 25, x + safeWidth - 50); // Prevent overflow
                    g.DrawString(label, arialFont, blackBrush, labelX, y - 5);
                    currentX += itemWidth;
                }
            }

            return y + legendHeight; // Return actual bottom position
        }
        private void PlotAssessmentLineFixed(DrawingGraphics g, List<ScoreEntry> entries, DrawingRectangle chartArea,
                                 Func<ScoreEntry, int?> scoreSelector, DrawingColor color,
                                 string label, double minScore, double maxScore)
        {
            var points = new List<DrawingPointF>();
            var scoreRange = maxScore - minScore;

            for (int i = 0; i < entries.Count; i++)
            {
                var score = scoreSelector(entries[i]);
                if (score.HasValue)
                {
                    // Correct X positioning
                    var x = chartArea.X + (entries.Count == 1 ? chartArea.Width / 2 :
                           (i * chartArea.Width / Math.Max(1, entries.Count - 1)));

                    // Correct Y positioning with proper scaling
                    var normalizedScore = (score.Value - minScore) / scoreRange;
                    var y = chartArea.Bottom - (int)(normalizedScore * chartArea.Height);

                    points.Add(new DrawingPointF(x, y));
                }
            }

            if (points.Count > 1)
            {
                // FIXED: Create new pen instead of modifying system pen
                using (var linePen = new System.Drawing.Pen(color, 3))
                {
                    g.DrawLines(linePen, points.ToArray());
                }
            }

            // Draw points with score labels
            using (var brush = new SolidBrush(color))
            using (var font = new DrawingFont("Arial", 8, DrawingFontStyle.Bold))
            using (var whitePen = new System.Drawing.Pen(DrawingColor.White, 1))
            using (var blackBrush = new SolidBrush(DrawingColor.Black))
            {
                for (int i = 0; i < points.Count; i++)
                {
                    var point = points[i];

                    // Draw point
                    g.FillEllipse(brush, point.X - 4, point.Y - 4, 8, 8);
                    g.DrawEllipse(whitePen, point.X - 4, point.Y - 4, 8, 8);

                    // Add score labels on points
                    var scoreValue = scoreSelector(entries[GetEntryIndexForPoint(entries, i, scoreSelector)]);
                    if (scoreValue.HasValue)
                    {
                        g.DrawString(scoreValue.Value.ToString(), font, blackBrush,
                                   point.X - 8, point.Y - 20);
                    }
                }
            }
        }

        // Helper method to get the correct entry index for a point
        private int GetEntryIndexForPoint(List<ScoreEntry> entries, int pointIndex, Func<ScoreEntry, int?> scoreSelector)
        {
            int currentPointIndex = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (scoreSelector(entries[i]).HasValue)
                {
                    if (currentPointIndex == pointIndex)
                        return i;
                    currentPointIndex++;
                }
            }
            return 0; // Fallback
        }

        private async void DeletePatient_Click(object sender, RoutedEventArgs e)
        {
            //  Allow both Admin and Doctor to delete patients
            if (!RoleHelper.IsAdmin(currentUser) && !RoleHelper.IsDoctor(currentUser))
            {
                MessageBox.Show("⚠️ Access Denied\n\nOnly Administrators and Doctors can delete patient data.",
                               "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedPatientId = PatientSelector.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(selectedPatientId))
            {
                MessageBox.Show("Please select a patient to delete.", "No Patient Selected",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new AppDbContext();

                // Get all entries for this patient from DATABASE
                var patientEntries = await context.ScoreEntries
                    .Where(e => e.PatientId == selectedPatientId)
                    .ToListAsync();

                if (!patientEntries.Any())
                {
                    MessageBox.Show("No data found for this patient in database.", "No Data",
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var patientSummary = $"Patient ID: {selectedPatientId}\n" +
                                    $"Total Assessments: {patientEntries.Count}\n" +
                                    $"Date Range: {patientEntries.Min(e => e.Date):yyyy-MM-dd} to {patientEntries.Max(e => e.Date):yyyy-MM-dd}";

                // Confirmation dialog
                var confirmResult = MessageBox.Show(
                    $"🚨 PATIENT DATA DELETION\n\n" +
                    $"You are about to PERMANENTLY delete ALL data for:\n\n" +
                    $"{patientSummary}\n\n" +
                    $"⚠️ THIS ACTION CANNOT BE UNDONE!\n\n" +
                    $"Continue with deletion?",
                    "Confirm Patient Data Deletion",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirmResult == MessageBoxResult.No) return;

                // Final confirmation
                var inputDialog = Microsoft.VisualBasic.Interaction.InputBox(
                    "Type 'DELETE' to confirm permanent removal:",
                    "Final Confirmation", "");

                if (inputDialog.ToUpper() != "DELETE")
                {
                    MessageBox.Show("Deletion cancelled.", "Cancelled",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }


                // DELETE FROM DATABASE
                context.ScoreEntries.RemoveRange(patientEntries);
                await context.SaveChangesAsync();

                // Log the deletion
                await _auditService.LogActionAsync("DELETE_PATIENT", selectedPatientId,
                                                 $"Deleted {patientEntries.Count} records for patient {selectedPatientId}");

                // COMPREHENSIVE UI REFRESH
                // 1. Remove from in-memory dictionary
                if (patientData.ContainsKey(selectedPatientId))
                {
                    patientData.Remove(selectedPatientId);
                }

                // 2. Remove from patient selector
                PatientSelector.Items.Remove(selectedPatientId);

                // 3. Clear current selection and display
                PatientSelector.SelectedItem = null;
                ClearPatientDisplay();

                // 4. Reload all data from database to ensure sync
                await LoadAllPatientsFromDatabase();

                // 5. Reset the patient ID box
                PatientIdBox.Clear();

                // 6. Refresh the main data grid
                ScoresGrid.ItemsSource = null;

                MessageBox.Show($"✅ Patient {selectedPatientId} deleted successfully from database!\n\n" +
                               $"Records deleted: {patientEntries.Count}",
                               "Deletion Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error deleting patient: {ex.Message}", "Database Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Helper method to get assessment types summary
        private string GetAssessmentTypesSummary(List<ScoreEntry> entries)
        {
            var types = new List<string>();
            if (entries.Any(e => e.PHQ9.HasValue)) types.Add("PHQ-9");
            if (entries.Any(e => e.GAD7.HasValue)) types.Add("GAD-7");
            if (entries.Any(e => e.BDI2.HasValue)) types.Add("BDI-II");
            if (entries.Any(e => e.PCL5.HasValue)) types.Add("PCL-5");
            if (entries.Any(e => e.YBOCS.HasValue)) types.Add("Y-BOCS");
            return types.Any() ? string.Join(", ", types) : "None";
        }

        // Helper method to clear patient display
        private void ClearPatientDisplay()
        {
            // Clear chart data
            Phq9Values.Clear();
            Gad7Values.Clear();
            Bdi2Values.Clear();
            Pcl5Values.Clear();
            YbocsValues.Clear();

            // Clear data grid
            ScoresGrid.ItemsSource = null;

            // Clear chart notes
            ChartNotesCanvas.Children.Clear();

            // Clear current patient entries
            currentPatientEntries.Clear();
        }



        private void NoteBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is string txt)
                MessageBox.Show(txt, "Full Treatment Note");
        }

        private bool _handlingSelection;

        private async void PatientSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_handlingSelection) return;          // ignore re-entrant calls
            _handlingSelection = true;
            try
            {
                if (PatientSelector.SelectedItem is not string id || id.Length == 0)
                    return;

                await LoadPatientDataFromDatabase(id);   // touches DB for *one* patient
                UpdateChartForPatient(id);
                PatientIdBox.Text = id;

                await RecalculateMetricsOnlyAsync();     // no DB, no Items.Clear()
                UpdateCurrentPatientOutcome(id);         // refresh right-hand pane
            }
            finally
            {
                _handlingSelection = false;
            }
        }
        private Task RecalculateMetricsOnlyAsync()
        {
            currentMetrics = metricsService.CalculateCombinedMetrics(patientData);
            UpdateMetricsDisplay(currentMetrics);
            return Task.CompletedTask;
        }


/*        private async Task RecalculateAndRefreshWithCacheAsync(string focusPatientId = null)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // Only recalculate if:
                // 1. We've never calculated before (currentMetrics is null)
                // 2. The data is stale (older than 5 minutes)
                // 3. Patient data has been modified since last calculation

                bool shouldRecalculate = currentMetrics == null ||
                                         DateTime.Now - currentMetrics.CalculatedOn > TimeSpan.FromMinutes(5) ||
                                         HasDataChangedSinceLastCalculation();

                if (shouldRecalculate)
                {
                    // Full recalculation
                    await LoadAllPatientsFromDatabase();
                    currentMetrics = metricsService.CalculateCombinedMetrics(patientData);
                    UpdateMetricsDisplay(currentMetrics);

                    System.Diagnostics.Debug.WriteLine($"Full metrics recalculation at {DateTime.Now:HH:mm:ss}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Using cached metrics from {currentMetrics.CalculatedOn:HH:mm:ss}");
                }

                // Always update the selected patient display
                if (!string.IsNullOrWhiteSpace(focusPatientId))
                {
                    UpdateCurrentPatientOutcome(focusPatientId);
                }

                // Auto-expand metrics panel if needed
                if (!isMetricsExpanded)
                {
                    isMetricsExpanded = true;
                    MetricsContent.Visibility = Visibility.Visible;
                    MetricsToggleIcon.Text = "▼";
                    QuickStats.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error calculating metrics: {ex.Message}", "Calculation Error",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }*/

        private DateTime lastDataModification = DateTime.Now;
        private bool HasDataChangedSinceLastCalculation()
        {
            // This gets set to true whenever AddScore, Delete, or Import happens
            // For now, return false to use time-based caching only
            return currentMetrics != null && lastDataModification > currentMetrics.CalculatedOn;
        }

        private async Task LoadPatientDataFromDatabase(string patientId)
        {
            try
            {
                var entries = await _dbContext.ScoreEntries
                    .Where(e => e.PatientId == patientId)
                    .OrderBy(e => e.Date)
                    .ToListAsync();

                patientData[patientId] = entries;
                ScoresGrid.ItemsSource = entries;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading patient data: {ex.Message}");
                MessageBox.Show($"Error loading patient data: {ex.Message}", "Database Error",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ResetMetricsDisplay()
        {
            // Reset PHQ-9 Response displays
            ResponseRateText.Text = "0.0%";
            ResponseCountText.Text = "(0/0)";

            // Reset BDI-II Remission displays  
            RemissionRateText.Text = "0.0%";
            RemissionCountText.Text = "(0/0)";

            // Reset combined displays
            AverageImprovementText.Text = "0.0%";

            // CHANGED: Use TotalEligibleText instead of EligiblePatientsText
            TotalEligibleText.Text = "0";

            // Reset individual eligible counts
            PHQ9EligibleText.Text = "0";
            BDI2EligibleText.Text = "0";

            QuickResponseRate.Text = "0.0%";
            QuickRemissionRate.Text = "0.0%";

            // Reset colors to default
            ResponseRateText.Foreground = Brushes.Black;
            RemissionRateText.Foreground = Brushes.Black;

            // Reset patient outcome displays
            CurrentPatientOutcomeTitle.Text = "No patient selected";
            CurrentPatientOutcomeDetails.Text = "Select a patient from the dropdown to view their clinical outcomes analysis.";
            CurrentPatientProgressSummary.Text = "Patient-specific response and remission status will appear here when a patient is selected.";
        }

        private async void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            filterId = FilterBox.Text.Trim();

            try
            {
                using var context = new AppDbContext();

                // FILTER FROM DATABASE instead of patientData dictionary
                var filteredEntries = await _dbContext.ScoreEntries
                    .Where(r => string.IsNullOrEmpty(filterId) ||
                               r.PatientId.ToLower().Contains(filterId.ToLower()))
                    .OrderBy(r => r.PatientId)
                    .ThenBy(r => r.Date)
                    .ToListAsync();

                ScoresGrid.ItemsSource = filteredEntries;

                // Debug info
                System.Diagnostics.Debug.WriteLine($"Filter '{filterId}' returned {filteredEntries.Count} entries");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Filter error: {ex.Message}");
                MessageBox.Show($"Filter error: {ex.Message}", "Filter Error",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void CalculateMetrics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedId = PatientSelector.SelectedItem?.ToString();
                await RecalculateAndRefreshAsync(selectedId);

                // Show confirmation that manual calculation completed
                MessageBox.Show($"✅ Clinical outcomes calculated!\n\n" +
                               $"PHQ-9 RESPONSE ANALYSIS:\n" +
                               $"  • Current response rate: {currentMetrics.ResponseRate:F1}%\n" +
                               $"  • Ever achieved response: {currentMetrics.EverAchievedResponseRate:F1}%\n\n" +
                               $"BDI-II REMISSION ANALYSIS:\n" +
                               $"  • Current remission rate: {currentMetrics.RemissionRate:F1}%\n" +
                               $"  • Ever achieved remission: {currentMetrics.EverAchievedRemissionRate:F1}%",
                               "Clinical Outcomes", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error calculating metrics: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool isMetricsExpanded = false;

        private void ToggleMetrics_Click(object sender, MouseButtonEventArgs e)
        {
            isMetricsExpanded = !isMetricsExpanded;

            if (isMetricsExpanded)
            {
                MetricsContent.Visibility = Visibility.Visible;
                MetricsToggleIcon.Text = "▼";
                QuickStats.Visibility = Visibility.Collapsed;
            }
            else
            {
                MetricsContent.Visibility = Visibility.Collapsed;
                MetricsToggleIcon.Text = "▶";
                QuickStats.Visibility = Visibility.Visible;
            }
        }

        private void UpdateMetricsDisplay(ClinicalMetricsService.ClinicalMetrics metrics)
        {
            // PHQ-9 Response metrics (≥50% improvement)
            ResponseRateText.Text = $"{metrics.ResponseRate:F1}%";
            ResponseCountText.Text = $"({metrics.ResponseCount}/{metrics.EligibleForResponse})";

            // BDI-II Remission metrics (score ≤14)
            RemissionRateText.Text = $"{metrics.RemissionRate:F1}%";
            RemissionCountText.Text = $"({metrics.RemissionCount}/{metrics.EligibleForRemission})";

            // Combined metrics
            AverageImprovementText.Text = $"{metrics.AverageImprovement:F1}%";

            // CHANGED: Use TotalEligibleText instead of EligiblePatientsText
            TotalEligibleText.Text = metrics.PatientsWithMultipleAssessments.ToString();

            // Individual eligible counts
            PHQ9EligibleText.Text = metrics.EligibleForResponse.ToString();
            BDI2EligibleText.Text = metrics.EligibleForRemission.ToString();

            // Quick stats
            QuickResponseRate.Text = $"{metrics.ResponseRate:F1}%";
            QuickRemissionRate.Text = $"{metrics.RemissionRate:F1}%";

            // Color coding - different thresholds for response vs remission
            ResponseRateText.Foreground = metrics.ResponseRate >= 50 ? Brushes.Green :
                                         metrics.ResponseRate >= 30 ? new SolidColorBrush(Color.FromRgb(70, 130, 180)) : Brushes.Red;

            RemissionRateText.Foreground = metrics.RemissionRate >= 30 ? new SolidColorBrush(Color.FromRgb(139, 69, 19)) :
                                          metrics.RemissionRate >= 15 ? new SolidColorBrush(Color.FromRgb(255, 140, 0)) : Brushes.Red;
        }


        private void UpdateCurrentPatientOutcome(string patientId)
        {
            if (currentMetrics == null || !patientData.ContainsKey(patientId))
            {
                CurrentPatientOutcomeTitle.Text = "No patient selected";
                CurrentPatientOutcomeDetails.Text = "Select a patient from the dropdown to view their clinical outcomes analysis.";
                CurrentPatientProgressSummary.Text = "Patient-specific response and remission status will appear here when a patient is selected.";
                return;
            }

            var outcome = currentMetrics.PatientOutcomes.FirstOrDefault(p => p.PatientId == patientId);
            if (outcome == null)
            {
                CurrentPatientOutcomeTitle.Text = $"Patient {patientId}: Insufficient Data";
                CurrentPatientOutcomeDetails.Text = "This patient does not have sufficient assessment data for clinical outcome analysis. Requires at least 2 assessments with PHQ-9 and/or BDI-II scores.";
                CurrentPatientProgressSummary.Text = "Unable to calculate response or remission status.";
                return;
            }

            CurrentPatientOutcomeTitle.Text = $"Patient {patientId} - Clinical Outcomes";

            var details = new StringBuilder();
            var summary = new StringBuilder();

            // PHQ-9 Response information
            if (outcome.BaselinePHQ9.HasValue && outcome.MostRecentPHQ9.HasValue)
            {
                details.AppendLine($"PHQ-9 RESPONSE ANALYSIS:");
                details.AppendLine($"Baseline: {outcome.BaselinePHQ9} ({outcome.PHQ9BaselineDate:yyyy-MM-dd}) → Most Recent: {outcome.MostRecentPHQ9} ({outcome.PHQ9MostRecentDate:yyyy-MM-dd})");
                details.AppendLine($"Improvement: {outcome.PHQ9PercentImprovement:F1}% over {outcome.DaysBetweenAssessments} days");
                details.AppendLine($"Response (≥50% improvement): {(outcome.HasResponse ? "YES" : "NO")} | Remission (score ≤14): N/A");
                details.AppendLine($"Ever Achieved Response: {(outcome.EverAchievedResponse ? "YES" : "NO")} | Ever Achieved Remission: N/A");

                if (outcome.EverAchievedResponse && outcome.FirstResponseDate.HasValue)
                {
                    details.AppendLine($"First Response Date: {outcome.FirstResponseDate.Value:yyyy-MM-dd}");
                    details.AppendLine($"Best Improvement: {outcome.BestPHQ9Improvement:F1}%");
                }

                summary.AppendLine($"PHQ-9 Response: {(outcome.HasResponse ? "✓ CURRENTLY YES" : "✗ Currently No")}");
                summary.AppendLine($"Ever Achieved Response: {(outcome.EverAchievedResponse ? "✓ YES" : "✗ NO")}");
            }
            else
            {
                details.AppendLine($"PHQ-9 RESPONSE ANALYSIS: Insufficient data (need ≥2 PHQ-9 assessments)");
            }

            // BDI-II Remission information
            if (outcome.BaselineBDI2.HasValue && outcome.MostRecentBDI2.HasValue)
            {
                details.AppendLine();
                details.AppendLine($"BDI-II REMISSION ANALYSIS:");
                details.AppendLine($"Baseline BDI-II: {outcome.BaselineBDI2} ({outcome.BDI2BaselineDate:yyyy-MM-dd}) → Most Recent: {outcome.MostRecentBDI2} ({outcome.BDI2MostRecentDate:yyyy-MM-dd})");
                details.AppendLine($"Improvement: {outcome.BDI2PercentImprovement:F1}% over {outcome.DaysBetweenAssessments} days");
                details.AppendLine($"Response (≥50% improvement): {(outcome.BDI2PercentImprovement >= 50 ? "YES" : "NO")} | Remission (score ≤14): {(outcome.HasRemission ? "YES" : "NO")}");
                details.AppendLine($"Ever Achieved Response: {(outcome.BDI2PercentImprovement >= 50 ? "YES" : "NO")} | Ever Achieved Remission: {(outcome.EverAchievedRemission ? "YES" : "NO")}");
                details.AppendLine($"Total assessments: {outcome.TotalAssessments}");

                if (outcome.EverAchievedRemission)
                {
                    if (outcome.FirstRemissionDate.HasValue)
                        details.AppendLine($"First Remission Date: {outcome.FirstRemissionDate.Value:yyyy-MM-dd}");
                    if (outcome.LowestBDI2Score.HasValue)
                        details.AppendLine($"Lowest BDI-II Score: {outcome.LowestBDI2Score}");
                }

                summary.AppendLine($"BDI-II Remission: {(outcome.HasRemission ? "✓ CURRENTLY YES" : "✗ Currently No")} (current score: {outcome.MostRecentBDI2})");
                summary.AppendLine($"Ever Achieved Remission: {(outcome.EverAchievedRemission ? "✓ YES" : "✗ NO")}");
            }
            else
            {
                details.AppendLine();
                details.AppendLine($"BDI-II REMISSION ANALYSIS: Insufficient data (need ≥2 BDI-II assessments)");
            }

            // Overall clinical status
            summary.AppendLine();
            if ((outcome.HasResponse || outcome.EverAchievedResponse) && (outcome.HasRemission || outcome.EverAchievedRemission))
                summary.AppendLine($"Overall Status: ✓ EXCELLENT (Achieved both outcomes)");
            else if (outcome.HasResponse || outcome.EverAchievedResponse || outcome.HasRemission || outcome.EverAchievedRemission)
                summary.AppendLine($"Overall Status: ⚠ PARTIAL (Some outcomes achieved)");
            else if (outcome.BaselinePHQ9.HasValue || outcome.BaselineBDI2.HasValue)
                summary.AppendLine($"Overall Status: ✗ NEEDS IMPROVEMENT (No outcomes achieved yet)");
            else
                summary.AppendLine($"Overall Status: ? INSUFFICIENT DATA");

            CurrentPatientOutcomeDetails.Text = details.ToString();
            CurrentPatientProgressSummary.Text = summary.ToString();
        }
        private void ExportMetricsReport_Click(object sender, RoutedEventArgs e)
        {
            if (currentMetrics == null)
            {
                MessageBox.Show("Please calculate metrics first.", "No Data",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var report = metricsService.GenerateMetricsReport(currentMetrics);

                // Show Save Dialog
                var saveDialog = new SaveFileDialog
                {
                    Title = "Save Clinical Metrics Report",
                    Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    FileName = $"BDI2_ClinicalOutcomes_{DateTime.Now:yyyyMMdd_HHmm}.txt",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveDialog.FileName, report);

                    MessageBox.Show($"Clinical outcomes report saved successfully!\n\nLocation: {saveDialog.FileName}",
                                   "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting report: {ex.Message}", "Export Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void UserManagement_Click(object sender, RoutedEventArgs e)
{
    try
    {
        // Only admins can access user management
        if (!RoleHelper.IsAdmin(currentUser))
        {
            MessageBox.Show("Only administrators can manage user accounts.", 
                           "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Create and show user management window
        var userManagementWindow = new UserManagementWindow(authService, _auditService, _dbContext);
        userManagementWindow.Owner = this;
        userManagementWindow.ShowDialog();
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error opening user management: {ex.Message}", "Error",
                       MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
        private void ShowAllTimeRemissions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var analysis = remissionService.AnalyzeAllTimeRemissions(patientData);
                var remissionWindow = new RemissionHistoryWindow(analysis);
                remissionWindow.Owner = this;
                remissionWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error analyzing remissions: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExportToCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new AppDbContext();

                // LOAD FROM DATABASE
                var allEntries = await _dbContext.ScoreEntries
                    .Where(s => string.IsNullOrWhiteSpace(filterId) || s.PatientId.Contains(filterId))
                    .OrderBy(s => s.PatientId)
                    .ThenBy(s => s.Date)
                    .ToListAsync();

                if (!allEntries.Any())
                {
                    MessageBox.Show("No matching data to export from database.");
                    return;
                }

                // Show Save Dialog
                var saveDialog = new SaveFileDialog
                {
                    Title = "Save Patient Data Export",
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    FileName = $"PatientScores_Export_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                if (saveDialog.ShowDialog() == true)
                {
                    await _auditService.LogActionAsync("EXPORT_DATA", filterId, $"Exported {allEntries.Count} records");

                    var sb = new StringBuilder();
                    sb.AppendLine("PatientId,Date,PHQ9,GAD7,BDI2,PCL5,YBOCS,Note,CreatedBy,CreatedAt");

                    foreach (var s in allEntries)
                    {
                        var phq9Str = s.PHQ9?.ToString() ?? "-";
                        var gad7Str = s.GAD7?.ToString() ?? "-";
                        var bdi2Str = s.BDI2?.ToString() ?? "-";
                        var pcl5Str = s.PCL5?.ToString() ?? "-";
                        var ybocsStr = s.YBOCS?.ToString() ?? "-";
                        var note = s.Note?.Replace("\"", "\"\"") ?? "";

                        sb.AppendLine($"{s.PatientId},{s.Date:yyyy-MM-dd},{phq9Str},{gad7Str},{bdi2Str},{pcl5Str},{ybocsStr},\"{note}\",{s.CreatedBy},{s.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                    }

                    File.WriteAllText(saveDialog.FileName, sb.ToString());

                    MessageBox.Show($"✅ Exported {allEntries.Count} records successfully!\n\nSaved to: {saveDialog.FileName}",
                                   "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Export error: {ex.Message}", "Export Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task RecalculateAndRefreshAsync(string focusPatientId = null)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // 1. Ensure in-memory dictionary is up-to-date with database
                await LoadAllPatientsFromDatabase();

                // 2. (Re)calculate metrics for ALL patients
                currentMetrics = metricsService.CalculateCombinedMetrics(patientData);

                // 3. Update the summary tiles (Response Rate, Remission Rate, etc.)
                UpdateMetricsDisplay(currentMetrics);

                // 4. If a patient is selected, refresh their outcome block
                if (!string.IsNullOrWhiteSpace(focusPatientId))
                {
                    UpdateCurrentPatientOutcome(focusPatientId);
                }

                // 5. Auto-expand the metrics panel if it's collapsed
                if (!isMetricsExpanded)
                {
                    isMetricsExpanded = true;
                    MetricsContent.Visibility = Visibility.Visible;
                    MetricsToggleIcon.Text = "▼";
                    QuickStats.Visibility = Visibility.Collapsed;
                }

                System.Diagnostics.Debug.WriteLine($"Metrics auto-calculated at {DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error calculating metrics: {ex.Message}", "Calculation Error",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }


        private void BackupNowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // Export data as CSV string
                var csvData = ExportAllDataForBackup();

                // Prompt user with SaveFileDialog
                var saveDialog = new SaveFileDialog
                {
                    Title = "Save Backup File",
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"ReconnectData_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveDialog.FileName, csvData);
                    var fileInfo = new FileInfo(saveDialog.FileName);
                    MessageBox.Show($"✅ Data backup created successfully!\n\n" +
                                   $"File: {Path.GetFileName(saveDialog.FileName)}\n" +
                                   $"Location: {Path.GetDirectoryName(saveDialog.FileName)}\n" +
                                   $"Size: {fileInfo.Length / 1024:F1} KB\n" +
                                   $"Records: {patientData.Values.SelectMany(v => v).Count()}",
                                   "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Backup failed:\n{ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ADD this helper method for proper CSV escaping:
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            // If field contains comma, quote, or newline, wrap in quotes and escape internal quotes
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }

        // ADD this helper method to get data range info:
        private string GetDataDateRange()
        {
            var allEntries = patientData.Values.SelectMany(v => v).ToList();
            if (!allEntries.Any())
                return "No data";

            // Handle nullable DateTime properly
            var dates = allEntries.Select(e => e.Date).ToList();
            var earliest = dates.Min();
            var latest = dates.Max();

            if (earliest.Date == latest.Date)
                return earliest.ToString("yyyy-MM-dd");
            else
                return $"{earliest:yyyy-MM-dd} to {latest:yyyy-MM-dd}";
        }

        private void ImportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            // Admin-only check
            if (!RoleHelper.IsAdmin(currentUser))
            {
                MessageBox.Show("⚠️ Access Denied\n\nOnly Administrators can import data.",
                               "Admin Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Show import confirmation
                var confirmResult = MessageBox.Show(
                    "🔄 DATA IMPORT\n\n" +
                    "⚠️ Important:\n" +
                    "• This will import and merge CSV data\n" +
                    "• Existing data with same Patient ID + Date will be updated\n" +
                    "• New data will be added\n" +
                    "• This action cannot be undone\n\n" +
                    "📋 Recommended: Create a backup first!\n\n" +
                    "Continue with import?",
                    "Confirm Data Import",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmResult == MessageBoxResult.No) return;

                // Show file dialog to select CSV file
                var openDialog = new OpenFileDialog
                {
                    Title = "Select CSV File to Import",
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                if (openDialog.ShowDialog() == true)
                {
                    Mouse.OverrideCursor = Cursors.Wait;

                    try
                    {
                        // Read and parse CSV file
                        var importResult = ImportCsvData(openDialog.FileName);

                        if (importResult.Success)
                        {
                            // Refresh UI
                            RefreshUIAfterImport();

                            // Show detailed success message
                            MessageBox.Show($"✅ CSV Import Successful!\n\n" +
                                           $"📁 File: {Path.GetFileName(openDialog.FileName)}\n" +
                                           $"📊 Results:\n" +
                                           $"   • Records Imported: {importResult.ImportedCount}\n" +
                                           $"   • Records Updated: {importResult.UpdatedCount}\n" +
                                           $"   • Records Skipped: {importResult.SkippedCount}\n" +
                                           $"   • Patients Affected: {importResult.PatientsAffected}\n\n" +
                                           $"💡 {importResult.Message}\n\n" +
                                           $"🔄 UI has been refreshed with new data.",
                                           "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                            // Auto-select first imported patient if available
                            if (PatientSelector.Items.Count > 0)
                            {
                                PatientSelector.SelectedIndex = 0;
                            }
                        }
                        else
                        {
                            MessageBox.Show($"❌ Import Failed!\n\n" +
                                           $"📁 File: {Path.GetFileName(openDialog.FileName)}\n\n" +
                                           $"💬 Error Details:\n{importResult.Message}\n\n" +
                                           $"💡 Tips:\n" +
                                           $"• Check file format matches expected CSV structure\n" +
                                           $"• Ensure file is not corrupted\n" +
                                           $"• Try with a smaller test file first",
                                           "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"❌ Import failed:\n\n" +
                                       $"📁 File: {Path.GetFileName(openDialog.FileName)}\n\n" +
                                       $"💬 Error: {ex.Message}\n\n" +
                                       $"🔧 This might be due to:\n" +
                                       $"• File format issues\n" +
                                       $"• File access permissions\n" +
                                       $"• Invalid data in CSV",
                                       "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Import system error:\n\n{ex.Message}",
                               "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private ImportResult ImportCsvData(string filePath)
        {
            var result = new ImportResult();
            var importedEntries = new List<ScoreEntry>();
            var affectedPatients = new HashSet<string>();

            try
            {
                var lines = File.ReadAllLines(filePath);
                if (lines.Length == 0)
                {
                    result.Message = "CSV file is empty.";
                    return result;
                }

                // Find the header line (skip metadata lines starting with #)
                int headerIndex = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].StartsWith("#") && !string.IsNullOrWhiteSpace(lines[i]))
                    {
                        headerIndex = i;
                        break;
                    }
                }

                if (headerIndex == -1)
                {
                    result.Message = "No valid CSV header found.";
                    return result;
                }

                var header = lines[headerIndex];
                var expectedColumns = new[] { "PatientId", "Date", "PHQ9", "GAD7", "BDI2", "PCL5", "YBOCS", "Note" };

                // Validate header format
                if (!ValidateCsvHeader(header, expectedColumns))
                {
                    result.Message = $"Invalid CSV format. Expected columns: {string.Join(", ", expectedColumns)}";
                    return result;
                }

                // Parse data lines
                for (int i = headerIndex + 1; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    try
                    {
                        var entry = ParseCsvLine(line);
                        if (entry != null)
                        {
                            // Check for duplicates
                            var patientId = entry.PatientId;
                            var date = entry.Date.Date;

                            if (!patientData.ContainsKey(patientId))
                                patientData[patientId] = new List<ScoreEntry>();

                            var existingEntry = patientData[patientId].FirstOrDefault(e => e.Date.Date == date);

                            if (existingEntry != null)
                            {
                                // Update existing entry
                                UpdateExistingEntry(existingEntry, entry);
                                result.UpdatedCount++;
                            }
                            else
                            {
                                // Add new entry
                                patientData[patientId].Add(entry);
                                result.ImportedCount++;
                            }

                            affectedPatients.Add(patientId);
                        }
                        else
                        {
                            result.SkippedCount++;
                        }
                    }
                    catch (Exception lineEx)
                    {
                        result.SkippedCount++;
                        System.Diagnostics.Debug.WriteLine($"Error parsing line {i + 1}: {lineEx.Message}");
                    }
                }

                result.PatientsAffected = affectedPatients.Count;
                result.Success = true;
                result.Message = "Import completed successfully.";
            }
            catch (Exception ex)
            {
                result.Message = $"Error reading CSV file: {ex.Message}";
            }

            return result;
        }

        private bool ValidateCsvHeader(string header, string[] expectedColumns)
        {
            var headerColumns = header.Split(',');
            if (headerColumns.Length < 2) return false;

            // Check if we have the minimum required columns
            var requiredColumns = new[] { "PatientId", "Date" };
            foreach (var required in requiredColumns)
            {
                if (!headerColumns.Any(col => col.Trim().Equals(required, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            return true;
        }

        private ScoreEntry ParseCsvLine(string line, bool isHeader = false)
        {
            var values = ParseCsvValues(line);
            if (values.Length < 2) return null; // Need at least PatientId and Date

            if (isHeader) return null; // Don't parse header as data

            try
            {
                var entry = new ScoreEntry
                {
                    PatientId = values[0]?.Trim() ?? "",
                    Date = DateTime.Parse(values[1]),
                    PHQ9 = TryParseNullableInt(values.ElementAtOrDefault(2)),
                    GAD7 = TryParseNullableInt(values.ElementAtOrDefault(3)),
                    BDI2 = TryParseNullableInt(values.ElementAtOrDefault(4)),
                    PCL5 = TryParseNullableInt(values.ElementAtOrDefault(5)),
                    YBOCS = TryParseNullableInt(values.ElementAtOrDefault(6)),
                    Note = values.ElementAtOrDefault(7)?.Trim() ?? "",
                    CreatedBy = "CSV Import",
                    CreatedAt = DateTime.UtcNow
                };

                // Validate required fields
                if (string.IsNullOrWhiteSpace(entry.PatientId))
                    return null;

                return entry;
            }
            catch
            {
                return null;
            }
        }

        private string[] ParseCsvValues(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        current.Append('"');
                        i++; // Skip next quote
                    }
                    else
                    {
                        // Toggle quote state
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // End of field
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            // Add last field
            values.Add(current.ToString());

            return values.ToArray();
        }

        private int? TryParseNullableInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "-" || value == "—")
                return null;

            if (int.TryParse(value, out int result))
                return result;

            return null;
        }

        private void UpdateExistingEntry(ScoreEntry existing, ScoreEntry imported)
        {
            // Update scores only if imported value is not null
            if (imported.PHQ9.HasValue) existing.PHQ9 = imported.PHQ9;
            if (imported.GAD7.HasValue) existing.GAD7 = imported.GAD7;
            if (imported.BDI2.HasValue) existing.BDI2 = imported.BDI2;
            if (imported.PCL5.HasValue) existing.PCL5 = imported.PCL5;
            if (imported.YBOCS.HasValue) existing.YBOCS = imported.YBOCS;

            // Update note if provided
            if (!string.IsNullOrWhiteSpace(imported.Note))
                existing.Note = imported.Note;

            // Update audit fields
            existing.UpdatedBy = "CSV Import";
            existing.UpdatedAt = DateTime.UtcNow;
        }

        private void RefreshUIAfterImport()
        {
            // Refresh patient selector
            PatientSelector.Items.Clear();
            foreach (var patientId in patientData.Keys.OrderBy(x => x))
            {
                PatientSelector.Items.Add(patientId);
            }

            // Refresh current patient display if one is selected
            if (PatientSelector.SelectedItem is string selectedPatient)
            {
                UpdateChartForPatient(selectedPatient);
            }

            // Reset metrics if they were calculated
            if (currentMetrics != null)
            {
                ResetMetricsDisplay();
            }
        }

        private string ExportAllDataForBackup()
        {
            var sb = new StringBuilder();

            // Enhanced header with metadata
            sb.AppendLine($"# Reconnect Mental Health System Data Backup");
            sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"# System: Azure SQL Database");
            sb.AppendLine($"# Total Patients: {patientData.Keys.Count}");
            sb.AppendLine($"# Total Assessments: {patientData.Values.SelectMany(v => v).Count()}");
            sb.AppendLine($"# Data Range: {GetDataDateRange()}");
            sb.AppendLine("#");

            // CSV Header
            sb.AppendLine("PatientId,Date,PHQ9,GAD7,BDI2,PCL5,YBOCS,Note,CreatedBy,CreatedAt,UpdatedBy,UpdatedAt");

            // Export all patient data sorted by patient and date
            var allEntries = patientData.Values
                .SelectMany(v => v)
                .OrderBy(s => s.PatientId)
                .ThenBy(s => s.Date)
                .ToList();

            foreach (var entry in allEntries)
            {
                // Handle null values and escape CSV content
                var phq9Str = entry.PHQ9?.ToString() ?? "";
                var gad7Str = entry.GAD7?.ToString() ?? "";
                var bdi2Str = entry.BDI2?.ToString() ?? "";
                var pcl5Str = entry.PCL5?.ToString() ?? "";
                var ybocsStr = entry.YBOCS?.ToString() ?? "";
                var noteStr = EscapeCsvField(entry.Note ?? "");
                var createdBy = EscapeCsvField(entry.CreatedBy ?? "");
                var updatedBy = EscapeCsvField(entry.UpdatedBy ?? "");

                // Handle nullable DateTime properly
                var createdAt = entry.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

                var updatedAt = entry.UpdatedAt.HasValue ? entry.UpdatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "";

                sb.AppendLine($"{entry.PatientId},{entry.Date:yyyy-MM-dd},{phq9Str},{gad7Str},{bdi2Str},{pcl5Str},{ybocsStr},{noteStr},{createdBy},{createdAt},{updatedBy},{updatedAt}");
            }

            return sb.ToString();
        }

        // ─── : Edit/Delete Options ──────────────────────────────────────
        private async void ScoresGrid_MouseDoubleClick_1(object sender, MouseButtonEventArgs e)
        {
            if (ScoresGrid.SelectedItem is ScoreEntry selected)
            {
                // Check permissions first
                var canEdit = RoleHelper.CanEditData(currentUser);
                var canDelete = RoleHelper.CanDeleteData(currentUser);

                if (!canEdit && !canDelete)
                {
                    MessageBox.Show("You don't have permission to modify patient data.",
                                   "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Show edit/delete options
                string message = $"What would you like to do with this entry?\n\n" +
                                $"Patient: {selected.PatientId}\n" +
                                $"Date: {selected.Date:yyyy-MM-dd}\n" +
                                $"Scores: PHQ-9={selected.PHQ9}, GAD-7={selected.GAD7}, BDI-II={selected.BDI2}\n\n";

                MessageBoxButton buttons;
                if (canEdit && canDelete)
                {
                    message += "Click 'Yes' to EDIT or 'No' to DELETE";
                    buttons = MessageBoxButton.YesNoCancel;
                }
                else if (canEdit)
                {
                    message += "Click 'Yes' to EDIT";
                    buttons = MessageBoxButton.YesNo;
                }
                else if (canDelete)
                {
                    message += "Click 'Yes' to DELETE";
                    buttons = MessageBoxButton.YesNo;
                }
                else
                {
                    return;
                }

                var result = MessageBox.Show(message, "Edit or Delete Entry", buttons, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes && canEdit)
                {
                    // SIMPLE EDIT MODE: Just set the flags and load data
                    isInEditMode = true;
                    editingEntry = selected;

                    // Load into input fields
                    PatientIdBox.Text = selected.PatientId;
                    Phq9Box.Text = selected.PHQ9?.ToString() ?? "";
                    Gad7Box.Text = selected.GAD7?.ToString() ?? "";
                    Bdi2Box.Text = selected.BDI2?.ToString() ?? "";
                    PCL5Total.Text = selected.PCL5?.ToString() ?? "";
                    YBOCS.Text = selected.YBOCS?.ToString() ?? "";
                    NoteBox.Text = selected.Note ?? "";
                    DatePicker.SelectedDate = selected.Date;

                    MessageBox.Show("✅ Edit mode activated!\n\nMake your changes and click 'Add Score' to update.",
                                   "Edit Mode", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if ((result == MessageBoxResult.No && canEdit && canDelete) ||
                         (result == MessageBoxResult.Yes && canDelete && !canEdit))
                {
                    // DELETE
                    await DeleteSingleEntryFromDatabase(selected);
                }
            }
        }

        // NEW METHOD for single entry deletion:
        private async Task DeleteSingleEntryFromDatabase(ScoreEntry selected)
        {
            // CHECK IF THIS IS THE PATIENT'S ONLY ENTRY
            var patientEntryCount = 0;
            if (patientData.ContainsKey(selected.PatientId))
            {
                patientEntryCount = patientData[selected.PatientId].Count;
            }

            string warningMessage;
            if (patientEntryCount <= 1)
            {
                warningMessage = $"⚠️ WARNING: This is the ONLY entry for this patient!\n\n" +
                                $"Deleting this entry will remove the patient completely from the system.\n\n" +
                                $"Patient: {selected.PatientId}\n" +
                                $"Date: {selected.Date:yyyy-MM-dd}\n" +
                                $"Scores: PHQ-9={selected.PHQ9}, GAD-7={selected.GAD7}, BDI-II={selected.BDI2}\n\n" +
                                $"Are you sure you want to permanently delete this patient's only entry?";
            }
            else
            {
                warningMessage = $"Are you sure you want to permanently delete this entry?\n\n" +
                                $"Patient: {selected.PatientId}\n" +
                                $"Date: {selected.Date:yyyy-MM-dd}\n" +
                                $"Scores: PHQ-9={selected.PHQ9}, GAD-7={selected.GAD7}, BDI-II={selected.BDI2}\n\n" +
                                $"The patient will still have {patientEntryCount - 1} other entries remaining.";
            }

            var confirmDelete = MessageBox.Show(warningMessage, "Confirm Delete",
                                               MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirmDelete == MessageBoxResult.Yes)
            {
                try
                {
                    // DELETE FROM DATABASE
                    using var context = new AppDbContext();
                    var dbEntry = await context.ScoreEntries
                        .FirstOrDefaultAsync(e => e.PatientId == selected.PatientId &&
                                           e.Date.Date == selected.Date.Date);

                    if (dbEntry != null)
                    {
                        context.ScoreEntries.Remove(dbEntry);
                        await context.SaveChangesAsync();

                        // Log the deletion
                        await _auditService.LogActionAsync("DELETE_ENTRY", selected.PatientId,
                            $"Deleted single entry for {selected.PatientId} on {selected.Date:yyyy-MM-dd}");

                        // COMPREHENSIVE UI REFRESH
                        if (patientEntryCount <= 1)
                        {
                            // Patient has no more entries - remove completely
                            patientData.Remove(selected.PatientId);
                            PatientSelector.Items.Remove(selected.PatientId);
                            PatientSelector.SelectedItem = null;
                            ClearPatientDisplay();

                            // Clear the patient ID box since patient is gone
                            PatientIdBox.Clear();
                        }
                        else
                        {
                            // Patient still has other entries - refresh patient data
                            await LoadPatientDataFromDatabase(selected.PatientId);
                            UpdateChartForPatient(selected.PatientId);
                        }

                        // REFRESH THE FILTER GRID TOO
                        if (!string.IsNullOrEmpty(filterId))
                        {
                            // Trigger filter refresh
                            FilterBox_TextChanged(null, null);
                        }
                        else
                        {
                            // Refresh all data view
                            await LoadAllPatientsFromDatabase();
                            using var context2 = new AppDbContext();
                            var allEntries = await context2.ScoreEntries
                                .OrderBy(r => r.PatientId)
                                .ThenBy(r => r.Date)
                                .ToListAsync();
                            ScoresGrid.ItemsSource = allEntries;
                        }

                        string deleteMessage = "✅ Entry deleted successfully from database!";
                        if (patientEntryCount <= 1)
                        {
                            deleteMessage += "\n\n🗑️ Patient removed completely (no remaining entries).";
                        }

                        MessageBox.Show(deleteMessage, "Deleted",
                                       MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Entry not found in database.", "Not Found",
                                       MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Error deleting entry from database: {ex.Message}", "Database Error",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ScoresGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }

    public class ImportResult
    {
        public bool Success { get; set; } = false;
        public string Message { get; set; } = "";
        public int ImportedCount { get; set; } = 0;
        public int UpdatedCount { get; set; } = 0;
        public int SkippedCount { get; set; } = 0;
        public int PatientsAffected { get; set; } = 0;
    }
}