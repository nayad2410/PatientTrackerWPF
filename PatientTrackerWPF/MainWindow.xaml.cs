using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using Microsoft.Win32;
using PatientTrackerWPF.Data;
using PatientTrackerWPF.Models;
using PatientTrackerWPF.Services;
using PdfSharp.Drawing;
using PdfSharp.Xps;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using System.Runtime.Versioning;
using Separator = LiveCharts.Wpf.Separator;

//  these for the professional report 
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingFont = System.Drawing.Font;
using DrawingFontStyle = System.Drawing.FontStyle;
using DrawingBrushes = System.Drawing.Brushes;
using DrawingPens = System.Drawing.Pens;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingPointF = System.Drawing.PointF;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Drawing;
using Brushes = System.Windows.Media.Brushes;

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


        // ─── Chart Collections ────────────────────────────────────────────────
        public SeriesCollection ScoreSeriesCollection { get; set; } = new SeriesCollection();
        public ChartValues<DateTimePoint> Phq9Values { get; set; } = new();
        public ChartValues<DateTimePoint> Gad7Values { get; set; } = new();
        public ChartValues<DateTimePoint> Bdi2Values { get; set; } = new();
        public ChartValues<DateTimePoint> Pcl5Values { get; set; } = new();
        public ChartValues<DateTimePoint> YbocsValues { get; set; } = new();

        public MainWindow(
               AuthenticationService authenticationService,
               ICurrentUserService currentUserService,
               ClinicalMetricsService clinicalMetricsService,
               RemissionTrackingService remissionTrackingService)
        {
            InitializeComponent();

            // Assign injected services
            authService = authenticationService;
            this.currentUserService = currentUserService;
            metricsService = clinicalMetricsService;
            remissionService = remissionTrackingService;

            // Added ScoreConverter to resources programmatically if XAML fails
            if (!Resources.Contains("ScoreConverter"))
            {
                Resources.Add("ScoreConverter", new ScoreConverter());
            }

            DataContext = this;
            InitializeChart();
            SetupResponsiveLayout();
            UpdateUserDisplay();
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
                    this.Title = $"Reconnect Progress Tracker - {authService.GetCurrentUserFullName()} ({authService.CurrentUser?.Role})";
                }
                else if (currentUserService?.CurrentUser != null)
                {
                    CurrentUserText.Text = currentUserService.CurrentUser.FullName ?? currentUserService.CurrentUser.Username ?? "Unknown User";
                    UserRoleText.Text = currentUserService.CurrentUser.Role ?? "Unknown Role";
                    this.Title = $"Reconnect Progress Tracker - {CurrentUserText.Text} ({UserRoleText.Text})";
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

            //if (!Resources.Contains("ScoreConverter"))
            //{
            //    Resources.Add("ScoreConverter", new ScoreConverter());
            //}
            //DataContext = this;
            //InitializeChart();
            //SetupResponsiveLayout();




        }


        // ─── Add Score Click with Score Validation ──────────────────────────────────────────────────
        // Updated AddScore_Click method with proper user tracking
        private void AddScore_Click(object sender, RoutedEventArgs e)
        {
            var id = PatientIdBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                MessageBox.Show("Please enter a Patient ID.");
                return;
            }

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

            if (!patientData.ContainsKey(id))
                patientData[id] = new();

            var selectedDate = DatePicker.SelectedDate ?? DateTime.Today;

            // Get current user info for audit fields
            var currentUsername = authService?.GetCurrentUsername() ??
                                 currentUserService?.CurrentUser?.Username ??
                                 "Unknown";

            var currentUserId = authService?.CurrentUser?.Id ??
                               currentUserService?.CurrentUser?.Id;

            // Check for duplicate date entries
            var existingEntry = patientData[id].FirstOrDefault(e => e.Date.Date == selectedDate.Date);
            if (existingEntry != null)
            {
                var result = MessageBox.Show(
                    $"There is already a score entry for patient {id} on {selectedDate:yyyy-MM-dd}.\n\n" +
                    "Would you like to update the existing entry instead?",
                    "Duplicate Date Entry",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Update existing entry
                    existingEntry.PHQ9 = TryParseOrNull(Phq9Box.Text);
                    existingEntry.GAD7 = TryParseOrNull(Gad7Box.Text);
                    existingEntry.BDI2 = TryParseOrNull(Bdi2Box.Text);
                    existingEntry.PCL5 = TryParseOrNull(PCL5Total.Text);
                    existingEntry.YBOCS = TryParseOrNull(YBOCS.Text);
                    existingEntry.Note = NoteBox.Text.Trim();
                    existingEntry.UpdatedBy = currentUsername;
                    existingEntry.UpdatedByUserId = currentUserId;
                    existingEntry.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    return; // User chose not to update
                }
            }
            else
            {
                // Create new entry
                var entry = new ScoreEntry
                {
                    PatientId = id,
                    PHQ9 = TryParseOrNull(Phq9Box.Text),
                    GAD7 = TryParseOrNull(Gad7Box.Text),
                    BDI2 = TryParseOrNull(Bdi2Box.Text),
                    PCL5 = TryParseOrNull(PCL5Total.Text),
                    YBOCS = TryParseOrNull(YBOCS.Text),
                    Note = NoteBox.Text.Trim(),
                    Date = selectedDate,
                    CreatedBy = currentUsername,
                    CreatedByUserId = currentUserId,
                    CreatedAt = DateTime.UtcNow
                };
                patientData[id].Add(entry);
            }

            if (!PatientSelector.Items.Contains(id))
                PatientSelector.Items.Add(id);
            PatientSelector.SelectedItem = id;

            UpdateChartForPatient(id);

            // Clear inputs
            Phq9Box.Clear(); Gad7Box.Clear(); Bdi2Box.Clear();
            PCL5Total.Clear(); YBOCS.Clear(); NoteBox.Clear(); PatientIdBox.Clear();
            DatePicker.SelectedDate = DateTime.Today;

            ScoresGrid.ItemsSource = null;
            ScoresGrid.ItemsSource = patientData[id];
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

                // FIXED: Only add data points when scores exist (not null)
                foreach (var entry in scores)
                {
                    // Only add data points for actual scores, skip null values
                    if (entry.PHQ9.HasValue)  // FIXED: Check HasValue instead of >= 0
                        Phq9Values.Add(new DateTimePoint(entry.Date, (double)entry.PHQ9.Value));

                    if (entry.GAD7.HasValue)  // FIXED: Check HasValue instead of >= 0
                        Gad7Values.Add(new DateTimePoint(entry.Date, (double)entry.GAD7.Value));

                    if (entry.BDI2.HasValue)  // FIXED: Check HasValue instead of >= 0
                        Bdi2Values.Add(new DateTimePoint(entry.Date, (double)entry.BDI2.Value));

                    if (entry.PCL5.HasValue)  // FIXED: Check HasValue instead of >= 0
                        Pcl5Values.Add(new DateTimePoint(entry.Date, (double)entry.PCL5.Value));

                    if (entry.YBOCS.HasValue)  // FIXED: Check HasValue instead of >= 0
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

                // FIXED: Smart date separator with error handling
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
                // FIXED: Add null checks
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
                        // Within a month - show weeklyFup
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
                // FIXED: Graceful error handling - just log and continue
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
         /*       Padding = new Thickness(8, 4),*/
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

        private void GenerateProfessionalReport_Click(object sender, RoutedEventArgs e)
        {
            var patientId = PatientSelector.Text?.Trim();
            if (string.IsNullOrWhiteSpace(patientId) || !patientData.ContainsKey(patientId))
            {
                MessageBox.Show("Please select a valid patient.");
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var entries = patientData[patientId].OrderBy(e => e.Date).ToList();
                if (!entries.Any())
                {
                    MessageBox.Show("No data available for this patient.");
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
            // Report dimensions
            const int width = 1200;
            const int height = 1600;

            var bitmap = new DrawingBitmap(width, height);
            using var g = DrawingGraphics.FromImage(bitmap);

            // High quality rendering
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            // Background
            g.Clear(DrawingColor.White);

            // Fonts
            var titleFont = new DrawingFont("Arial", 24, DrawingFontStyle.Bold);
            var headerFont = new DrawingFont("Arial", 16, DrawingFontStyle.Bold);
            var subHeaderFont = new DrawingFont("Arial", 12, DrawingFontStyle.Bold);
            var bodyFont = new DrawingFont("Arial", 10);
            var smallFont = new DrawingFont("Arial", 9);

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
                g.FillRectangle(headerRect, 0, 0, width, 120);
                g.FillRectangle(headerBrush, 0, 0, width, 80);

                // Reconnect logo area
                g.DrawString("RECONNECT", titleFont, DrawingBrushes.White, 50, 25);
                g.DrawString("MENTAL HEALTH", new DrawingFont("Arial", 12), DrawingBrushes.LightGray, 50, 55);

                // Report title
                g.DrawString("Clinical Progress Report", headerFont, DrawingBrushes.White, width - 350, 25);
                g.DrawString("Mental Health Assessment Report", bodyFont, DrawingBrushes.LightGray, width - 350, 50);
            }

            currentY = 140;

            // PATIENT INFO SECTION
            g.DrawString($"Patient ID: {patientId}", headerFont, new SolidBrush(reconnectBlue), 50, currentY);
            currentY += 30;
            g.DrawString($"Report Generated: {DateTime.Now:MMMM dd, yyyy h:mm tt}", bodyFont, new SolidBrush(darkGray), 50, currentY);
            g.DrawString($"Assessment Period: {entries.First().Date:MMM dd, yyyy} to {entries.Last().Date:MMM dd, yyyy}",
                        bodyFont, new SolidBrush(darkGray), 400, currentY);
            currentY += 50;

            // DATA TABLE SECTION
            g.DrawString("Assessment Scores Over Time", headerFont, new SolidBrush(reconnectBlue), 50, currentY);
            currentY += 35;

            // Create data table
            var tableY = CreateDataTable(g, entries, 50, currentY, width - 100);
            currentY = tableY + 40;

            // PROGRESS CHART SECTION
            g.DrawString($"Progress Track: {entries.First().Date:MMM dd/yy} to {entries.Last().Date:MMM dd/yy}",
                        headerFont, new SolidBrush(reconnectBlue), 50, currentY);
            currentY += 35;

            // Create progress chart
            var chartHeight = CreateProgressChart(g, entries, 50, currentY, width - 100, 400);
            currentY += chartHeight + 40;

            // TREATMENT NOTES SECTION
            g.DrawString("Recent Treatment Notes", headerFont, new SolidBrush(reconnectBlue), 50, currentY);
            currentY += 35;

            var recentNotes = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Note))
                .OrderByDescending(e => e.Date)
                .Take(5)
                .ToList();

            if (recentNotes.Any())
            {
                foreach (var entry in recentNotes)
                {
                    g.DrawString($"• {entry.Date:yyyy-MM-dd}: {entry.Note}", bodyFont, new SolidBrush(darkGray),
                                70, currentY);
                    currentY += 25;
                }
            }
            else
            {
                g.DrawString("No treatment notes available.", bodyFont, new SolidBrush(darkGray), 70, currentY);
            }

            // FOOTER
            currentY = height - 60;
            using (var footerBrush = new SolidBrush(lightGray))
            {
                g.FillRectangle(footerBrush, 0, currentY - 10, width, 70);
                g.DrawString("This report is generated by Reconnect Mental Health Assessment System",
                            smallFont, new SolidBrush(darkGray), 50, currentY + 10);
                g.DrawString($"Report ID: RPT-{patientId}-{DateTime.Now:yyyyMMddHHmm}",
                            smallFont, new SolidBrush(darkGray), 50, currentY + 30);
            }

            // Cleanup fonts
            titleFont.Dispose();
            headerFont.Dispose();
            subHeaderFont.Dispose();
            bodyFont.Dispose();
            smallFont.Dispose();

            return bitmap;
        }

        private int CreateDataTable(DrawingGraphics g, List<ScoreEntry> entries, int x, int y, int tableWidth)
        {
            var cellFont = new DrawingFont("Arial", 9);
            var headerFont = new DrawingFont("Arial", 9, DrawingFontStyle.Bold);
            var reconnectBlue = DrawingColor.FromArgb(43, 95, 117);
            var lightGray = DrawingColor.FromArgb(240, 240, 240);

            // Take up to 10 most recent entries for the table
            var tableEntries = entries.TakeLast(10).ToList();

            // Column setup
            var columns = new[] { "Date", "PHQ-9", "GAD-7", "BDI-II", "PCL-5", "Y-BOCS" };
            var colWidth = tableWidth / columns.Length;
            var rowHeight = 25;

            var currentY = y;

            // Draw header
            using (var headerBrush = new SolidBrush(reconnectBlue))
            {
                g.FillRectangle(headerBrush, x, currentY, tableWidth, rowHeight);

                for (int i = 0; i < columns.Length; i++)
                {
                    var cellX = x + (i * colWidth);
                    g.DrawString(columns[i], headerFont, DrawingBrushes.White,
                                cellX + 10, currentY + 5);

                    // Draw column separator
                    if (i < columns.Length - 1)
                    {
                        g.DrawLine(DrawingPens.White, cellX + colWidth, currentY,
                                  cellX + colWidth, currentY + rowHeight);
                    }
                }
            }
            currentY += rowHeight;

            // Draw data rows
            for (int row = 0; row < tableEntries.Count; row++)
            {
                var entry = tableEntries[row];
                var isAlternate = row % 2 == 1;

                // Alternate row background
                if (isAlternate)
                {
                    using (var altBrush = new SolidBrush(lightGray))
                    {
                        g.FillRectangle(altBrush, x, currentY, tableWidth, rowHeight);
                    }
                }

                // Data values
                var values = new string[]
                {
            entry.Date.ToString("dd-MMM-yyyy"),
            entry.PHQ9?.ToString() ?? "—",
            entry.GAD7?.ToString() ?? "—",
            entry.BDI2?.ToString() ?? "—",
            entry.PCL5?.ToString() ?? "—",
            entry.YBOCS?.ToString() ?? "—"
                };

                // Draw cells
                for (int i = 0; i < values.Length; i++)
                {
                    var cellX = x + (i * colWidth);
                    g.DrawString(values[i], cellFont, DrawingBrushes.Black,
                                cellX + 10, currentY + 5);

                    // Draw column separator
                    if (i < values.Length - 1)
                    {
                        g.DrawLine(DrawingPens.LightGray, cellX + colWidth, currentY,
                                  cellX + colWidth, currentY + rowHeight);
                    }
                }

                // Draw row separator
                g.DrawLine(DrawingPens.LightGray, x, currentY + rowHeight,
                          x + tableWidth, currentY + rowHeight);

                currentY += rowHeight;
            }

            // Draw table border
            g.DrawRectangle(DrawingPens.Gray, x, y, tableWidth, currentY - y);

            cellFont.Dispose();
            headerFont.Dispose();

            return currentY;
        }

        private int CreateProgressChart(DrawingGraphics g, List<ScoreEntry> entries, int x, int y, int chartWidth, int chartHeight)
        {
            if (!entries.Any()) return y;

            var chartArea = new DrawingRectangle(x + 60, y + 40, chartWidth - 120, chartHeight - 80);
            var reconnectBlue = DrawingColor.FromArgb(43, 95, 117);
            var treatmentPhase = DrawingColor.FromArgb(150, 255, 255, 204); // Light yellow with transparency

            // Draw chart background
            g.FillRectangle(DrawingBrushes.White, chartArea);
            g.DrawRectangle(DrawingPens.Gray, chartArea);

            // Treatment phase background
            var phaseStart = entries.Count > 2 ? 0.2f : 0;
            var phaseEnd = entries.Count > 4 ? 0.8f : 1;

            var phaseStartX = chartArea.X + (int)(chartArea.Width * phaseStart);
            var phaseWidth = (int)(chartArea.Width * (phaseEnd - phaseStart));

            using (var phaseBrush = new SolidBrush(treatmentPhase))
            {
                g.FillRectangle(phaseBrush, phaseStartX, chartArea.Y, phaseWidth, chartArea.Height);
            }

            // Add phase label
            g.DrawString("Treatment Phase", new DrawingFont("Arial", 8), new SolidBrush(reconnectBlue),
                        phaseStartX + 10, chartArea.Y + 10);

            // Draw grid lines
            for (int i = 0; i <= 8; i++)
            {
                var gridY = chartArea.Y + (i * chartArea.Height / 8);
                g.DrawLine(DrawingPens.LightGray, chartArea.X, gridY, chartArea.Right, gridY);

                // Y-axis labels (0-80 scale)
                var value = 80 - (i * 10);
                g.DrawString(value.ToString(), new DrawingFont("Arial", 8), DrawingBrushes.Black,
                            chartArea.X - 30, gridY - 6);
            }

            // Draw vertical grid lines
            for (int i = 0; i <= 10; i++)
            {
                var gridX = chartArea.X + (i * chartArea.Width / 10);
                g.DrawLine(DrawingPens.LightGray, gridX, chartArea.Y, gridX, chartArea.Bottom);
            }

            // Plot assessment lines
            PlotAssessmentLine(g, entries, chartArea, e => e.PHQ9, DrawingColor.Blue, "PHQ-9", 0, 27);
            PlotAssessmentLine(g, entries, chartArea, e => e.GAD7, DrawingColor.Green, "GAD-7", 0, 21);
            PlotAssessmentLine(g, entries, chartArea, e => e.BDI2, DrawingColor.Orange, "BDI-II", 0, 63);
            PlotAssessmentLine(g, entries, chartArea, e => e.PCL5, DrawingColor.DarkCyan, "PCL-5", 0, 80);
            PlotAssessmentLine(g, entries, chartArea, e => e.YBOCS, DrawingColor.Purple, "Y-BOCS", 0, 40);

            // Draw legend
            DrawChartLegend(g, chartArea.Right - 200, chartArea.Y);

            // X-axis labels (dates)
            var dateStep = Math.Max(1, entries.Count / 6);
            for (int i = 0; i < entries.Count; i += dateStep)
            {
                var entry = entries[i];
                var pointX = chartArea.X + (i * chartArea.Width / (entries.Count - 1));
                g.DrawString(entry.Date.ToString("dd-MMM"), new DrawingFont("Arial", 8), DrawingBrushes.Black,
                            pointX - 20, chartArea.Bottom + 10);
            }

            return y + chartHeight;
        }

        private void PlotAssessmentLine(DrawingGraphics g, List<ScoreEntry> entries, DrawingRectangle chartArea,
                                       Func<ScoreEntry, int?> scoreSelector, DrawingColor color,
                                       string label, int minScore, int maxScore)
        {
            var points = new List<DrawingPointF>();

            for (int i = 0; i < entries.Count; i++)
            {
                var score = scoreSelector(entries[i]);
                if (score.HasValue)
                {
                    var x = chartArea.X + (i * chartArea.Width / (entries.Count - 1));
                    // Scale score to chart (0-80 range for display)
                    var normalizedScore = (double)score.Value / maxScore * 80;
                    var y = chartArea.Bottom - (int)(normalizedScore * chartArea.Height / 80);
                    points.Add(new DrawingPointF(x, y));
                }
            }

            if (points.Count > 1)
            {
                using (var pen = new System.Drawing.Pen(color, 3))
                {
                    g.DrawLines(pen, points.ToArray());
                }

                // Draw points
                using (var brush = new SolidBrush(color))
                {
                    foreach (var point in points)
                    {
                        g.FillEllipse(brush, point.X - 4, point.Y - 4, 8, 8);
                    }
                }
            }
        }

        private void DrawChartLegend(DrawingGraphics g, int x, int y)
        {
            var legendItems = new[]
            {
        ("PHQ-9", DrawingColor.Blue),
        ("GAD-7", DrawingColor.Green),
        ("BDI-II", DrawingColor.Orange),
        ("PCL-5", DrawingColor.DarkCyan),
        ("Y-BOCS", DrawingColor.Purple)
    };

            var currentY = y;
            foreach (var (label, color) in legendItems)
            {
                // Draw line sample
                using (var pen = new System.Drawing.Pen(color, 3))
                {
                    g.DrawLine(pen, x, currentY + 5, x + 20, currentY + 5);
                }

                // Draw label
                g.DrawString(label, new DrawingFont("Arial", 9), DrawingBrushes.Black, x + 25, currentY);
                currentY += 20;
            }
        }



        private void NoteBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is string txt)
                MessageBox.Show(txt, "Full Treatment Note");
        }


        private void PatientSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PatientSelector.SelectedItem is string id)
            {
                UpdateChartForPatient(id);

                if (currentMetrics != null)
                {
                    UpdateCurrentPatientOutcome(id);
                    ResetMetricsDisplay();

                }
            }
        }
        private void ResetMetricsDisplay()
        {
            ResponseRateText.Text = "0.0%";
            ResponseCountText.Text = "(0/0)";
            RemissionRateText.Text = "0.0%";
            RemissionCountText.Text = "(0/0)";
            AverageImprovementText.Text = "0.0%";
            EligiblePatientsText.Text = "0";

            QuickResponseRate.Text = "0.0%";
            QuickRemissionRate.Text = "0.0%";

            // Reset colors to default (optional)
            ResponseRateText.Foreground = Brushes.Black;
            RemissionRateText.Foreground = Brushes.Black;
        }


        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            filterId = FilterBox.Text.Trim();
            var list = patientData.Values.SelectMany(v => v)
                         .Where(r => string.IsNullOrEmpty(filterId) || r.PatientId.Contains(filterId, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(r => r.PatientId).ThenBy(r => r.Date)
                         .ToList();
            ScoresGrid.ItemsSource = list;
        }



        private void CalculateMetrics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                currentMetrics = metricsService.CalculateBDI2Metrics(patientData);
                UpdateMetricsDisplay(currentMetrics);

                var selectedPatientId = PatientSelector.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(selectedPatientId))
                {
                    UpdateCurrentPatientOutcome(selectedPatientId);
                }

                MessageBox.Show($"Metrics calculated successfully!\n\n" +
                               $"Eligible patients: {currentMetrics.PatientsWithMultipleAssessments}\n" +
                               $"Response rate: {currentMetrics.ResponseRate:F1}%\n" +
                               $"Remission rate: {currentMetrics.RemissionRate:F1}%",
                               "Clinical Metrics", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error calculating metrics: {ex.Message}", "Error",
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
            ResponseRateText.Text = $"{metrics.ResponseRate:F1}%";
            ResponseCountText.Text = $"({metrics.ResponseCount}/{metrics.PatientsWithMultipleAssessments})"; // Removed "patients"

            RemissionRateText.Text = $"{metrics.RemissionRate:F1}%";
            RemissionCountText.Text = $"({metrics.RemissionCount}/{metrics.PatientsWithMultipleAssessments})"; // Removed "patients"

            AverageImprovementText.Text = $"{metrics.AverageImprovement:F1}%";
            EligiblePatientsText.Text = metrics.PatientsWithMultipleAssessments.ToString();


            QuickResponseRate.Text = $"{metrics.ResponseRate:F1}%";
            QuickRemissionRate.Text = $"{metrics.RemissionRate:F1}%";
        

            // Color coding
            ResponseRateText.Foreground = metrics.ResponseRate >= 50 ? Brushes.Green :
                                         metrics.ResponseRate >= 30 ? Brushes.Orange : Brushes.Red;

            RemissionRateText.Foreground = metrics.RemissionRate >= 30 ? Brushes.Blue :
                                          metrics.RemissionRate >= 15 ? Brushes.Orange : Brushes.Red;
        }

        private void UpdateCurrentPatientOutcome(string patientId)
        {
            if (currentMetrics == null || !patientData.ContainsKey(patientId))
            {
                CurrentPatientOutcome.Visibility = Visibility.Collapsed;
                return;
            }

            var outcome = currentMetrics.PatientOutcomes.FirstOrDefault(p => p.PatientId == patientId);
            if (outcome == null)
            {
                CurrentPatientOutcomeTitle.Text = $"Patient {patientId}: Not eligible for outcome analysis";
                CurrentPatientOutcomeDetails.Text = "Requires baseline BDI-II ≥14 and at least 2 assessments with BDI-II scores.";
                CurrentPatientOutcome.Visibility = Visibility.Visible;
                return;
            }

            CurrentPatientOutcomeTitle.Text = $"Patient {patientId} - Clinical Outcome";

            var details = $"Baseline BDI-II: {outcome.BaselineBDI2} ({outcome.BaselineDate:yyyy-MM-dd}) → " +
                         $"Most Recent: {outcome.MostRecentBDI2} ({outcome.MostRecentDate:yyyy-MM-dd})\n" +
                         $"Improvement: {outcome.PercentImprovement:F1}% over {outcome.DaysBetweenAssessments} days\n" +
                         $"Response (≥50% improvement): {(outcome.HasResponse ? "YES" : "NO")} | " +
                         $"Remission (score <14): {(outcome.HasRemission ? "YES" : "NO")}\n" +
                         $"Total BDI-II assessments: {outcome.TotalAssessments}";

            CurrentPatientOutcomeDetails.Text = details;
            CurrentPatientOutcome.Visibility = Visibility.Visible;
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
                var fileName = $"BDI2_ClinicalOutcomes_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

                File.WriteAllText(fileName, report);

                MessageBox.Show($"Clinical outcomes report exported to {fileName}", "Export Successful",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting report: {ex.Message}", "Export Error",
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

        private void ExportToCsv_Click(object sender, RoutedEventArgs e)
        {
            var filtered = patientData.Values.SelectMany(v => v)
       .Where(s => string.IsNullOrWhiteSpace(filterId) || s.PatientId.Contains(filterId, StringComparison.OrdinalIgnoreCase))
       .OrderBy(s => s.PatientId)
       .ThenBy(s => s.Date)
       .ToList();

            if (!filtered.Any())
            {
                MessageBox.Show("No matching data to export.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("PatientId,Date,PHQ9,GAD7,BDI2,PCL5,YBOCS,Note");
            foreach (var s in filtered)
            {
                // FIXED: Handle null values properly in CSV export - use simple dash for CSV compatibility
                var phq9Str = s.PHQ9.HasValue ? s.PHQ9.Value.ToString() : "-";
                var gad7Str = s.GAD7.HasValue ? s.GAD7.Value.ToString() : "-";
                var bdi2Str = s.BDI2.HasValue ? s.BDI2.Value.ToString() : "-";
                var pcl5Str = s.PCL5.HasValue ? s.PCL5.Value.ToString() : "-";
                var ybocsStr = s.YBOCS.HasValue ? s.YBOCS.Value.ToString() : "-";

                sb.AppendLine($"{s.PatientId},{s.Date:yyyy-MM-dd},{phq9Str},{gad7Str},{bdi2Str},{pcl5Str},{ybocsStr},\"{s.Note?.Replace("\"", "\"\"")}\"");
            }

            var filePath = $"PatientScores_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            File.WriteAllText(filePath, sb.ToString());
            MessageBox.Show($"Exported to {filePath}", "Success");
        }




        //Export to PNG ──────────────────────────────────────────────────────



        //private async void ExportToPng_Click(object sender, RoutedEventArgs e)
        //{
        //    var patientId = PatientSelector.Text?.Trim();
        //    if (string.IsNullOrWhiteSpace(patientId) || !patientData.ContainsKey(patientId))
        //    {
        //        MessageBox.Show("Please select a valid patient.");
        //        return;
        //    }

        //    // Store original window state
        //    var originalWindowState = this.WindowState;
        //    var originalWidth = this.Width;
        //    var originalHeight = this.Height;

        //    try
        //    {
        //        Mouse.OverrideCursor = Cursors.Wait;

        //        // SOLUTION: Temporarily set fixed window size for consistent export
        //        this.WindowState = WindowState.Normal;
        //        this.Width = 1200;
        //        this.Height = 900;
        //        this.UpdateLayout();
        //        await Task.Delay(100);

        //        // Prepare export layout with FIXED dimensions
        //        ExportLayout.Visibility = Visibility.Visible;
        //        ExportLayout.Opacity = 1.0;
        //        ExportLayout.IsHitTestVisible = true;

        //        // FORCE FIXED DIMENSIONS regardless of parent
        //        ExportLayout.Width = 950;
        //        ExportLayout.Height = double.NaN; // Let height auto-calculate
        //        ExportContent.Width = 900;
        //        ExportContent.Height = double.NaN;

        //        // Set up all content
        //        ExportPatientId.Text = $"Patient ID: {patientId}";
        //        ExportDate.Text = $"Report Generated: {DateTime.Now:MMMM dd, yyyy hh:mm tt}";

        //        // Create chart image
        //        var chartRenderSize = new System.Windows.Size(800, 400);
        //        PatientProgressChart.Measure(chartRenderSize);
        //        PatientProgressChart.Arrange(new Rect(chartRenderSize));
        //        PatientProgressChart.UpdateLayout();

        //        var chartBitmap = new RenderTargetBitmap(800, 400, 96, 96, PixelFormats.Pbgra32);
        //        chartBitmap.Render(PatientProgressChart);
        //        ExportChartImage.Source = chartBitmap;

        //        // Set up data
        //        var patientEntries = patientData[patientId].OrderBy(e => e.Date).ToList();
        //        ExportScoreGrid.ItemsSource = null;
        //        ExportScoreGrid.ItemsSource = patientEntries;

        //        // Set up notes
        //        var recentNotes = patientEntries
        //            .Where(e => !string.IsNullOrWhiteSpace(e.Note))
        //            .OrderByDescending(e => e.Date)
        //            .Take(5)
        //            .Select(e => $"{e.Date:yyyy-MM-dd}: {e.Note}")
        //            .ToList();

        //        ExportNoteText.Text = recentNotes.Any()
        //            ? string.Join("\n\n", recentNotes)
        //            : "No treatment notes available.";

        //        // FORCE all child elements to remove height constraints
        //        ExportScoreGrid.Height = double.NaN;
        //        ExportScoreGrid.MaxHeight = double.PositiveInfinity;
        //        ExportNoteText.Height = double.NaN;
        //        ExportNoteText.MaxHeight = double.PositiveInfinity;

        //        // Force layout update with FIXED width
        //        ExportLayout.InvalidateVisual();
        //        ExportLayout.InvalidateMeasure();
        //        ExportLayout.InvalidateArrange();

        //        // Measure with FIXED width, unlimited height
        //        ExportLayout.Measure(new System.Windows.Size(950, double.PositiveInfinity));

        //        // Use FIXED width and calculated height
        //        var layoutWidth = 950;
        //        var layoutHeight = ExportLayout.DesiredSize.Height;

        //        // Arrange with calculated dimensions
        //        ExportLayout.Arrange(new Rect(0, 0, layoutWidth, layoutHeight));
        //        ExportLayout.UpdateLayout();

        //        // Wait for rendering
        //        await Task.Delay(500);

        //        // Multiple dispatcher calls to ensure complete rendering
        //        for (int i = 0; i < 3; i++)
        //        {
        //            Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        //            await Task.Delay(100);
        //        }

        //        // Get final dimensions
        //        var finalWidth = 950;  // FIXED width
        //        var finalHeight = (int)Math.Ceiling(Math.Max(layoutHeight, 800));

        //        // Debug info
        //        System.Diagnostics.Debug.WriteLine($"Fixed export dimensions: {finalWidth} x {finalHeight}");
        //        System.Diagnostics.Debug.WriteLine($"Layout actual: {ExportLayout.ActualWidth} x {ExportLayout.ActualHeight}");
        //        System.Diagnostics.Debug.WriteLine($"Layout desired: {ExportLayout.DesiredSize.Width} x {ExportLayout.DesiredSize.Height}");
        //        System.Diagnostics.Debug.WriteLine($"Window size during export: {this.Width} x {this.Height}");

        //        // Create bitmap with FIXED dimensions
        //        var exportBitmap = new RenderTargetBitmap(
        //            finalWidth,
        //            finalHeight,
        //            96, 96,
        //            PixelFormats.Pbgra32);

        //        exportBitmap.Render(ExportLayout);

        //        // Verify bitmap
        //        if (exportBitmap.PixelWidth == 0 || exportBitmap.PixelHeight == 0)
        //        {
        //            MessageBox.Show("Error: Unable to generate export image. Please try again.",
        //                           "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        //            return;
        //        }

        //        // Save file
        //        var encoder = new PngBitmapEncoder();
        //        encoder.Frames.Add(BitmapFrame.Create(exportBitmap));

        //        var dialog = new Microsoft.Win32.SaveFileDialog
        //        {
        //            FileName = $"PatientReport_{patientId}_{DateTime.Now:yyyyMMdd_HHmm}.png",
        //            Filter = "PNG Image|*.png"
        //        };

        //        if (dialog.ShowDialog() == true)
        //        {
        //            using (var stream = File.Create(dialog.FileName))
        //            {
        //                encoder.Save(stream);
        //            }

        //            var fileInfo = new FileInfo(dialog.FileName);
        //            MessageBox.Show($"Report exported successfully!\n\nFile: {dialog.FileName}\nSize: {finalWidth} x {finalHeight} pixels\nFile size: {fileInfo.Length / 1024:F1} KB\nMethod: Fixed Dimensions",
        //                           "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Export failed: {ex.Message}\n\nDetails: {ex.ToString()}",
        //                       "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //    finally
        //    {
        //        // Restore original window state
        //        this.WindowState = originalWindowState;
        //        this.Width = originalWidth;
        //        this.Height = originalHeight;
        //        this.UpdateLayout();

        //        Mouse.OverrideCursor = null;
        //        ExportLayout.Visibility = Visibility.Collapsed;
        //    }
        //}







        //private void SaveBitmapAsPdf(BitmapSource bmp, string pdfPath)
        //{
        //    using var doc = new PdfSharp.Pdf.PdfDocument();
        //    var page = doc.AddPage();
        //    page.Width  = XUnit.FromPoint(bmp.PixelWidth * 72.0 / ReportDpi);
        //    page.Height = XUnit.FromPoint(bmp.PixelHeight * 72.0 / ReportDpi);

        //    using var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
        //    using var ms = new MemoryStream();
        //    var enc = new PngBitmapEncoder();
        //    enc.Frames.Add(BitmapFrame.Create(bmp));
        //    enc.Save(ms);
        //    ms.Position = 0;

        //    using var img = PdfSharp.Drawing.XImage.FromStream(ms);
        //    gfx.DrawImage(img, 0, 0, page.Width, page.Height);

        //    doc.Save(pdfPath);
        //}





        // ─── : Edit/Delete Options ──────────────────────────────────────
        private void ScoresGrid_MouseDoubleClick_1(object sender, MouseButtonEventArgs e)
        {
            if (ScoresGrid.SelectedItem is ScoreEntry selected)
            {
                // Show edit/delete dialog instead of automatic deletion
                var result = MessageBox.Show(
                    $"What would you like to do with this entry?\n\n" +
                    $"Patient: {selected.PatientId}\n" +
                    $"Date: {selected.Date:yyyy-MM-dd}\n" +
                    $"Scores: PHQ-9={selected.PHQ9}, GAD-7={selected.GAD7}, BDI-II={selected.BDI2}\n\n" +
                    "Click 'Yes' to EDIT or 'No' to DELETE",
                    "Edit or Delete Entry",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // EDIT: Load into input fields - FIXED for null values
                    PatientIdBox.Text = selected.PatientId;
                    Phq9Box.Text = selected.PHQ9.HasValue ? selected.PHQ9.Value.ToString() : "";  // FIXED
                    Gad7Box.Text = selected.GAD7.HasValue ? selected.GAD7.Value.ToString() : "";  // FIXED
                    Bdi2Box.Text = selected.BDI2.HasValue ? selected.BDI2.Value.ToString() : "";  // FIXED
                    PCL5Total.Text = selected.PCL5.HasValue ? selected.PCL5.Value.ToString() : "";  // FIXED
                    YBOCS.Text = selected.YBOCS.HasValue ? selected.YBOCS.Value.ToString() : "";  // FIXED
                    NoteBox.Text = selected.Note;
                    DatePicker.SelectedDate = selected.Date;

                    // Remove the entry so it can be re-added with updates
                    patientData[selected.PatientId].Remove(selected);
                    UpdateChartForPatient(selected.PatientId);

                    MessageBox.Show("Entry loaded for editing. Make your changes and click 'Add Score' to update.",
                                   "Edit Mode", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (result == MessageBoxResult.No)
                {
                    // DELETE: Confirm and remove
                    var confirmDelete = MessageBox.Show(
                        "Are you sure you want to permanently delete this entry?",
                        "Confirm Delete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (confirmDelete == MessageBoxResult.Yes)
                    {
                        patientData[selected.PatientId].Remove(selected);
                        UpdateChartForPatient(selected.PatientId);
                        MessageBox.Show("Entry deleted successfully.", "Deleted",
                                       MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                // Cancel = do nothing
            }
        }

        private void ScoresGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
           
        }
    }
}