using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

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
            string patientId = PatientIdBox.Text.Trim(); // ✅ use the input box, not the ComboBox

            if (string.IsNullOrWhiteSpace(patientId) ||
                string.IsNullOrWhiteSpace(Phq9Box.Text) ||
                string.IsNullOrWhiteSpace(Gad7Box.Text) ||
                string.IsNullOrWhiteSpace(Bdi2Box.Text) ||
                string.IsNullOrWhiteSpace(PCL5Total.Text) ||
                string.IsNullOrWhiteSpace(YBOCS.Text))
            {
                MessageBox.Show("Please fill in all fields.");
                return;
            }

            // Add to ComboBox if it's new
            if (!PatientSelector.Items.Contains(patientId))
                PatientSelector.Items.Add(patientId);

            PatientSelector.SelectedItem = patientId; // ✅ ensure it's selected


            var entry = new ScoreEntry
            {
                PatientId = patientId,
                PHQ9 = int.Parse(Phq9Box.Text),
                GAD7 = int.Parse(Gad7Box.Text),
                BDI2 = int.Parse(Bdi2Box.Text),
                PCL5 = int.Parse(PCL5Total.Text),
                YBOCS = int.Parse(YBOCS.Text)
            };

            if (!patientData.ContainsKey(patientId))
            {
                patientData[patientId] = new List<ScoreEntry>();
                PatientSelector.Items.Add(patientId);
            }

            patientData[patientId].Add(entry);
            UpdateChartForPatient(patientId);

            Phq9Box.Clear();
            Gad7Box.Clear();
            Bdi2Box.Clear();
            PCL5Total.Clear();
            YBOCS.Clear();
            PatientIdBox.Clear();
        }

        private void UpdateChartForPatient(string patientId)
        {
            if (!patientData.ContainsKey(patientId)) return;

            var scores = patientData[patientId];
            ScoresGrid.ItemsSource = scores;

            Phq9Values.Clear(); Gad7Values.Clear(); Bdi2Values.Clear(); Pcl5Values.Clear(); YbocsValues.Clear();
            TimeLabels.Clear();

            for (int i = 0; i < scores.Count; i++)
            {
                var s = scores[i];
                Phq9Values.Add(s.PHQ9);
                Gad7Values.Add(s.GAD7);
                Bdi2Values.Add(s.BDI2);
                Pcl5Values.Add(s.PCL5);
                YbocsValues.Add(s.YBOCS);
                TimeLabels.Add(i == 0 ? "PreTx" : i == 1 ? "Â½ way" : i == 2 ? "PostTx" : $"T{i + 1}");
            }

            progressChart.AxisX[0].Labels = TimeLabels;
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
            sb.AppendLine("PatientId,PHQ9,GAD7,BDI2,PCL5,YBOCS");
            foreach (var s in filtered)
            {
                sb.AppendLine($"{s.PatientId},{s.PHQ9},{s.GAD7},{s.BDI2},{s.PCL5},{s.YBOCS}");
            }

            var filePath = $"PatientScores_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            File.WriteAllText(filePath, sb.ToString());

            MessageBox.Show($"Exported to {filePath}", "Success");
        }
    }
}
