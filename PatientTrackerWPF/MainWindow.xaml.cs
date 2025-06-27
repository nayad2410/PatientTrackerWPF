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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Xps.Packaging;
using Separator = LiveCharts.Wpf.Separator;

namespace PatientTrackerWPF
{
    public partial class MainWindow : Window
    {
        // ─── Fields ────────────────────────────────────────────────────────────
        private string filterId = "";
        private Dictionary<string, List<ScoreEntry>> patientData = new();
        private ClinicalMetricsService metricsService = new();
        private RemissionTrackingService remissionService = new();
        private readonly AuthenticationService? authService;
        private ClinicalMetricsService.ClinicalMetrics? currentMetrics;
        private List<ScoreEntry> currentPatientEntries = new List<ScoreEntry>();
        // Constants pulled from resources at runtime
        private double ReportWidth => (double)FindResource("ReportWidth");
        private int ReportDpi => (int)FindResource("ReportDpi");
        private double[] HistoryColumnWidths
          => ((double[])FindResource("HistoryColumnWidths")).ToArray();
        private Brush PrimaryBrush => (Brush)FindResource("PrimaryBrush");
        private Brush SecondaryBrush => (Brush)FindResource("SecondaryBrush");
        private Brush AccentBrush => (Brush)FindResource("AccentBrush");

        // ─── Chart Collections ────────────────────────────────────────────────
        public SeriesCollection ScoreSeriesCollection { get; set; }
        public ChartValues<DateTimePoint> Phq9Values { get; set; } = new();
        public ChartValues<DateTimePoint> Gad7Values { get; set; } = new();
        public ChartValues<DateTimePoint> Bdi2Values { get; set; } = new();
        public ChartValues<DateTimePoint> Pcl5Values { get; set; } = new();
        public ChartValues<DateTimePoint> YbocsValues { get; set; } = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            InitializeChart();
            SetupResponsiveLayout();
        }

        public MainWindow(AuthenticationService authenticationService) : this()
        {
            authService = authenticationService;
            CurrentUserText.Text = authService.GetCurrentUserFullName();
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

        // ─── Add Score Click ──────────────────────────────────────────────────
        // ─── Add Score Click with Score Validation ──────────────────────────────────────────────────
        private void AddScore_Click(object sender, RoutedEventArgs e)
        {
            var id = PatientIdBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                MessageBox.Show("Please enter a Patient ID.");
                return;
            }

            // ADDED: Validate score ranges (0-80)
            var validationErrors = new List<string>();

            if (!string.IsNullOrWhiteSpace(Phq9Box.Text))
            {
                if (!int.TryParse(Phq9Box.Text, out int phq9) || phq9 < 0 || phq9 > 80)
                    validationErrors.Add("PHQ-9 must be between 0 and 80");
            }

            if (!string.IsNullOrWhiteSpace(Gad7Box.Text))
            {
                if (!int.TryParse(Gad7Box.Text, out int gad7) || gad7 < 0 || gad7 > 80)
                    validationErrors.Add("GAD-7 must be between 0 and 80");
            }

            if (!string.IsNullOrWhiteSpace(Bdi2Box.Text))
            {
                if (!int.TryParse(Bdi2Box.Text, out int bdi2) || bdi2 < 0 || bdi2 > 80)
                    validationErrors.Add("BDI-II must be between 0 and 80");
            }

            if (!string.IsNullOrWhiteSpace(PCL5Total.Text))
            {
                if (!int.TryParse(PCL5Total.Text, out int pcl5) || pcl5 < 0 || pcl5 > 80)
                    validationErrors.Add("PCL-5 must be between 0 and 80");
            }

            if (!string.IsNullOrWhiteSpace(YBOCS.Text))
            {
                if (!int.TryParse(YBOCS.Text, out int ybocs) || ybocs < 0 || ybocs > 80)
                    validationErrors.Add("Y-BOCS must be between 0 and 80");
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
                    existingEntry.PHQ9 = TryParseOrDefault(Phq9Box.Text);
                    existingEntry.GAD7 = TryParseOrDefault(Gad7Box.Text);
                    existingEntry.BDI2 = TryParseOrDefault(Bdi2Box.Text);
                    existingEntry.PCL5 = TryParseOrDefault(PCL5Total.Text);
                    existingEntry.YBOCS = TryParseOrDefault(YBOCS.Text);
                    existingEntry.Note = NoteBox.Text.Trim();
                    existingEntry.CreatedBy = authService?.GetCurrentUsername() ?? "Unknown";
                    existingEntry.CreatedAt = DateTime.UtcNow;
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
                    PHQ9 = TryParseOrDefault(Phq9Box.Text),
                    GAD7 = TryParseOrDefault(Gad7Box.Text),
                    BDI2 = TryParseOrDefault(Bdi2Box.Text),
                    PCL5 = TryParseOrDefault(PCL5Total.Text),
                    YBOCS = TryParseOrDefault(YBOCS.Text),
                    Note = NoteBox.Text.Trim(),
                    Date = selectedDate,
                    CreatedBy = authService?.GetCurrentUsername() ?? "Unknown",
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

        private int TryParseOrDefault(string txt)
            => int.TryParse(txt, out var v) ? v : -1;

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

                // FIXED: Add null checks for chart and axis
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

                    // FIXED: Check separator exists before setting
                    if (PatientProgressChart.AxisX[0].Separator != null)
                    {
                        PatientProgressChart.AxisX[0].Separator.Step = TimeSpan.FromDays(1).Ticks;
                    }
                    return;
                }

                // Add data points
                foreach (var entry in scores)
                {
                    Phq9Values.Add(new DateTimePoint(entry.Date, entry.PHQ9 >= 0 ? entry.PHQ9 : 0));
                    Gad7Values.Add(new DateTimePoint(entry.Date, entry.GAD7 >= 0 ? entry.GAD7 : 0));
                    Bdi2Values.Add(new DateTimePoint(entry.Date, entry.BDI2 >= 0 ? entry.BDI2 : 0));
                    Pcl5Values.Add(new DateTimePoint(entry.Date, entry.PCL5 >= 0 ? entry.PCL5 : 0));
                    YbocsValues.Add(new DateTimePoint(entry.Date, entry.YBOCS >= 0 ? entry.YBOCS : 0));
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

        // IMPROVED: Better color variation (keep your approach but with more colors)
        private Color GetNoteBoxColor(int index)
        {
            var colors = new[]
            {
        Color.FromRgb(255, 255, 204), // Light Yellow
        Color.FromRgb(230, 243, 255), // Light Blue  
        Color.FromRgb(240, 248, 230), // Light Green
        Color.FromRgb(255, 240, 245), // Light Pink
        Color.FromRgb(245, 245, 220), // Beige
        Color.FromRgb(230, 230, 250), // Lavender
        Color.FromRgb(240, 255, 240), // Honeydew
        Color.FromRgb(255, 250, 240), // Floral White
        Color.FromRgb(255, 228, 225), // Misty Rose
        Color.FromRgb(240, 248, 255), // Alice Blue
        Color.FromRgb(250, 240, 230), // Linen
        Color.FromRgb(245, 255, 250)  // Mint Cream
    };
            return colors[index % colors.Length];
        }




        private void NoteBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is string txt)
                MessageBox.Show(txt, "Full Treatment Note");
        }

        // ─── Event Handlers ─────────────────────────────────────────────────
        private void PatientSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PatientSelector.SelectedItem is string id)
            {
                UpdateChartForPatient(id);

                if (currentMetrics != null)
                {
                    UpdateCurrentPatientOutcome(id);
                }
            }
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

        // ─── RESTORED: All your original working methods ─────────────────────

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
                var phq9Str = s.PHQ9 == -1 ? "Not entered" : s.PHQ9.ToString();
                var gad7Str = s.GAD7 == -1 ? "Not entered" : s.GAD7.ToString();
                var bdi2Str = s.BDI2 == -1 ? "Not entered" : s.BDI2.ToString();
                var pcl5Str = s.PCL5 == -1 ? "Not entered" : s.PCL5.ToString();
                var ybocsStr = s.YBOCS == -1 ? "Not entered" : s.YBOCS.ToString();

                sb.AppendLine($"{s.PatientId},{s.Date:yyyy-MM-dd},{phq9Str},{gad7Str},{bdi2Str},{pcl5Str},{ybocsStr},\"{s.Note?.Replace("\"", "\"\"")}\"");
            }

            var filePath = $"PatientScores_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            File.WriteAllText(filePath, sb.ToString());
            MessageBox.Show($"Exported to {filePath}", "Success");
        }




        //Export to PNG ──────────────────────────────────────────────────────
        // FIXED Export Method - Capture the FULL ExportLayout with background
        private async void ExportToPng_Click(object sender, RoutedEventArgs e)
        {
            var patientId = PatientSelector.Text?.Trim();
            if (string.IsNullOrWhiteSpace(patientId) || !patientData.ContainsKey(patientId))
            {
                MessageBox.Show("Please select a valid patient.");
                return;
            }

            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                // 1) Populate export panel
                ExportPatientId.Text = $"Patient ID: {patientId}";
                ExportDate.Text = $"Report Generated: {DateTime.Now:MMMM dd, yyyy hh:mm tt}";

                var exportData = patientData[patientId]
                    .OrderBy(s => s.Date)
                    .Select(s => new {
                        Date = s.Date.ToString("yyyy-MM-dd"),
                        PHQ9 = s.PHQ9 >= 0 ? s.PHQ9.ToString() : "—",
                        GAD7 = s.GAD7 >= 0 ? s.GAD7.ToString() : "—",
                        BDI2 = s.BDI2 >= 0 ? s.BDI2.ToString() : "—",
                        PCL5 = s.PCL5 >= 0 ? s.PCL5.ToString() : "—",
                        YBOCS = s.YBOCS >= 0 ? s.YBOCS.ToString() : "—",
                        Note = s.Note ?? ""
                    })
                    .ToList();
                ExportScoreGrid.ItemsSource = exportData;

                // 2) Capture chart image
                PatientProgressChart.UpdateLayout();
                var chartBmp = new RenderTargetBitmap(
                    (int)PatientProgressChart.ActualWidth,
                    (int)PatientProgressChart.ActualHeight,
                    96, 96, PixelFormats.Pbgra32);
                chartBmp.Render(PatientProgressChart);
                ExportChartImage.Source = chartBmp;

                // 3) Show and measure export layout
                ExportLayout.Visibility = Visibility.Visible;
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

                // 4) SIMPLE: Use the resource width and measure height naturally
                double designWidth = (double)FindResource("ReportWidth"); // 900px
                ExportLayout.Measure(new Size(designWidth, double.PositiveInfinity));
                ExportLayout.Arrange(new Rect(0, 0, designWidth, ExportLayout.DesiredSize.Height));
                ExportLayout.UpdateLayout();

                // Wait for final layout
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

                // 5) SIMPLE: Render at screen resolution first, then scale
                double finalWidth = ExportLayout.ActualWidth;
                double finalHeight = ExportLayout.ActualHeight;

                // Create high-res bitmap
                const double scale = 300.0 / 96.0; // 300 DPI
                int pixelWidth = (int)(finalWidth * scale);
                int pixelHeight = (int)(finalHeight * scale);

                var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 300, 300, PixelFormats.Pbgra32);

                // Simple transform and render
                var transform = new ScaleTransform(scale, scale);
                ExportLayout.RenderTransform = transform;
                rtb.Render(ExportLayout);
                ExportLayout.RenderTransform = null; // Reset transform

                // 6) Save file
                var dlg = new SaveFileDialog
                {
                    Filter = "PNG Image|*.png",
                    FileName = $"PatientReport_{patientId}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                };

                if (dlg.ShowDialog() == true)
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));
                    using var fs = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write);
                    encoder.Save(fs);
                    MessageBox.Show($"Report exported successfully:\n{dlg.FileName}", "Export Complete",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                if (btn != null) btn.IsEnabled = true;
                ExportLayout.Visibility = Visibility.Collapsed;
            }
        }






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





        // ─── FIXED: Edit/Delete Options ──────────────────────────────────────
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
                    // EDIT: Load into input fields
                    PatientIdBox.Text = selected.PatientId;
                    Phq9Box.Text = selected.PHQ9 == -1 ? "" : selected.PHQ9.ToString();
                    Gad7Box.Text = selected.GAD7 == -1 ? "" : selected.GAD7.ToString();
                    Bdi2Box.Text = selected.BDI2 == -1 ? "" : selected.BDI2.ToString();
                    PCL5Total.Text = selected.PCL5 == -1 ? "" : selected.PCL5.ToString();
                    YBOCS.Text = selected.YBOCS == -1 ? "" : selected.YBOCS.ToString();
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
            // Handle grid selection if needed
        }
    }
}