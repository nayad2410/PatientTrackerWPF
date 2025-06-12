using PatientTrackerWPF.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PatientTrackerWPF.Services
{
    public class ClinicalMetricsService
    {
        public class PatientOutcome
        {
            public string PatientId { get; set; } = string.Empty;
            public int BaselineBDI2 { get; set; }
            public int MostRecentBDI2 { get; set; }
            public DateTime BaselineDate { get; set; }
            public DateTime MostRecentDate { get; set; }
            public double PercentImprovement { get; set; }
            public bool HasResponse { get; set; } // ≥50% improvement
            public bool HasRemission { get; set; } // Score <14 from baseline ≥14
            public int TotalAssessments { get; set; }
            public int DaysBetweenAssessments { get; set; }
        }

        public class ClinicalMetrics
        {
            public int TotalPatients { get; set; }
            public int PatientsWithMultipleAssessments { get; set; }
            public int ResponseCount { get; set; }
            public int RemissionCount { get; set; }
            public double ResponseRate { get; set; }
            public double RemissionRate { get; set; }
            public double AverageImprovement { get; set; }
            public List<PatientOutcome> PatientOutcomes { get; set; } = new();
            public DateTime CalculatedOn { get; set; } = DateTime.Now;
        }

        public ClinicalMetrics CalculateBDI2Metrics(Dictionary<string, List<ScoreEntry>> patientData)
        {
            var metrics = new ClinicalMetrics();
            var patientOutcomes = new List<PatientOutcome>();

            foreach (var kvp in patientData)
            {
                var patientId = kvp.Key;
                var entries = kvp.Value
                    .Where(e => e.BDI2 > 0) // Only entries with BDI-II scores
                    .OrderBy(e => e.Date)
                    .ToList();

                if (entries.Count < 2) continue; // Need at least 2 assessments

                var baseline = entries.First();
                var mostRecent = entries.Last();

                // Only include patients who started with BDI-II ≥ 14 (moderate depression or higher)
                if (baseline.BDI2 < 14) continue;

                var percentImprovement = ((double)(baseline.BDI2 - mostRecent.BDI2) / baseline.BDI2) * 100;
                var hasResponse = percentImprovement >= 50; // ≥50% improvement
                var hasRemission = mostRecent.BDI2 < 14; // Score drops below 14

                var outcome = new PatientOutcome
                {
                    PatientId = patientId,
                    BaselineBDI2 = baseline.BDI2,
                    MostRecentBDI2 = mostRecent.BDI2,
                    BaselineDate = baseline.Date,
                    MostRecentDate = mostRecent.Date,
                    PercentImprovement = percentImprovement,
                    HasResponse = hasResponse,
                    HasRemission = hasRemission,
                    TotalAssessments = entries.Count,
                    DaysBetweenAssessments = (mostRecent.Date - baseline.Date).Days
                };

                patientOutcomes.Add(outcome);
            }

            // Calculate overall metrics
            metrics.TotalPatients = patientData.Keys.Count;
            metrics.PatientsWithMultipleAssessments = patientOutcomes.Count;
            metrics.ResponseCount = patientOutcomes.Count(p => p.HasResponse);
            metrics.RemissionCount = patientOutcomes.Count(p => p.HasRemission);
            metrics.ResponseRate = patientOutcomes.Count > 0 ?
                (double)metrics.ResponseCount / patientOutcomes.Count * 100 : 0;
            metrics.RemissionRate = patientOutcomes.Count > 0 ?
                (double)metrics.RemissionCount / patientOutcomes.Count * 100 : 0;
            metrics.AverageImprovement = patientOutcomes.Count > 0 ?
                patientOutcomes.Average(p => p.PercentImprovement) : 0;
            metrics.PatientOutcomes = patientOutcomes;

            return metrics;
        }

        public string GenerateMetricsReport(ClinicalMetrics metrics)
        {
            var report = new System.Text.StringBuilder();

            report.AppendLine("BDI-II CLINICAL OUTCOMES REPORT");
            report.AppendLine("================================");
            report.AppendLine($"Generated: {metrics.CalculatedOn:yyyy-MM-dd HH:mm}");
            report.AppendLine();

            report.AppendLine("SUMMARY METRICS:");
            report.AppendLine($"Total Patients: {metrics.TotalPatients}");
            report.AppendLine($"Patients with Multiple Assessments: {metrics.PatientsWithMultipleAssessments}");
            report.AppendLine($"Response Rate (≥50% improvement): {metrics.ResponseRate:F1}% ({metrics.ResponseCount}/{metrics.PatientsWithMultipleAssessments})");
            report.AppendLine($"Remission Rate (score <14): {metrics.RemissionRate:F1}% ({metrics.RemissionCount}/{metrics.PatientsWithMultipleAssessments})");
            report.AppendLine($"Average Improvement: {metrics.AverageImprovement:F1}%");
            report.AppendLine();

            report.AppendLine("PATIENT DETAILS:");
            report.AppendLine("Patient ID\tBaseline\tMost Recent\tImprovement\tResponse\tRemission\tDays");

            foreach (var outcome in metrics.PatientOutcomes.OrderByDescending(p => p.PercentImprovement))
            {
                report.AppendLine($"{outcome.PatientId}\t" +
                                $"{outcome.BaselineBDI2}\t" +
                                $"{outcome.MostRecentBDI2}\t" +
                                $"{outcome.PercentImprovement:F1}%\t" +
                                $"{(outcome.HasResponse ? "Yes" : "No")}\t" +
                                $"{(outcome.HasRemission ? "Yes" : "No")}\t" +
                                $"{outcome.DaysBetweenAssessments}");
            }

            return report.ToString();
        }

        // Method to get metrics for a specific patient
        public PatientOutcome? GetPatientOutcome(string patientId, List<ScoreEntry> entries)
        {
            var bdi2Entries = entries
                .Where(e => e.BDI2 > 0)
                .OrderBy(e => e.Date)
                .ToList();

            if (bdi2Entries.Count < 2) return null;

            var baseline = bdi2Entries.First();
            var mostRecent = bdi2Entries.Last();

            if (baseline.BDI2 < 14) return null; // Must start with moderate depression or higher

            var percentImprovement = ((double)(baseline.BDI2 - mostRecent.BDI2) / baseline.BDI2) * 100;

            return new PatientOutcome
            {
                PatientId = patientId,
                BaselineBDI2 = baseline.BDI2,
                MostRecentBDI2 = mostRecent.BDI2,
                BaselineDate = baseline.Date,
                MostRecentDate = mostRecent.Date,
                PercentImprovement = percentImprovement,
                HasResponse = percentImprovement >= 50,
                HasRemission = mostRecent.BDI2 < 14,
                TotalAssessments = bdi2Entries.Count,
                DaysBetweenAssessments = (mostRecent.Date - baseline.Date).Days
            };
        }
    }
}