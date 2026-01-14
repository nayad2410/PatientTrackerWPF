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
using PdfSharp.Drawing;
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
using PdfSharp.Pdf;



using XGraphics = PdfSharp.Drawing.XGraphics; // Explicitly alias the desired `XGraphics` type.


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
using Microsoft.Extensions.DependencyInjection;

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
        private readonly string _connectionString;

        // Add this INSIDE your MainWindow class, after your existing fields:

        #region Modern Theme Colors and Helpers
        private static class ModernTheme
        {
            // Modern colors (WCAG-aware, print-safe)
            public static readonly DrawingColor Ink = DrawingColor.FromArgb(38, 38, 38);
            public static readonly DrawingColor Muted = DrawingColor.FromArgb(100, 116, 139);
            public static readonly DrawingColor Line = DrawingColor.FromArgb(228, 232, 240);
            public static readonly DrawingColor Surface = DrawingColor.FromArgb(249, 250, 251);
            public static readonly DrawingColor Card = DrawingColor.FromArgb(253, 254, 255);
            public static readonly DrawingColor Brand = DrawingColor.FromArgb(14, 116, 144);
            public static readonly DrawingColor Success = DrawingColor.FromArgb(16, 131, 86);
            public static readonly DrawingColor Danger = DrawingColor.FromArgb(200, 30, 30);
            public static readonly DrawingColor RemissionBand = DrawingColor.FromArgb(205, 237, 216);

            // Modern chart colors
            public static readonly DrawingColor ChartBlue = DrawingColor.FromArgb(59, 130, 246);
            public static readonly DrawingColor ChartGreen = DrawingColor.FromArgb(34, 197, 94);
            public static readonly DrawingColor ChartOrange = DrawingColor.FromArgb(249, 115, 22);
            public static readonly DrawingColor ChartCyan = DrawingColor.FromArgb(6, 182, 212);
            public static readonly DrawingColor ChartPurple = DrawingColor.FromArgb(147, 51, 234);

            // Font helpers
            public static DrawingFont GetModernFont(int size, bool bold = false)
            {
                try
                {
                    return new DrawingFont("Segoe UI", size, bold ? DrawingFontStyle.Bold : DrawingFontStyle.Regular);
                }
                catch
                {
                    return new DrawingFont("Arial", size, bold ? DrawingFontStyle.Bold : DrawingFontStyle.Regular);
                }
            }

            // Draw modern card background
            public static void DrawCard(DrawingGraphics g, DrawingRectangle rect)
            {
                using var cardBg = new SolidBrush(Card);
                using var cardBorder = new System.Drawing.Pen(Line, 1);

                g.FillRectangle(cardBg, rect);
                g.DrawRectangle(cardBorder, rect);
            }

            // Draw section title with underline
            public static int DrawSectionTitle(DrawingGraphics g, string title, int x, int y, DrawingFont font)
            {
                using var titleBrush = new SolidBrush(Brand);
                using var linePen = new System.Drawing.Pen(Line, 1);

                g.DrawString(title, font, titleBrush, x, y);
                var titleHeight = (int)g.MeasureString(title, font).Height;
                g.DrawLine(linePen, x, y + titleHeight + 6, x + 600, y + titleHeight + 6);

                return y + titleHeight + 20;
            }
        }
        #endregion
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

            // Show a friendly loading message immediately
            ShowDatabaseStatus("Connecting to patient database...");

            // Warm up database in background
            _ = Task.Run(async () =>
            {
                await WarmUpDatabase();
                await Dispatcher.InvokeAsync(async () =>
                {
                    HideDatabaseStatus();
                    await LoadAllPatientsFromDatabase();
                });
            });
        }
        // Always load from database
        //_ = Task.Run(async () =>
        //{
        //    await Dispatcher.InvokeAsync(async () =>
        //    {
        //        await LoadAllPatientsFromDatabase();
        //    });
        //});
/*        private string GetConnectionString()
        {

            return "Server=tcp:reconnect-mental-health.database.windows.net,1433;" +
                "Initial Catalog=ReconnectMentalHealth-db;" +
                "Persist Security Info=False;" +
                "User ID=reconnect-admin;Password={MH2025Project};" +
                "MultipleActiveResultSets=False;Encrypt=True;" +
                "TrustServerCertificate=False;" +
                "Connection Timeout=30";
        }
*/

        private void ShowDatabaseStatus(string message)
        {
            // Add a small status bar or overlay
            DatabaseStatusText.Text = message;
            DatabaseStatusPanel.Visibility = Visibility.Visible;
        }

        private void HideDatabaseStatus()
        {
            DatabaseStatusPanel.Visibility = Visibility.Collapsed;
        }

        private async Task WarmUpDatabase(int retryCount = 3)
        {
            try
            {
                using var scope = App.GetService<IServiceScopeFactory>().CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                ShowDatabaseStatus($"DB connect failed: {ex.Message}");
            }
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


        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            var authService = App.GetService<AuthenticationService>();
            var changePasswordWindow = new ChangePasswordWindow(authService);
            changePasswordWindow.Owner = this;

            if (changePasswordWindow.ShowDialog() == true)
            {
                MessageBox.Show("Password changed successfully!", "Success",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
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

            // Validate score ranges (keep your existing validation)
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

            try
            {
                System.Diagnostics.Debug.WriteLine("DATABASE OPERATION START");

                if (isInEditMode && editingEntry != null)
                {
                    // ✅ EDIT MODE - Use fresh context
                    var context = _dbContext;

                    var existingEntry = await context.ScoreEntries
                        .FirstOrDefaultAsync(e => e.Id == editingEntry.Id);

                    if (existingEntry != null)
                    {
                        existingEntry.PHQ9 = TryParseOrNull(Phq9Box.Text);
                        existingEntry.GAD7 = TryParseOrNull(Gad7Box.Text);
                        existingEntry.BDI2 = TryParseOrNull(Bdi2Box.Text);
                        existingEntry.PCL5 = TryParseOrNull(PCL5Total.Text);
                        existingEntry.YBOCS = TryParseOrNull(YBOCS.Text);
                        existingEntry.Note = NoteBox.Text.Trim();

                        await context.SaveChangesAsync();
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
                    // ✅ NORMAL MODE - Use fresh context
                    using var context = new AppDbContext();

                    var existingEntry = await context.ScoreEntries
                        .FirstOrDefaultAsync(e => e.PatientId == id && e.Date.Date == selectedDate.Date);

                    if (existingEntry != null)
                    {
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
                            return;

                        existingEntry.PHQ9 = TryParseOrNull(Phq9Box.Text);
                        existingEntry.GAD7 = TryParseOrNull(Gad7Box.Text);
                        existingEntry.BDI2 = TryParseOrNull(Bdi2Box.Text);
                        existingEntry.PCL5 = TryParseOrNull(PCL5Total.Text);
                        existingEntry.YBOCS = TryParseOrNull(YBOCS.Text);
                        existingEntry.Note = NoteBox.Text.Trim();
                    }
                    else
                    {
                        // Create new entry
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

                        context.ScoreEntries.Add(newEntry);
                    }

                    var changeCount = await context.SaveChangesAsync();

                    // Log the action
                    string action = isInEditMode ? "UPDATE_SCORE" : "CREATE_SCORE";
                    await _auditService.LogActionAsync(action, id, $"Saved scores for patient {id}");

                    if (changeCount > 0)
                    {
                        lastDataModification = DateTime.Now;
                    }
                }

                // Reset edit mode if needed
                if (isInEditMode)
                {
                    ResetEditMode();
                }

                // ✅ Refresh UI - this reloads all patients from database
                await LoadAllPatientsFromDatabase();

                // ✅ Clear current selection first to ensure SelectionChanged event fires properly
                _handlingSelection = true;
                PatientSelector.SelectedItem = null;
                _handlingSelection = false;

                // ✅ Set the selection to the new/updated patient
                PatientSelector.SelectedItem = id;

                // ✅ Manually load and display the patient's data
                await LoadPatientDataFromDatabase(id);
                UpdateChartForPatient(id);

                // ✅ Recalculate metrics for this patient
                await RecalculateAndRefreshAsync(id);

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
                System.Diagnostics.Debug.WriteLine($"ERROR in AddScore_Click: {ex.Message}");

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
                System.Diagnostics.Debug.WriteLine("🔍 Starting LoadAllPatientsFromDatabase...");

                // ✅ Use fresh context
                using var context = new AppDbContext();

                var allEntries = await context.ScoreEntries
                    .AsNoTracking()
                    .OrderBy(e => e.PatientId)
                    .ThenBy(e => e.Date)
                    .ToListAsync();

                System.Diagnostics.Debug.WriteLine($"🔍 Found {allEntries.Count} total entries in database");

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

                ScoresGrid.ItemsSource = allEntries;

                System.Diagnostics.Debug.WriteLine($"✅ Loaded {patientData.Keys.Count} patients from database");

                if (!allEntries.Any())
                {
                    MessageBox.Show("⚠️ No data found in database.\nCheck connection string and ensure data exists.",
                                   "No Data Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error loading patients: {ex.Message}");
                MessageBox.Show($"Database error: {ex.Message}", "Database Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
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




        private List<int> GetAllScores(List<ScoreEntry> entries)
        {
            var allScores = new List<int>();
            foreach (var e in entries)
            {
                if (e.PHQ9.HasValue) allScores.Add(e.PHQ9.Value);
                if (e.GAD7.HasValue) allScores.Add(e.GAD7.Value);
                if (e.BDI2.HasValue) allScores.Add(e.BDI2.Value);
                if (e.PCL5.HasValue) allScores.Add(e.PCL5.Value);
                if (e.YBOCS.HasValue) allScores.Add(e.YBOCS.Value);
            }
            return allScores;
        }





 


        [SupportedOSPlatform("windows")]
        private void CreateTwoPageClinicalReport(string patientId, List<ScoreEntry> entries, string outputPath, bool generatePdf = true)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(patientId))
                throw new ArgumentException("Patient ID cannot be empty", nameof(patientId));

            if (entries == null || !entries.Any())
                throw new ArgumentException("No entries provided", nameof(entries));

            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path cannot be empty", nameof(outputPath));

            try
            {
                // Sort entries once at the top
                entries = entries.OrderBy(e => e.Date).ToList();

                // Single page dimensions: 8.5" x 11" at 300 DPI
                const int pageWidth = 2550;
                const int pageHeight = 3300;
                const int margin = 150; // 0.5" margin at 300 DPI

                // Guard for null directory
                var directory = Path.GetDirectoryName(outputPath);
                if (string.IsNullOrEmpty(directory))
                {
                    directory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    outputPath = Path.Combine(directory, Path.GetFileName(outputPath));
                }

                // Ensure directory exists
                Directory.CreateDirectory(directory);

                if (generatePdf)
                {
                    CreateTwoPagePdf(patientId, entries, outputPath, pageWidth, pageHeight, margin);
                }
                else
                {
                    CreateTwoPagePngs(patientId, entries, outputPath, pageWidth, pageHeight, margin);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to generate report for patient {patientId}: {ex.Message}", ex);
            }
        }

        [SupportedOSPlatform("windows")]
        private void CreateTwoPagePdf(string patientId, List<ScoreEntry> entries, string outputPath,
            int pageWidth, int pageHeight, int margin)
        {
            // Create both page bitmaps in memory
            var page1Bitmap = CreatePageBitmap(pageWidth, pageHeight, (g) =>
                DrawPage1Content(g, patientId, entries, pageWidth, pageHeight, margin));

            var page2Bitmap = CreatePageBitmap(pageWidth, pageHeight, (g) =>
                DrawPage2Content(g, patientId, entries, pageWidth, pageHeight, margin));

            // Convert to PDF
            var pdfPath = Path.ChangeExtension(outputPath, ".pdf");
            SaveBitmapsAsPdf(page1Bitmap, page2Bitmap, pdfPath); // ✅ Removed extra parameter

            page1Bitmap.Dispose();
            page2Bitmap.Dispose();

            MessageBox.Show($"Two-page PDF report generated successfully!\n\nFile: {pdfPath}",
                           "PDF Report Generated", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [SupportedOSPlatform("windows")]
        private void CreateTwoPagePngs(string patientId, List<ScoreEntry> entries, string outputPath,
            int pageWidth, int pageHeight, int margin)
        {
            // Create Page 1
            var page1Bitmap = CreatePageBitmap(pageWidth, pageHeight, (g) =>
                DrawPage1Content(g, patientId, entries, pageWidth, pageHeight, margin));

            // Create Page 2
            var page2Bitmap = CreatePageBitmap(pageWidth, pageHeight, (g) =>
                DrawPage2Content(g, patientId, entries, pageWidth, pageHeight, margin));

            // Save both pages
            var basePath = Path.GetFileNameWithoutExtension(outputPath);
            var directory = Path.GetDirectoryName(outputPath);

            var page1Path = Path.Combine(directory, $"{basePath}_Page1.png");
            var page2Path = Path.Combine(directory, $"{basePath}_Page2.png");

            page1Bitmap.Save(page1Path, ImageFormat.Png);
            page2Bitmap.Save(page2Path, ImageFormat.Png);

            page1Bitmap.Dispose();
            page2Bitmap.Dispose();

            MessageBox.Show($"Two-page PNG report generated successfully!\n\nPage 1: {page1Path}\nPage 2: {page2Path}",
                           "PNG Report Generated", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [SupportedOSPlatform("windows")]
        private DrawingBitmap CreatePageBitmap(int pageWidth, int pageHeight, Action<DrawingGraphics> drawAction)
        {
            var bitmap = new DrawingBitmap(pageWidth, pageHeight);
            bitmap.SetResolution(300, 300); // ✅ Set proper print resolution

            using (var g = DrawingGraphics.FromImage(bitmap))
            {
                SetupGraphicsQuality(g);
                drawAction(g);
            }

            return bitmap;
        }

        private void SaveBitmapsAsPdf(DrawingBitmap page1, DrawingBitmap page2, string pdfPath)
        {
            var doc = new PdfDocument();
            doc.Info.Title = $"Clinical Report - {PatientSelector.Text}";
            doc.Info.Creator = "Reconnect Mental Health System";
            doc.Info.Subject = "Clinical Progress Report";

            var bitmaps = new[] { page1, page2 };

            for (int i = 0; i < bitmaps.Length; i++)
            {
                var bitmap = bitmaps[i];
                var page = doc.AddPage();
                page.Size = PdfSharp.PageSize.Letter; // 8.5" x 11"

                using var xg = XGraphics.FromPdfPage(page);

                // Convert bitmap to memory stream first
                using var memoryStream = new MemoryStream();
                bitmap.Save(memoryStream, ImageFormat.Png);
                memoryStream.Position = 0;

                // Create XImage from memory stream
                using var xi = XImage.FromStream(memoryStream);
                const double marginPt = 36; // 0.5" * 72 pt/in
                xg.DrawImage(xi, marginPt, marginPt, page.Width - 2*marginPt, page.Height - 2*marginPt);

            }

            doc.Save(pdfPath);
            doc.Close();
        }


        [SupportedOSPlatform("windows")]
        private int DrawPage1Content(DrawingGraphics g, string patientId, List<ScoreEntry> entries,
            int pageWidth, int pageHeight, int margin, int yOffset = 0)
        {
            const int FOOTER_RESERVED = 90;
            const int  SECTION_GAP = 36;
            var usableTop = margin + yOffset;
            var usableBottom = pageHeight - margin - FOOTER_RESERVED + yOffset;

            // Use modern theme
            using var surfaceBrush = new SolidBrush(ModernTheme.Surface);
            g.FillRectangle(surfaceBrush, 0, 0, pageWidth, pageHeight);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Modern fonts
            using var titleFont = ModernTheme.GetModernFont(16, true);
            using var bodyFont = ModernTheme.GetModernFont(11);
            using var subHeaderFont = ModernTheme.GetModernFont(12, true);
            using var chartFont = ModernTheme.GetModernFont(10);

            int currentY = usableTop;

            // Header with single-line layout
            currentY = DrawPage1Header_Clean(g, patientId, entries, margin, currentY, pageWidth, titleFont, bodyFont);
            currentY += 48; // More space after header

            if (currentY >= usableBottom) goto Footer;



            // Chart (enlarged and with better spacing)

            int remaining = Math.Max(300, usableBottom - currentY);
            int chartHeight = Math.Min(1100, Math.Max(520, (int)(remaining * 0.72)));  // ⬅ bigger
            int chartWidth = pageWidth - (margin * 2);

            currentY = DrawDateBasedChart_Modern(g, entries, margin, currentY, chartWidth, chartHeight, chartFont);
            currentY += SECTION_GAP;

       
            if (currentY >= usableBottom) goto Footer;

            // Summary panel with proper layout
            currentY = DrawSummaryPanel_Clean(g, entries, margin, currentY, pageWidth, subHeaderFont, bodyFont);

        Footer:
            DrawPageFooter(g, patientId, 1, pageWidth, pageHeight, margin);
            return Math.Min(currentY, usableBottom);
        }


        private int DrawDateBasedChart_Modern(
     DrawingGraphics g, List<ScoreEntry> entries,
     int x, int y, int chartWidth, int chartHeight, DrawingFont chartFont)
        {
            if (!entries.Any())
            {
                using var textBrush = new SolidBrush(ModernTheme.Ink);
                g.DrawString("No assessment data available for chart.", chartFont, textBrush, x, y);
                return y + 50;
            }

            // FIXED: Better spacing calculations
            const int LEGEND_H = 68;
            const int LEGEND_PAD = 35;           // ⬅ INCREASED from 14
            const int XAXIS_LABEL_HEIGHT = 45;   // ⬅ FIXED: Reserve proper space for x-axis labels
            const int LEGEND_SHIFT_RIGHT = 36;
            const int PLOT_L = 72, PLOT_R = 48, PLOT_T = 42, PLOT_B = 75; // ⬅ INCREASED bottom padding

            // FIXED: Calculate plot height accounting for x-axis labels AND legend
            var plotH = Math.Max(150, chartHeight - (LEGEND_H + LEGEND_PAD + XAXIS_LABEL_HEIGHT + PLOT_T + PLOT_B));
            var chartArea = new DrawingRectangle(
                x + PLOT_L,
                y + PLOT_T,
                Math.Max(200, chartWidth - (PLOT_L + PLOT_R)),
                plotH
            );

            // Range calc
            var minDate = entries.First().Date;
            var maxDate = entries.Last().Date;
            if (minDate == maxDate) { minDate = minDate.AddDays(-3); maxDate = maxDate.AddDays(+3); }

            var allScores = GetAllScores(entries);
            if (!allScores.Any()) return y + 50;

            double minScore = Math.Max(0, allScores.Min() - 5);
            double maxScore = Math.Min(80, allScores.Max() + 10);
            if (Math.Abs(maxScore - minScore) < 0.0001) maxScore = minScore + 1;

            // Background + border
            using var chartBg = new SolidBrush(ModernTheme.Card);
            using var border = new System.Drawing.Pen(ModernTheme.Line, 1);
            g.FillRectangle(chartBg, chartArea);
            g.DrawRectangle(border, chartArea);

            DrawRemissionBand(g, chartArea, minScore, maxScore, entries);
            DrawGrid_Clean(g, chartArea, minScore, maxScore, entries, chartFont);
            DrawDataSeries_DateScaled(g, entries, chartArea, minScore, maxScore, minDate, maxDate);


            // FIXED: Legend positioned with proper spacing from x-axis labels
            var legendRect = DrawLegendBelowChart(
                g, chartArea, chartFont, LEGEND_H,
                XAXIS_LABEL_HEIGHT + LEGEND_PAD,  // ⬅ FIXED: Proper gap calculation
                LEGEND_SHIFT_RIGHT);

            return legendRect.Bottom + 18;
        }

        private void DrawDataSeries_DateScaled(
    DrawingGraphics g, List<ScoreEntry> entries,
    DrawingRectangle chartArea, double minScore, double maxScore,
    DateTime minDate, DateTime maxDate)
        {
            var series = new[]
            {
        (sel: (Func<ScoreEntry,int?>)(e => e.PHQ9),  col: ModernTheme.ChartBlue),
        (sel: (Func<ScoreEntry,int?>)(e => e.GAD7),  col: ModernTheme.ChartGreen),
        (sel: (Func<ScoreEntry,int?>)(e => e.BDI2),  col: ModernTheme.ChartOrange),
        (sel: (Func<ScoreEntry,int?>)(e => e.PCL5),  col: ModernTheme.ChartCyan),
        (sel: (Func<ScoreEntry,int?>)(e => e.YBOCS), col: ModernTheme.ChartPurple),
    };

            double scoreRange = Math.Max(1e-6, maxScore - minScore);
            double dateRange = Math.Max(1e-6, (maxDate - minDate).TotalDays);

            foreach (var s in series)
            {
                var pts = new List<(DrawingPointF pt, int val)>();

                foreach (var entry in entries)
                {
                    var v = s.sel(entry);
                    if (!v.HasValue) continue;

                    double dx = (entry.Date - minDate).TotalDays / dateRange; // 0..1
                    float x = chartArea.X + (float)(dx * chartArea.Width);

                    double ny = (v.Value - minScore) / scoreRange;           // 0..1
                    float y = chartArea.Bottom - (float)(ny * chartArea.Height);

                    pts.Add((new DrawingPointF(x, y), v.Value));
                }

                if (pts.Count == 0) continue;

                using var pen = new System.Drawing.Pen(s.col, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                if (pts.Count > 1) g.DrawLines(pen, pts.Select(p => p.pt).ToArray());

                using var fill = new SolidBrush(s.col);
                using var white = new System.Drawing.Pen(DrawingColor.White, 2);
                using var fnt = new DrawingFont("Segoe UI", 8, DrawingFontStyle.Bold);
                using var txt = new SolidBrush(DrawingColor.FromArgb(50, 50, 50));

                foreach (var p in pts)
                {
                    g.FillEllipse(fill, p.pt.X - 6, p.pt.Y - 6, 12, 12);
                    g.DrawEllipse(white, p.pt.X - 6, p.pt.Y - 6, 12, 12);
                    var sVal = p.val.ToString();
                    var sz = g.MeasureString(sVal, fnt);
                    g.DrawString(sVal, fnt, txt, p.pt.X - sz.Width/2, p.pt.Y - 20);
                }
            }
        }

        private DrawingRectangle DrawLegendBelowChart(
    DrawingGraphics g,
    DrawingRectangle chartArea,
    DrawingFont chartFont,
    int legendHeight,
    int topOffset,         // was: legendPad
    int shiftRight = 0     // new: pixels to nudge right
)
        {
            int lx = chartArea.X + shiftRight;              // ⬅ shove to the right
            int ly = chartArea.Bottom + topOffset;          // ⬅ sits below x-axis labels
            int lw = chartArea.Width - shiftRight;          // keep inside page width
            int lh = legendHeight;

            using var legendBg = new SolidBrush(System.Drawing.Color.FromArgb(248, 250, 252));
            using var legendBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 224, 230), 1);
            using var textBrush = new SolidBrush(ModernTheme.Ink);
            using var legFont = new DrawingFont("Segoe UI", 9, DrawingFontStyle.Regular);

            g.FillRectangle(legendBg, lx, ly, lw, lh);
            g.DrawRectangle(legendBorder, lx, ly, lw, lh);

            var items = new[]
            {
        ("PHQ-9",  ModernTheme.ChartBlue),
        ("GAD-7",  ModernTheme.ChartGreen),
        ("BDI-II", ModernTheme.ChartOrange),
        ("PCL-5",  ModernTheme.ChartCyan),
        ("Y-BOCS", ModernTheme.ChartPurple)
    };

            int itemW = lw / items.Length;
            int cy = ly + (lh / 2) - 6;

            for (int i = 0; i < items.Length; i++)
            {
                int cx = lx + i * itemW + 14;
                using var p = new System.Drawing.Pen(items[i].Item2, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(p, cx, cy, cx + 22, cy);
                g.DrawString(items[i].Item1, legFont, textBrush, cx + 28, cy - 8);
            }

            return new DrawingRectangle(lx, ly, lw, lh);
        }


        private DrawingRectangle DrawLegendBelowChart(
    DrawingGraphics g, DrawingRectangle chartArea, DrawingFont chartFont,
    int legendHeight, int legendPad)
        {
            int lx = chartArea.X;
            int ly = chartArea.Bottom + legendPad;
            int lw = chartArea.Width;
            int lh = legendHeight;

            using var legendBg = new SolidBrush(System.Drawing.Color.FromArgb(248, 250, 252));
            using var legendBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 224, 230), 1);
            using var textBrush = new SolidBrush(ModernTheme.Ink);

            // background box
            g.FillRectangle(legendBg, lx, ly, lw, lh);
            g.DrawRectangle(legendBorder, lx, ly, lw, lh);

            var items = new[]
            {
        ("PHQ-9",  ModernTheme.ChartBlue),
        ("GAD-7",  ModernTheme.ChartGreen),
        ("BDI-II", ModernTheme.ChartOrange),
        ("PCL-5",  ModernTheme.ChartCyan),
        ("Y-BOCS", ModernTheme.ChartPurple)
    };

            int itemW = lw / items.Length;
            int cy = ly + (lh / 2) - 6; // center lines vertically

            using var legFont = new DrawingFont("Segoe UI", 9, DrawingFontStyle.Regular);

            for (int i = 0; i < items.Length; i++)
            {
                int cx = lx + i * itemW + 14;
                using var p = new System.Drawing.Pen(items[i].Item2, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(p, cx, cy, cx + 22, cy);
                g.DrawString(items[i].Item1, legFont, textBrush, cx + 28, cy - 8);
            }

            return new DrawingRectangle(lx, ly, lw, lh);
        }


        // CLEAN: Page 1 Header
        private int DrawPage1Header_Clean(
      DrawingGraphics g, string patientId, List<ScoreEntry> entries,
      int margin, int currentY, int pageWidth, DrawingFont titleFont, DrawingFont bodyFont)
        {
            const int LOGO_NUDGE_UP = 12;
            int headerHeight = 148;
            using var headerBg = new SolidBrush(DrawingColor.FromArgb(234, 244, 250));
            using var brandBrush = new SolidBrush(DrawingColor.FromArgb(43, 95, 117));
            using var textBrush = new SolidBrush(DrawingColor.FromArgb(128, 128, 128));

            g.FillRectangle(headerBg, 0, currentY, pageWidth, headerHeight);

            int yHeaderLine = currentY + 20;

            // Logo / title
            int logoTargetH = 140;
            if (TryLoadLogoBitmap(out var logoBmp))
            {
                using (logoBmp)
                    DrawLogo(g, logoBmp, margin, yHeaderLine - LOGO_NUDGE_UP, logoTargetH);
            }
            else
            {
                g.DrawString("RECONNECT", titleFont, brandBrush, margin, yHeaderLine);
            }

            var summaryText = "Clinical Progress Summary";
            var summarySize = g.MeasureString(summaryText, titleFont);
            var summaryX = pageWidth - margin - summarySize.Width;
            g.DrawString(summaryText, titleFont, brandBrush, summaryX, yHeaderLine);

            // --- Put Patient ID BELOW the header band ---
            const int GAP_BELOW_HEADER = 10;                        // ⬅ tweak this for more/less space
            int yInfoLine = currentY + headerHeight + GAP_BELOW_HEADER;

            var dateRange = entries.Any()
                ? $"{entries.First().Date:yyyy-MM-dd} to {entries.Last().Date:yyyy-MM-dd}"
                : "No data";
            var infoText = $"Patient ID: {patientId}  |  Period: {dateRange}  |  Total Assessments: {entries.Count}";
            g.DrawString(infoText, bodyFont, textBrush, margin, yInfoLine);

            // Advance past the info line so the chart doesn't overlap
            int infoH = (int)g.MeasureString(infoText, bodyFont).Height;
            int blockBottom = yInfoLine + infoH + 6;                // small padding after info

            return blockBottom;
        }


        private static bool TryLoadLogoBitmap(out DrawingBitmap logo)
        {
            logo = null;
            try
            {
                // Use the same pack URI that works in your XAML
                var uri = new Uri("pack://application:,,,/Images/Logo.png");
                var sri = System.Windows.Application.GetResourceStream(uri);
                if (sri != null)
                {
                    using var ms = new MemoryStream();
                    sri.Stream.CopyTo(ms);
                    ms.Position = 0;
                    logo = new DrawingBitmap(ms);
                    logo.SetResolution(300, 300); // High DPI for PDF
                    return true;
                }
            }
            catch
            {
                // If resource loading fails, fall back to text
            }
            return false;
        }
        private static void DrawLogo(DrawingGraphics g, DrawingBitmap bmp, int x, int y, int targetHeight)
        {
            float scale = targetHeight / (float)bmp.Height;
            int w = (int)Math.Round(bmp.Width * scale);
            g.DrawImage(bmp, new DrawingRectangle(x, y, w, targetHeight));
        }

        // CLEAN: Chart with proper X-axis and score labels
        private int DrawChart_Clean(
     DrawingGraphics g, List<ScoreEntry> entries,
     int x, int y, int chartWidth, int chartHeight, DrawingFont chartFont)
        {
            if (!entries.Any())
            {
                using var textBrush = new SolidBrush(ModernTheme.Ink);
                g.DrawString("No assessment data available for chart.", chartFont, textBrush, x, y);
                return y + 50;
            }

            var plotMargin = 80;
            var chartArea = new DrawingRectangle(
                x + plotMargin, y + 40,
                Math.Max(200, chartWidth - (plotMargin * 2)),
                Math.Max(200, chartHeight - 100)); // More space for bottom labels

            var minDate = entries.First().Date;
            var maxDate = entries.Last().Date;
            if (minDate == maxDate)
            {
                minDate = minDate.AddDays(-1);
                maxDate = maxDate.AddDays(1);
            }

            var allScores = GetAllScores(entries);
            if (!allScores.Any()) return y + 50;

            double minScore = Math.Max(0, allScores.Min() - 5);
            double maxScore = Math.Min(80, allScores.Max() + 10);
            if (Math.Abs(maxScore - minScore) < 0.0001) maxScore = minScore + 1;

            // Chart background
            using var chartBg = new SolidBrush(ModernTheme.Card);
            using var borderPen = new System.Drawing.Pen(ModernTheme.Line, 1);
            g.FillRectangle(chartBg, chartArea);
            g.DrawRectangle(borderPen, chartArea);

            // Draw remission band
            DrawRemissionBand(g, chartArea, minScore, maxScore, entries);

            // Draw grid and axes
            DrawGrid_Clean(g, chartArea, minScore, maxScore, entries, chartFont);

            // Draw data series with score labels
            DrawDataSeries_Clean(g, entries, chartArea, minScore, maxScore);

            return chartArea.Bottom + 60; // Space for X-axis labels
        }



        // CLEAN: Grid with properly spaced X-axis
        private void DrawGrid_Clean(
      DrawingGraphics g, DrawingRectangle chartArea, double minScore, double maxScore,
      List<ScoreEntry> entries, DrawingFont chartFont)
        {
            using var gridPen = new System.Drawing.Pen(DrawingColor.FromArgb(235, 238, 243), 1);
            using var textBrush = new SolidBrush(ModernTheme.Muted);

            double scoreRange = maxScore - minScore;

            // Y grid (vertical lines for scores)
            int ySteps = Math.Max(3, Math.Min(6, (int)Math.Ceiling(scoreRange / 10.0)));
            for (int i = 0; i <= ySteps; i++)
            {
                float gy = (float)(chartArea.Y + (i * chartArea.Height / (double)ySteps));
                g.DrawLine(gridPen, chartArea.X, gy, chartArea.Right, gy);
                var val = maxScore - (i * scoreRange / ySteps);
                g.DrawString(((int)Math.Round(val)).ToString(), chartFont, textBrush, chartArea.X - 35, gy - 8);
            }

            // FIXED: X grid with proper date label positioning
            var assessmentDates = entries
                .Select(e => e.Date.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            if (assessmentDates.Count > 0)
            {
                for (int i = 0; i < assessmentDates.Count; i++)
                {
                    float gx = chartArea.X + (i * chartArea.Width / Math.Max(1, assessmentDates.Count - 1));

                    if (assessmentDates.Count == 1)
                        gx = chartArea.X + chartArea.Width / 2f;

                    g.DrawLine(gridPen, gx, chartArea.Y, gx, chartArea.Bottom);

                    // FIXED: Better positioned date labels with more space
                    var dateLabel = assessmentDates[i].ToString("MM/dd");
                    var labelSize = g.MeasureString(dateLabel, chartFont);
                    float labelY = chartArea.Bottom + 16; // ⬅ INCREASED spacing from chart
                    g.DrawString(dateLabel, chartFont, textBrush, gx - labelSize.Width/2, labelY);
                }
            }
        }

        // CLEAN: Data series with score labels on ALL points
        private void DrawDataSeries_Clean(
     DrawingGraphics g, List<ScoreEntry> entries,
     DrawingRectangle chartArea, double minScore, double maxScore)
        {
            var seriesConfigs = new[]
            {
        (selector: (Func<ScoreEntry, int?>)(e => e.PHQ9), color: ModernTheme.ChartBlue, name: "PHQ-9"),
        (selector: (Func<ScoreEntry, int?>)(e => e.GAD7), color: ModernTheme.ChartGreen, name: "GAD-7"),
        (selector: (Func<ScoreEntry, int?>)(e => e.BDI2), color: ModernTheme.ChartOrange, name: "BDI-II"),
        (selector: (Func<ScoreEntry, int?>)(e => e.PCL5), color: ModernTheme.ChartCyan, name: "PCL-5"),
        (selector: (Func<ScoreEntry, int?>)(e => e.YBOCS), color: ModernTheme.ChartPurple, name: "Y-BOCS")
    };

            double scoreRange = maxScore - minScore;

            // Get unique assessment dates for X positioning
            var assessmentDates = entries.Select(e => e.Date.Date).Distinct().OrderBy(d => d).ToList();

            foreach (var config in seriesConfigs)
            {
                var pts = new List<(DrawingPointF pt, int val, DateTime date)>();

                // Build points for this series
                foreach (var entry in entries)
                {
                    var score = config.selector(entry);
                    if (!score.HasValue) continue;

                    // Find X position based on date
                    int dateIndex = assessmentDates.IndexOf(entry.Date.Date);
                    float x = chartArea.X + (dateIndex * chartArea.Width / Math.Max(1, assessmentDates.Count - 1));

                    if (assessmentDates.Count == 1)
                        x = chartArea.X + chartArea.Width / 2f;

                    var yn = (score.Value - minScore) / scoreRange;
                    var y = chartArea.Bottom - (yn * chartArea.Height);

                    pts.Add((new DrawingPointF(x, (float)y), score.Value, entry.Date));
                }

                if (pts.Count == 0) continue;

                // Draw lines between points
                using var pen = new System.Drawing.Pen(config.color, 3)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };

                if (pts.Count > 1)
                    g.DrawLines(pen, pts.Select(p => p.pt).ToArray());

                // Draw points and labels
                using var brush = new SolidBrush(config.color);
                using var whitePen = new System.Drawing.Pen(DrawingColor.White, 2);
                using var labelFont = new DrawingFont("Segoe UI", 8, DrawingFontStyle.Bold);
                using var labelBrush = new SolidBrush(DrawingColor.FromArgb(50, 50, 50));

                foreach (var p in pts)
                {
                    // Draw point
                    g.FillEllipse(brush, p.pt.X - 6, p.pt.Y - 6, 12, 12);
                    g.DrawEllipse(whitePen, p.pt.X - 6, p.pt.Y - 6, 12, 12);

                    // Draw score label above point
                    var scoreText = p.val.ToString();
                    var textSize = g.MeasureString(scoreText, labelFont);
                    g.DrawString(scoreText, labelFont, labelBrush, p.pt.X - textSize.Width/2, p.pt.Y - 20);
                }
            }
        }

        // CLEAN: Legend below chart
        private int DrawLegend_Clean(DrawingGraphics g, int x, int y, int width)
        {
            var legendItems = new[]
            {
        ("PHQ-9", ModernTheme.ChartBlue),
        ("GAD-7", ModernTheme.ChartGreen),
        ("BDI-II", ModernTheme.ChartOrange),
        ("PCL-5", ModernTheme.ChartCyan),
        ("Y-BOCS", ModernTheme.ChartPurple)
    };

            using var legendFont = new DrawingFont("Segoe UI", 10);
            using var blackBrush = new SolidBrush(DrawingColor.Black);

            int itemSpacing = Math.Max(120, width / legendItems.Length);
            int currentX = x + (width - (legendItems.Length * itemSpacing)) / 2; // Center the legend
            const int legendHeight = 30;

            foreach (var (label, color) in legendItems)
            {
                // Draw line sample
                using var pen = new System.Drawing.Pen(color, 4);
                g.DrawLine(pen, currentX, y + 10, currentX + 25, y + 10);

                // Draw label
                g.DrawString(label, legendFont, blackBrush, currentX + 30, y + 5);
                currentX += itemSpacing;
            }

            return y + legendHeight;
        }

        // CLEAN: Summary panel with proper spacing
        private int DrawSummaryPanel_Clean(
      DrawingGraphics g, List<ScoreEntry> entries, int margin, int y,
      int pageWidth, DrawingFont headerFont, DrawingFont bodyFont)
        {
            // FIXED: Better spacing system
            const int PAD_X = 32;              // ⬅ INCREASED horizontal padding
            const int PAD_Y = 28;              // ⬅ INCREASED vertical padding
            const int TITLE_GAP = 20;          // ⬅ INCREASED gap after title
            const int COL_GAP = 48;            // ⬅ INCREASED column gap
            const int SUBHEAD_GAP = 18;        // ⬅ INCREASED gap under subheads
            const int ROW_GAP = 16;            // ⬅ INCREASED vertical space between rows

            int lineH = (int)Math.Ceiling(g.MeasureString("Ag", bodyFont).Height) + 8; // ⬅ INCREASED line height

            var phqBase = entries.FirstOrDefault(e => e.PHQ9.HasValue);
            var phqLast = entries.LastOrDefault(e => e.PHQ9.HasValue);
            var bdiBase = entries.FirstOrDefault(e => e.BDI2.HasValue);
            var bdiLast = entries.LastOrDefault(e => e.BDI2.HasValue);

            bool hasPHQ = phqBase != null && phqLast != null;
            bool hasBDI = bdiBase != null && bdiLast != null;

            int titleH = (int)Math.Ceiling(g.MeasureString("Key Clinical Outcomes", headerFont).Height);
            int colHeaderH = (int)Math.Ceiling(g.MeasureString("PHQ-9 Analysis", headerFont).Height) + SUBHEAD_GAP;

            int rowsLeft = hasPHQ ? 3 : 1;
            int rowsRight = hasBDI ? 3 : 1;
            int leftBlockH = colHeaderH + rowsLeft * (lineH + ROW_GAP);
            int rightBlockH = colHeaderH + rowsRight * (lineH + ROW_GAP);

            int contentH = Math.Max(leftBlockH, rightBlockH);
            int cardW = pageWidth - margin * 2;
            int cardH = PAD_Y + titleH + TITLE_GAP + contentH + PAD_Y;

            var cardRect = new DrawingRectangle(margin, y, cardW, cardH);
            ModernTheme.DrawCard(g, cardRect);

            using var titleBrush = new SolidBrush(ModernTheme.Brand);
            using var textBrush = new SolidBrush(ModernTheme.Ink);
            using var mutedBrush = new SolidBrush(ModernTheme.Muted);
            using var successBrush = new SolidBrush(ModernTheme.Success);
            using var dangerBrush = new SolidBrush(ModernTheme.Danger);
            using var gridPen = new System.Drawing.Pen(ModernTheme.Line, 1);

            int x0 = cardRect.X + PAD_X;
            int y0 = cardRect.Y + PAD_Y;
            g.DrawString("Key Clinical Outcomes", headerFont, titleBrush, x0, y0);

            int contentTop = y0 + titleH + TITLE_GAP;
            int innerW = cardRect.Width - PAD_X * 2;
            int colW = (innerW - COL_GAP) / 2;

            int leftX = x0;
            int rightX = x0 + colW + COL_GAP;

            g.DrawLine(gridPen, rightX - (COL_GAP / 2), contentTop, rightX - (COL_GAP / 2), cardRect.Bottom - PAD_Y);

            // LEFT: PHQ-9 with proper spacing
            int yL = contentTop;
            g.DrawString("PHQ-9 Analysis", headerFont, titleBrush, leftX, yL);
            yL += colHeaderH;

            if (hasPHQ)
            {
                double imp = ((double)(phqBase!.PHQ9!.Value - phqLast!.PHQ9!.Value) / phqBase.PHQ9.Value) * 100.0;
                bool resp = imp >= 50;

                yL = DrawLabelValueRow(g, "Baseline:", $" {phqBase.PHQ9}    Latest: {phqLast.PHQ9}",
                                       leftX, yL, colW, bodyFont, mutedBrush, textBrush, lineH, ROW_GAP);

                string respText = resp ? "YES (≥50%)" : "No (<50%)";
                var respBrush = resp ? successBrush : dangerBrush;
                yL = DrawLabelValueRow(g, "Response:", respText,
                                       leftX, yL, colW, bodyFont, mutedBrush, respBrush, lineH, ROW_GAP);

                yL = DrawLabelValueRow(g, "Improvement:", $"{imp:F1}%",
                                       leftX, yL, colW, bodyFont, mutedBrush, textBrush, lineH, ROW_GAP);
            }
            else
            {
                yL = DrawLabelValueRow(g, "PHQ-9:", "Insufficient data",
                                       leftX, yL, colW, bodyFont, mutedBrush, textBrush, lineH, ROW_GAP);
            }

            // RIGHT: BDI-II with proper spacing
            int yR = contentTop;
            g.DrawString("BDI-II Analysis", headerFont, titleBrush, rightX, yR);
            yR += colHeaderH;

            if (hasBDI)
            {
                bool rem = bdiLast!.BDI2!.Value <= 14;
                double imp = ((double)(bdiBase!.BDI2!.Value - bdiLast.BDI2!.Value) / bdiBase.BDI2.Value) * 100.0;

                yR = DrawLabelValueRow(g, "Baseline:", $" {bdiBase.BDI2}    Latest: {bdiLast.BDI2}",
                                       rightX, yR, colW, bodyFont, mutedBrush, textBrush, lineH, ROW_GAP);

                string remText = rem ? "YES (≤14)" : "No (>14)";
                var remBrush = rem ? successBrush : dangerBrush;
                yR = DrawLabelValueRow(g, "Remission:", remText,
                                       rightX, yR, colW, bodyFont, mutedBrush, remBrush, lineH, ROW_GAP);

                yR = DrawLabelValueRow(g, "Improvement:", $"{imp:F1}%",
                                       rightX, yR, colW, bodyFont, mutedBrush, textBrush, lineH, ROW_GAP);
            }
            else
            {
                yR = DrawLabelValueRow(g, "BDI-II:", "Insufficient data",
                                       rightX, yR, colW, bodyFont, mutedBrush, textBrush, lineH, ROW_GAP);
            }

            return cardRect.Bottom + 32; // ⬅ INCREASED gap after summary panel
        }

        // adds explicit extra vertical gap per row
        private int DrawLabelValueRow(
            DrawingGraphics g, string label, string value,
            int x, int y, int colWidth,
            DrawingFont font, SolidBrush labelBrush, SolidBrush valueBrush,
            int lineHeight, int extraGap)
        {
            int labelW = (int)Math.Ceiling(g.MeasureString(label, font).Width);
            int valueX = x + Math.Min(labelW + 10, colWidth / 2);

            g.DrawString(label, font, labelBrush, x, y);
            g.DrawString(value, font, valueBrush, valueX, y);

            return y + lineHeight + extraGap; // ← extra breathing room
        }



        // ADD these supporting methods to your MainWindow class:

        private void DrawRemissionBand(DrawingGraphics g, DrawingRectangle chartArea, double minScore, double maxScore, List<ScoreEntry> entries)
        {
            if (minScore <= 14 && entries.Any(e => e.BDI2.HasValue))
            {
                double scoreRange = maxScore - minScore;
                var remissionY = (float)(chartArea.Bottom - ((14 - minScore) / scoreRange * chartArea.Height));
                var bandHeight = Math.Max(0, chartArea.Bottom - remissionY);

                if (bandHeight > 0)
                {
                    using var remissionBrush = new SolidBrush(DrawingColor.FromArgb(120, ModernTheme.RemissionBand));
                    g.FillRectangle(remissionBrush, chartArea.X, remissionY, chartArea.Width, bandHeight);

                    using var labelBrush = new SolidBrush(ModernTheme.Success);
                    using var labelFont = ModernTheme.GetModernFont(9);
                    g.DrawString("Remission Zone (BDI-II ≤ 14)", labelFont, labelBrush, chartArea.X + 10, remissionY - 20);
                }
            }
        }

     

    





        [SupportedOSPlatform("windows")]
        private void DrawPage2Content(
       DrawingGraphics g, string patientId, List<ScoreEntry> entries,
       int pageWidth, int pageHeight, int margin, int yOffset = 0)
        {
            var page2Bottom = pageHeight - margin - 90 + yOffset; // Reserve space for footer
            var currentY = margin + yOffset;

            var reconnectBlue = DrawingColor.FromArgb(43, 95, 117);

            using var headerFont = new DrawingFont("Segoe UI", 16, DrawingFontStyle.Bold);
            using var subHeaderFont = new DrawingFont("Segoe UI", 14, DrawingFontStyle.Bold);
            using var bodyFont = new DrawingFont("Segoe UI", 12);
            using var smallFont = new DrawingFont("Segoe UI", 11);

            // Header
            currentY = DrawPage2Header(g, patientId, margin, currentY, pageWidth, headerFont, bodyFont, reconnectBlue);

            // Assessment table with fixed column widths
            currentY = DrawFullAssessmentTable_Enhanced(
                g, entries, margin, currentY, pageWidth, page2Bottom, smallFont, subHeaderFont);

            currentY += 32; // More space after table

            // FIXED: Ensure Clinical Notes section shows properly
            if (currentY < page2Bottom - 100) // Only if we have reasonable space
            {
                DrawClinicalNotesSection_Enhanced(g, entries, margin, currentY, pageWidth, page2Bottom, subHeaderFont, bodyFont);
            }

            // Footer
            DrawPageFooter(g, patientId, 2, pageWidth, pageHeight, margin);
        }

        private int DrawPage2Header(
            DrawingGraphics g, string patientId, int margin, int currentY,
            int pageWidth, DrawingFont headerFont, DrawingFont bodyFont, DrawingColor reconnectBlue)
        {
            using var blueBrush = new SolidBrush(reconnectBlue);
            using var grayBrush = new SolidBrush(DrawingColor.DarkGray);

            // Title
            var title = $"Clinical Data Appendix - Patient {patientId}";
            g.DrawString(title, headerFont, blueBrush, margin, currentY);
            var titleH = (int)Math.Ceiling(g.MeasureString(title, headerFont).Height);
            currentY += titleH + 12;          // ← space under the title

            // Subtitle
            var sub = "Complete assessment history and clinical notes";
            g.DrawString(sub, bodyFont, grayBrush, margin, currentY);
            var subH = (int)Math.Ceiling(g.MeasureString(sub, bodyFont).Height);
            currentY += subH + 22;            // ← extra space before the first section

            return currentY;
        }

       

        // ✅ New footer method
        private void DrawPageFooter(DrawingGraphics g, string patientId, int pageNo, int pageWidth, int pageHeight, int margin)
        {
            using var footerFont = new DrawingFont("Arial", 9);
            using var grayBrush = new SolidBrush(DrawingColor.DimGray);

            var text = $"{patientId} • {DateTime.Now:yyyy-MM-dd HH:mm} • Page {pageNo} of 2";
            var textSize = g.MeasureString(text, footerFont);
            var x = (pageWidth - textSize.Width) / 2;
            var y = pageHeight - margin + 10;

            g.DrawString(text, footerFont, grayBrush, x, y);
        }

       


        // ✅ Updated button handler with proper dialog filters
        private async void GenerateTwoPageReport_Click(object sender, RoutedEventArgs e)
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

                var entries = await _dbContext.ScoreEntries
       .AsNoTracking()
       .Where(e => e.PatientId == patientId)
       .OrderBy(e => e.Date)
       .ToListAsync();

                if (!entries.Any())
                {
                    MessageBox.Show($"No data available for patient {patientId}.");
                    return;
                }

                // Ask user for format preference
                var formatChoice = MessageBox.Show(
                    "Choose output format:\n\n" +
                    "YES = PDF (recommended for printing)\n" +
                    "NO = PNG images (for editing/sharing)\n" +
                    "CANCEL = abort",
                    "Report Format",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (formatChoice == MessageBoxResult.Cancel) return;

                bool generatePdf = formatChoice == MessageBoxResult.Yes;

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"Clinical_Report_{patientId}_{DateTime.Now:yyyyMMdd_HHmm}",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                // ✅ Set filter based on chosen format
                if (generatePdf)
                {
                    dialog.Filter = "PDF Files|*.pdf|All Files|*.*";
                    dialog.DefaultExt = "pdf";
                }
                else
                {
                    dialog.Filter = "PNG Images|*.png|All Files|*.*";
                    dialog.DefaultExt = "png";
                }

                if (dialog.ShowDialog() == true)
                {
                    CreateTwoPageClinicalReport(patientId, entries, dialog.FileName, generatePdf);
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

        private void SetupGraphicsQuality(DrawingGraphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // ✅ FIXED: Better for PNG/PDF bitmaps - crisp text instead of smudgy ClearType
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

            g.Clear(DrawingColor.White);
        }

        private int DrawFullAssessmentTable_Enhanced(
      DrawingGraphics g, List<ScoreEntry> entries, int margin,
      int currentY, int pageWidth, int pageBottom,
      DrawingFont cellFont, DrawingFont subHeaderFont)
        {
            // Colors & pens
            using var blueBrush = new SolidBrush(DrawingColor.FromArgb(43, 95, 117));
            using var blackBrush = new SolidBrush(DrawingColor.Black);
            using var mutedBrush = new SolidBrush(DrawingColor.FromArgb(130, 130, 130)); // slightly lighter
            using var headerBg = new SolidBrush(DrawingColor.FromArgb(234, 244, 250));
            using var altRowBrush = new SolidBrush(DrawingColor.FromArgb(248, 250, 252));
            using var gridPen = new System.Drawing.Pen(DrawingColor.FromArgb(225, 229, 235), 1); // light
            using var headerPen = new System.Drawing.Pen(DrawingColor.FromArgb(210, 214, 220), 1);

            using var headerFont = new DrawingFont("Segoe UI", 13, DrawingFontStyle.Bold);
            using var dataFont = new DrawingFont("Segoe UI", 12);

            // Density (switch to true if you ever want it tighter)
            const bool COMFORTABLE = true;

            // Layout constants (more airy)
            int HEADER_H = COMFORTABLE ? 44 : 36;
            int ROW_H = COMFORTABLE ? 44 : 34;
            int ROW_GAP = COMFORTABLE ? 4 : 2;
            int CELL_PAD_X = 12;
            int TITLE_GAP = COMFORTABLE ? 26 : 18;
            int AFTER_HEAD = COMFORTABLE ? 8 : 4;

            // Optional: drop columns that are entirely empty to reduce clutter
            bool omitEmptyColumns = true;

            // Build columns dynamically
            var cols = new List<(string Label, Func<ScoreEntry, string> Get, bool RightAlign)>
    {
        ("Date",   e => e.Date.ToString("yyyy-MM-dd"), false),
        ("PHQ-9",  e => e.PHQ9?.ToString() ?? "—",     true),
        ("GAD-7",  e => e.GAD7?.ToString() ?? "—",     true),
        ("BDI-II", e => e.BDI2?.ToString() ?? "—",     true),
        ("PCL-5",  e => e.PCL5?.ToString() ?? "—",     true),
        ("Y-BOCS", e => e.YBOCS?.ToString() ?? "—",    true)
    };

            if (omitEmptyColumns)
            {
                bool allPclEmpty = entries.Count == 0 || entries.All(x => x.PCL5  == null);
                bool allYbocsEmpty = entries.Count == 0 || entries.All(x => x.YBOCS == null);
                if (allPclEmpty) cols.RemoveAll(c => c.Label == "PCL-5");
                if (allYbocsEmpty) cols.RemoveAll(c => c.Label == "Y-BOCS");
            }

            // Section title
            const string sectionTitle = "Complete Assessment History";
            g.DrawString(sectionTitle, subHeaderFont, blueBrush, margin, currentY);
            var titleH = (int)Math.Ceiling(g.MeasureString(sectionTitle, subHeaderFont).Height);
            currentY += titleH + TITLE_GAP;

            int tableWidth = pageWidth - (margin * 2);
            int dateColWidth = 200; // wider date column
            int colGap = 8;         // small visual gap; we won't draw vertical lines

            // Compute widths
            int nonDateCols = cols.Count - 1;
            int scoreArea = tableWidth - dateColWidth - (nonDateCols * colGap);
            int scoreW = nonDateCols > 0 ? Math.Max(110, scoreArea / nonDateCols) : 0;

            // Header background
            var headerRect = new DrawingRectangle(margin, currentY, tableWidth, HEADER_H);
            g.FillRectangle(headerBg, headerRect);
            g.DrawLine(headerPen, margin, currentY + HEADER_H - 1, margin + tableWidth, currentY + HEADER_H - 1);

            // Header labels (center for headers)
            int headX = margin;
            for (int i = 0; i < cols.Count; i++)
            {
                int colW = (i == 0) ? dateColWidth : scoreW;
                var size = g.MeasureString(cols[i].Label, headerFont);
                float hx = headX + (colW - size.Width) / 2f;
                float hy = currentY + (HEADER_H - size.Height) / 2f;
                g.DrawString(cols[i].Label, headerFont, blueBrush, hx, hy);
                headX += colW + (i < cols.Count - 1 ? colGap : 0);
            }

            currentY += HEADER_H + AFTER_HEAD;

            // Rows
            int rowsDrawn = 0;
            for (int r = 0; r < entries.Count; r++)
            {
                int required = ROW_H + ROW_GAP;
                if (currentY + required > pageBottom - 80) break; // leave space for footnotes, etc.

                var rowRect = new DrawingRectangle(margin, currentY, tableWidth, ROW_H);

                // Zebra background (start with light on first visible row)
                if (r % 2 == 0) g.FillRectangle(altRowBrush, rowRect);

                // Horizontal separators only (clean look)
                g.DrawLine(gridPen, margin, currentY + ROW_H, margin + tableWidth, currentY + ROW_H);

                // Draw cells
                int x = margin;
                for (int c = 0; c < cols.Count; c++)
                {
                    int colW = (c == 0) ? dateColWidth : scoreW;
                    string val = cols[c].Get(entries[r]);
                    var size = g.MeasureString(val, dataFont);

                    // Left for date; right for numbers
                    float tx = cols[c].RightAlign
                        ? x + colW - CELL_PAD_X - size.Width
                        : x + CELL_PAD_X;

                    float ty = currentY + (ROW_H - size.Height) / 2f;

                    // Muted dash for missing values
                    var brush = (val == "—") ? mutedBrush : blackBrush;
                    g.DrawString(val, dataFont, brush, tx, ty);

                    x += colW + (c < cols.Count - 1 ? colGap : 0);
                }

                currentY += ROW_H + ROW_GAP;
                rowsDrawn++;
            }

            // (Optional) you can draw a note if columns were omitted
            if (omitEmptyColumns)
            {
                string note = "Columns with no recorded values were omitted.";
                var size = g.MeasureString(note, cellFont);
                g.DrawString(note, cellFont, mutedBrush, margin, currentY + 6);
                currentY += (int)size.Height + 10;
            }

            return currentY;
        }


        // UPDATED: Clinical Notes with enhanced formatting
        private void DrawClinicalNotesSection_Enhanced(
            DrawingGraphics g, List<ScoreEntry> entries, int margin, int currentY,
            int pageWidth, int pageBottom, DrawingFont subHeaderFont, DrawingFont bodyFont)
        {
            using var titleBrush = new SolidBrush(DrawingColor.FromArgb(43, 95, 117));
            using var cardBg = new SolidBrush(DrawingColor.FromArgb(248, 250, 252));
            using var borderPen = new System.Drawing.Pen(DrawingColor.FromArgb(220, 224, 230), 1);
            using var dateBrush = new SolidBrush(DrawingColor.FromArgb(70, 70, 70));
            using var textBrush = new SolidBrush(DrawingColor.Black);
            using var notesBodyFont = new DrawingFont("Segoe UI", 9);     // ⬅ SMALLER than bodyFont
            using var notesTitleFont = new DrawingFont("Segoe UI", 11, DrawingFontStyle.Bold); // ⬅ SMALLER header

            const int CARD_PAD = 25; // More padding
            const int CARD_GAP_Y = 20; // More gap between cards
            const int SECTION_GAP = 32; // More section gap

            // Clinical Notes header
            const string notesTitle = "Clinical Notes";
            g.DrawString(notesTitle, subHeaderFont, titleBrush, margin, currentY);
            var titleH = (int)Math.Ceiling(g.MeasureString(notesTitle, subHeaderFont).Height);
            currentY += titleH + SECTION_GAP;

            var notesEntries = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Note))
                .OrderBy(e => e.Date)
                .ToList();

            if (!notesEntries.Any())
            {
                g.DrawString("No clinical notes recorded for this patient.", bodyFont, dateBrush, margin, currentY);
                return;
            }

            // Show summary first
            g.DrawString($"Total clinical notes: {notesEntries.Count}", bodyFont, dateBrush, margin, currentY);
            currentY += 60; // More space after summary

            // Note cards with enhanced formatting
            int maxWidth = pageWidth - (margin * 2);
            int layoutTextWidth = maxWidth - (CARD_PAD * 2);

            int notesShown = 0;
            foreach (var entry in notesEntries)
            {
                // Format as: YYYY-MM-DD: <note text>
                var formattedNote = $"{entry.Date:yyyy-MM-dd}: {entry.Note}";

                var textSize = g.MeasureString(formattedNote, bodyFont, new SizeF(layoutTextWidth, 1000));
                int cardHeight = Math.Max(70, (int)Math.Ceiling(textSize.Height) + (CARD_PAD * 2) + 8); // More height

                if (currentY + cardHeight > pageBottom - 50)
                {
                    var remaining = notesEntries.Count - notesShown;
                    if (remaining > 0)
                    {
                        g.DrawString($"({remaining} additional notes omitted due to page space)", bodyFont, dateBrush, margin, currentY);
                    }
                    break;
                }

                var cardRect = new DrawingRectangle(margin, currentY, maxWidth, cardHeight);
                g.FillRectangle(cardBg, cardRect);
                g.DrawRectangle(borderPen, cardRect);

                // Draw formatted note content (YYYY-MM-DD: note text)
                var textRect = new DrawingRectangle(
                    cardRect.X + CARD_PAD, cardRect.Y + CARD_PAD,
                    layoutTextWidth, cardRect.Height - (CARD_PAD * 2));

                g.DrawString(formattedNote, bodyFont, textBrush, textRect);

                currentY += cardHeight + CARD_GAP_Y;
                notesShown++;
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
             

                // Get all entries for this patient from DATABASE
                var patientEntries = await _dbContext.ScoreEntries
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
                _dbContext.RemoveRange(patientEntries);
                await _dbContext.SaveChangesAsync();

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
            if (_handlingSelection) return;
            _handlingSelection = true;
            try
            {
                var id = PatientSelector.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(id))
                    id = PatientSelector.Text?.Trim();

                if (string.IsNullOrWhiteSpace(id)) return;

                await LoadPatientDataFromDatabase(id);
                UpdateChartForPatient(id);
                PatientIdBox.Text = id;

                await RecalculateMetricsOnlyAsync();
                UpdateCurrentPatientOutcome(id);
            }
            finally { _handlingSelection = false; }
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
     .AsNoTracking()
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
         


                // FILTER FROM DATABASE instead of patientData dictionary
                var filteredEntries = await _dbContext.ScoreEntries
      .AsNoTracking()
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
           

                // LOAD FROM DATABASE
                var allEntries = await _dbContext.ScoreEntries
     .AsNoTracking()
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
                    var context = _dbContext;
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
                            var db = _dbContext;
                            var allEntries = await _dbContext.ScoreEntries
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