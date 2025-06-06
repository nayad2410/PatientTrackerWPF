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

namespace PatientTrackerWPF
{
    public partial class MainWindow : Window
    {
        private string filterId = "";
        private Dictionary<string, List<ScoreEntry>> patientData = new();

        public class ScoreEntry
        {
            public string PatientId { get; set; }
            public int PHQ9 { get; set; }
            public int GAD7 { get; set; }
            public int PCL5 { get; set; }
            public int BDI2 { get; set; }
            public int PCL5Total { get; set; }
            public int YBOCS { get; set; }
            //I will add a new field for notes
            //or comments if needed
            public string Note { get; set; }
            //I will add date field
            public DateTime Date { get; set; }



        }

        public SeriesCollection ScoreSeriesCollection { get; set; }
        public ChartValues<int> Phq9Values { get; set; } = new();
        public ChartValues<int> Gad7Values { get; set; } = new();
        public ChartValues<int> Bdi2Values { get; set; } = new();
        public ChartValues<int> Pcl5Values { get; set; } = new();
        public ChartValues<int> YbocsValues { get; set; } = new();
        public List<string> TimeLabels { get; set; } = new();

        public MainWindow()
        {
            InitializeComponent();

            ScoreSeriesCollection = new SeriesCollection
            {
                new LineSeries { Title = "PHQ-9", Values = Phq9Values, PointGeometrySize = 10 },
                new LineSeries { Title = "GAD-7", Values = Gad7Values, PointGeometrySize = 10 },
                new LineSeries { Title = "BDI-II", Values = Bdi2Values, PointGeometrySize = 10 },
                new LineSeries { Title = "PCL-5", Values = Pcl5Values, PointGeometrySize = 10 },
                new LineSeries { Title = "Y-BOCS", Values = YbocsValues, PointGeometrySize = 10 }
            };

            ScoresGrid.ItemsSource = new List<ScoreEntry>();
            DataContext = this;
        }

        private void AddScore_Click(object sender, RoutedEventArgs e)
        {
            string patientId = PatientIdBox.Text.Trim(); // ✅ Get from input box

            if (string.IsNullOrWhiteSpace(patientId))
            {
                MessageBox.Show("Please enter a Patient ID.");
                return;
            }

            // Optional: warn if all score fields are blank
            if (string.IsNullOrWhiteSpace(Phq9Box.Text) &&
                string.IsNullOrWhiteSpace(Gad7Box.Text) &&
                string.IsNullOrWhiteSpace(Bdi2Box.Text) &&
                string.IsNullOrWhiteSpace(PCL5Total.Text) &&
                string.IsNullOrWhiteSpace(YBOCS.Text))
            {
                MessageBox.Show("Please enter at least one score.");
                return;
            }

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
                Note = NoteBox.Text.Trim(),
                // Fix: Use the correct instance of DatePicker
                Date = (DatePicker?.SelectedDate ?? DateTime.Today).Date + DateTime.Now.TimeOfDay

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

            // Refresh grid properly
            ScoresGrid.ItemsSource = null;
            ScoresGrid.ItemsSource = patientData[patientId];
            ScoresGrid.ScrollIntoView(entry);
        }

        // Helper
        private int TryParseOrDefault(string text)
        {
            return int.TryParse(text, out int value) ? value : -1; // Use -1 to represent missing
        }


        private void UpdateChartForPatient(string patientId)
        {
            if (!patientData.ContainsKey(patientId)) return;

            var scores = patientData[patientId];

            // 1. Update the grid
            ScoresGrid.ItemsSource = null;
            ScoresGrid.ItemsSource = scores;
            progressChart.AxisX[0].Labels = TimeLabels;


            // 2. Clear old values
            Phq9Values.Clear();
            Gad7Values.Clear();
            Bdi2Values.Clear();
            Pcl5Values.Clear();
            YbocsValues.Clear();
            TimeLabels.Clear();

            // 3. Add new values and labels
            foreach (var s in scores)
            {
                Phq9Values.Add(s.PHQ9);
                Gad7Values.Add(s.GAD7);
                Bdi2Values.Add(s.BDI2);
                Pcl5Values.Add(s.PCL5);
                YbocsValues.Add(s.YBOCS);
                TimeLabels.Add(s.Date.ToString("MM/dd")); // Or use .ToShortDateString()
            }

            progressChart.AxisX[0].Labels = TimeLabels;
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

            // Fill notes (or default message)
            ExportNoteText.Text = string.Join("\n\n", patientData[patientId]
                .Where(p => !string.IsNullOrWhiteSpace(p.Note))
                .Select(p => $"{p.Date:g}: {p.Note}"));
            if (string.IsNullOrWhiteSpace(ExportNoteText.Text))
                ExportNoteText.Text = "No treatment notes available.";

            // Step 2: Capture chart image into ExportChartImage
            // Render a larger version of the chart to fill full width
            double exportChartWidth = 2000;
            double exportChartHeight = 300;

            progressChart.Measure(new Size(exportChartWidth, exportChartHeight));
            progressChart.Arrange(new Rect(0, 0, exportChartWidth, exportChartHeight));
            progressChart.UpdateLayout();

            var chartBmp = new RenderTargetBitmap(
                (int)exportChartWidth,
                (int)exportChartHeight,
                96, 96, PixelFormats.Pbgra32);

            chartBmp.Render(progressChart);
            ExportChartImage.Source = chartBmp;

            // Step 3: Prepare ExportLayout
            ExportLayout.Visibility = Visibility.Visible;
            ExportLayout.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            ExportLayout.Arrange(new Rect(0, 0, ExportLayout.DesiredSize.Width, ExportLayout.DesiredSize.Height));
            ExportLayout.UpdateLayout();

            // Let the UI finish layout rendering
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            int width = (int)Math.Ceiling(ExportLayout.ActualWidth);
            int height = (int)Math.Ceiling(ExportLayout.ActualHeight);

            // Fallback if values are too small
            if (width < 1000) width = 2200;
            if (height < 500) height = 1600;


            if (width == 0 || height == 0)
            {
                MessageBox.Show("Export area has zero width or height.");
                return;
            }

            // Step 4: Render and Save PNG
            var renderBmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            renderBmp.Render(ExportLayout);

            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(renderBmp));

            string fileName = $"PatientReport_{patientId}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            using (var stream = new FileStream(fileName, FileMode.Create))
            {
                png.Save(stream);
            }

            ExportLayout.Visibility = Visibility.Collapsed;
            MessageBox.Show($"Exported to {fileName}", "Success");
        }



        //    // Save to PNG
        //    var png = new PngBitmapEncoder();
        //    png.Frames.Add(BitmapFrame.Create(renderBmp));
        //    var fileName = $"PatientReport_{patientId}_{DateTime.Now:yyyyMMdd_HHmmss}.png";

        //    using (var stream = new FileStream(fileName, FileMode.Create))
        //    {
        //        png.Save(stream);
        //    }

        //    MessageBox.Show($"Exported to {fileName}", "Success");
        //}


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
            ScoresGrid.ItemsSource = patientData
                .SelectMany(kvp => kvp.Value)
                .Where(s => string.IsNullOrWhiteSpace(filterId) || s.PatientId.Contains(filterId))
                .ToList();
        }

        private void ExportToCsv_Click(object sender, RoutedEventArgs e)
        {
            var filtered = patientData
                .SelectMany(kvp => kvp.Value)
                .Where(s => string.IsNullOrWhiteSpace(filterId) || s.PatientId.Contains(filterId))
                .ToList();

            if (!filtered.Any())
            {
                MessageBox.Show("No matching data to export.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("PatientId,PHQ9,GAD7,BDI2,PCL5,YBOCS,Note");
            foreach (var s in filtered)
            {
                sb.AppendLine($"{s.PatientId},{s.PHQ9},{s.GAD7},{s.BDI2},{s.PCL5},{s.YBOCS},{s.Date.ToShortDateString()},{s.Note}");
            }

            var filePath = $"PatientScores_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            File.WriteAllText(filePath, sb.ToString());

            MessageBox.Show($"Exported to {filePath}", "Success");
        }
    }
}
