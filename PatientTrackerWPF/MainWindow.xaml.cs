using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel.DataAnnotations;

namespace PatientTrackerWPF
{
    public partial class MainWindow : Window
    {
        private string filterId = "";
        private Dictionary<string, List<ScoreEntry>> patientData = new Dictionary<string, List<ScoreEntry>>();

        public class ScoreEntry
        {
            [Key]
            public int Id { get; set; }

            [Required]
            public string PatientId { get; set; } = string.Empty;

            public int PHQ9 { get; set; }
            public int GAD7 { get; set; }
            public int PCL5 { get; set; }
            public int BDI2 { get; set; }
            public int YBOCS { get; set; }
            public string Note { get; set; } = string.Empty;
            public DateTime Date { get; set; }
        }

        public SeriesCollection ScoreSeriesCollection { get; set; }
        public ChartValues<int> Phq9Values { get; set; } = new ChartValues<int>();
        public ChartValues<int> Gad7Values { get; set; } = new ChartValues<int>();
        public ChartValues<int> Bdi2Values { get; set; } = new ChartValues<int>();
        public ChartValues<int> Pcl5Values { get; set; } = new ChartValues<int>();
        public ChartValues<int> YbocsValues { get; set; } = new ChartValues<int>();
        public List<string> TimeLabels { get; set; } = new List<string>();

        public MainWindow()
        {
            InitializeComponent();

            ScoreSeriesCollection = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "PHQ-9",
                    Values = Phq9Values,
                    LineSmoothness = 0,
                    StrokeThickness = 3,
                    Stroke = Brushes.MediumBlue,
                    Fill = Brushes.Transparent,
                    PointGeometrySize = 8
                },
                new LineSeries
                {
                    Title = "GAD-7",
                    Values = Gad7Values,
                    LineSmoothness = 0,
                    StrokeThickness = 3,
                    Stroke = Brushes.ForestGreen,
                    Fill = Brushes.Transparent,
                    PointGeometrySize = 8
                },
                new LineSeries
                {
                    Title = "BDI-II",
                    Values = Bdi2Values,
                    LineSmoothness = 0,
                    StrokeThickness = 3,
                    Stroke = Brushes.OrangeRed,
                    Fill = Brushes.Transparent,
                    PointGeometrySize = 8
                },
                new LineSeries
                {
                    Title = "PCL-5",
                    Values = Pcl5Values,
                    LineSmoothness = 0,
                    StrokeThickness = 3,
                    Stroke = Brushes.DarkCyan,
                    Fill = Brushes.Transparent,
                    PointGeometrySize = 8
                },
                new LineSeries
                {
                    Title = "Y-BOCS",
                    Values = YbocsValues,
                    LineSmoothness = 0,
                    StrokeThickness = 3,
                    Stroke = Brushes.Purple,
                    Fill = Brushes.Transparent,
                    PointGeometrySize = 8
                }
            };

            ScoresGrid.ItemsSource = new List<ScoreEntry>();
            DataContext = this;
        }

        private void AddScore_Click(object sender, RoutedEventArgs e)
        {
            string patientId = PatientIdBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(patientId))
            {
                MessageBox.Show("Please enter a Patient ID.");
                return;
            }

            // Allow entry of notes only - no validation required for scores

            // Add to dictionary if new
            if (!patientData.ContainsKey(patientId))
            {
                patientData[patientId] = new List<ScoreEntry>();
            }

            // Add to ComboBox if new
            if (!PatientSelector.Items.Contains(patientId))
                PatientSelector.Items.Add(patientId);

            PatientSelector.SelectedItem = patientId;

            var entry = new ScoreEntry
            {
                PatientId = patientId,
                PHQ9 = TryParseOrDefault(Phq9Box.Text),
                GAD7 = TryParseOrDefault(Gad7Box.Text),
                BDI2 = TryParseOrDefault(Bdi2Box.Text),
                PCL5 = TryParseOrDefault(PCL5Total.Text),
                YBOCS = TryParseOrDefault(YBOCS.Text),
                Note = NoteBox.Text?.Trim() ?? string.Empty,
                Date = DatePicker.SelectedDate ?? DateTime.Today
            };

            patientData[patientId].Add(entry);

            UpdateChartForPatient(patientId);

            // Clear fields
            Phq9Box.Clear();
            Gad7Box.Clear();
            Bdi2Box.Clear();
            PCL5Total.Clear();
            YBOCS.Clear();
            PatientIdBox.Clear();
            NoteBox.Clear();
            DatePicker.SelectedDate = DateTime.Today;

            // Refresh grid properly
            ScoresGrid.ItemsSource = null;
            ScoresGrid.ItemsSource = patientData[patientId];
            ScoresGrid.ScrollIntoView(entry);
        }

        // Helper
        private int TryParseOrDefault(string text)
        {
            return int.TryParse(text, out int value) ? value : 0; // Use 0 for missing values
        }

        private void UpdateChartForPatient(string patientId)
        {
            if (!patientData.ContainsKey(patientId)) return;

            var scores = patientData[patientId].OrderBy(s => s.Date).ToList();

            ScoresGrid.ItemsSource = null;
            ScoresGrid.ItemsSource = scores;

            Phq9Values.Clear();
            Gad7Values.Clear();
            Bdi2Values.Clear();
            Pcl5Values.Clear();
            YbocsValues.Clear();
            TimeLabels.Clear();

            foreach (var s in scores)
            {
                // Add all values including zeros for consistent chart display
                Phq9Values.Add(s.PHQ9);
                Gad7Values.Add(s.GAD7);
                Bdi2Values.Add(s.BDI2);
                Pcl5Values.Add(s.PCL5);
                YbocsValues.Add(s.YBOCS);

                TimeLabels.Add(s.Date.ToString("dd-MMM"));
            }

            // Clear and set X-axis
            PatientProgressChart.AxisX.Clear();
            PatientProgressChart.AxisX.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Date",
                Labels = TimeLabels,
                LabelsRotation = 45,
                Separator = new LiveCharts.Wpf.Separator
                {
                    Step = 1,
                    IsEnabled = true
                }
            });

            // Set Y-axis for better visualization - increased max to accommodate higher scores
            PatientProgressChart.AxisY.Clear();
            PatientProgressChart.AxisY.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Score",
                MinValue = 0,
                MaxValue = 80, // Increased from 35 to 80 to accommodate higher scores
                Separator = new LiveCharts.Wpf.Separator
                {
                    Step = 10, // Increased step size for better readability
                    IsEnabled = true
                }
            });
        }

        private void ExportToPng_Click(object sender, RoutedEventArgs e)
        {
            string patientId = PatientSelector.Text?.Trim();
            if (string.IsNullOrWhiteSpace(patientId) || !patientData.ContainsKey(patientId))
            {
                MessageBox.Show("Please select a valid patient.");
                return;
            }

            // Step 1: Fill ExportLayout content
            ExportPatientId.Text = $"Patient ID: {patientId}";
            ExportDate.Text = $"Report Generated: {DateTime.Now:MMMM dd, yyyy hh:mm tt}";
            ExportScoreGrid.ItemsSource = null;
            ExportScoreGrid.ItemsSource = patientData[patientId];

            // Fill notes with better formatting - limit length to prevent excessive height
            var notesText = string.Join("\n\n", patientData[patientId]
                .Where(p => !string.IsNullOrWhiteSpace(p.Note))
                .Select(p => $"{p.Date:MMM dd}: {p.Note}")
                .Take(10)); // Limit to 10 most recent notes to control height

            ExportNoteText.Text = string.IsNullOrWhiteSpace(notesText) ?
                "No treatment notes available." : notesText;

            // Step 2: Capture chart image with smaller dimensions
            double exportChartWidth = 1500;
            double exportChartHeight = 250;

            PatientProgressChart.Measure(new Size(exportChartWidth, exportChartHeight));
            PatientProgressChart.Arrange(new Rect(0, 0, exportChartWidth, exportChartHeight));
            PatientProgressChart.UpdateLayout();

            var chartBmp = new RenderTargetBitmap(
                (int)exportChartWidth,
                (int)exportChartHeight,
                96, 96, PixelFormats.Pbgra32);

            chartBmp.Render(PatientProgressChart);
            ExportChartImage.Source = chartBmp;

            // Step 3: Prepare ExportLayout with fixed dimensions
            ExportLayout.Visibility = Visibility.Visible;

            // Force specific size for consistent export
            ExportLayout.Width = 1600;
            ExportLayout.Height = 1200;

            ExportLayout.Measure(new Size(1600, 1200));
            ExportLayout.Arrange(new Rect(0, 0, 1600, 1200));
            ExportLayout.UpdateLayout();

            // Let the UI finish layout rendering
            Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            // Step 4: Render and Save PNG with fixed dimensions
            var renderBmp = new RenderTargetBitmap(1600, 1200, 96, 96, PixelFormats.Pbgra32);
            renderBmp.Render(ExportLayout);

            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(renderBmp));

            string fileName = $"PatientReport_{patientId}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            using (var stream = new FileStream(fileName, FileMode.Create))
            {
                png.Save(stream);
            }

            ExportLayout.Visibility = Visibility.Collapsed;
            MessageBox.Show($"Exported to {fileName}\nSize: 1600x1200 pixels", "Success");
        }

        private void PatientSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var patientId = PatientSelector.SelectedItem?.ToString();
            if (!string.IsNullOrWhiteSpace(patientId))
            {
                UpdateChartForPatient(patientId);
            }
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            filterId = FilterBox.Text.Trim();
            var filteredData = patientData
                .SelectMany(kvp => kvp.Value)
                .Where(s => string.IsNullOrWhiteSpace(filterId) || s.PatientId.Contains(filterId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.PatientId)
                .ThenBy(s => s.Date)
                .ToList();

            ScoresGrid.ItemsSource = filteredData;
        }

        private void ExportToCsv_Click(object sender, RoutedEventArgs e)
        {
            var filtered = patientData
                .SelectMany(kvp => kvp.Value)
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
                sb.AppendLine($"{s.PatientId},{s.Date:yyyy-MM-dd},{s.PHQ9},{s.GAD7},{s.BDI2},{s.PCL5},{s.YBOCS},\"{s.Note?.Replace("\"", "\"\"")}\"");
            }

            var filePath = $"PatientScores_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            File.WriteAllText(filePath, sb.ToString());

            MessageBox.Show($"Exported to {filePath}", "Success");
        }
    }
}