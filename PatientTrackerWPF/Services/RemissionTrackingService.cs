using PatientTrackerWPF.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PatientTrackerWPF.Services
{
    public class RemissionTrackingService
    {
        public class RemissionPeriod
        {
            public string PatientId { get; set; } = string.Empty;
            public DateTime RemissionStartDate { get; set; }
            public DateTime? RemissionEndDate { get; set; } // null if still in remission
            public int DaysInRemission { get; set; }
            public bool IsCurrentlyInRemission { get; set; }
            public int ScoreAtRemissionStart { get; set; }
            public int? ScoreAtRemissionEnd { get; set; }
            public string RemissionStatus { get; set; } = string.Empty; // "Current", "Lost", "Maintained"
        }

        public class AllTimeRemissionAnalysis
        {
            public int TotalEligiblePatients { get; set; }
            public int PatientsWhoEverReachedRemission { get; set; }
            public double AllTimeRemissionRate { get; set; }
            public int CurrentlyInRemission { get; set; }
            public int LostRemission { get; set; }
            public List<RemissionPeriod> AllRemissionPeriods { get; set; } = new();
            public double AverageDaysToRemission { get; set; }
            public double AverageRemissionDuration { get; set; }
        }

        public AllTimeRemissionAnalysis AnalyzeAllTimeRemissions(Dictionary<string, List<ScoreEntry>> patientData)
        {
            var analysis = new AllTimeRemissionAnalysis();
            var allRemissionPeriods = new List<RemissionPeriod>();

            foreach (var kvp in patientData)
            {
                var patientId = kvp.Key;
                var entries = kvp.Value
                    .Where(e => e.BDI2.HasValue && e.BDI2.Value >= 0) // FIXED: Handle nullable
                    .OrderBy(e => e.Date)
                    .ToList();

                if (entries.Count < 2) continue; // Need at least 2 assessments

                var baseline = entries.First();
                if (baseline.BDI2.Value < 14) continue; // FIXED: Use .Value

                // Track remission periods for this patient
                var remissionPeriods = FindRemissionPeriods(patientId, entries);
                allRemissionPeriods.AddRange(remissionPeriods);
            }

            // Calculate overall statistics
            var eligiblePatients = patientData.Values
                .Where(entries => entries.Where(e => e.BDI2.HasValue && e.BDI2.Value >= 0).Count() >= 2) // FIXED: Handle nullable
                .Where(entries => entries.Where(e => e.BDI2.HasValue && e.BDI2.Value >= 0).OrderBy(e => e.Date).First().BDI2.Value >= 14) // FIXED: Handle nullable
                .Count();

            var patientsWithRemission = allRemissionPeriods
                .Select(r => r.PatientId)
                .Distinct()
                .Count();

            analysis.TotalEligiblePatients = eligiblePatients;
            analysis.PatientsWhoEverReachedRemission = patientsWithRemission;
            analysis.AllTimeRemissionRate = eligiblePatients > 0 ?
                (double)patientsWithRemission / eligiblePatients * 100 : 0;
            analysis.CurrentlyInRemission = allRemissionPeriods.Count(r => r.IsCurrentlyInRemission);
            analysis.LostRemission = allRemissionPeriods.Count(r => !r.IsCurrentlyInRemission && r.RemissionEndDate.HasValue);
            analysis.AllRemissionPeriods = allRemissionPeriods;

            // Calculate averages
            var completedRemissions = allRemissionPeriods.Where(r => r.RemissionEndDate.HasValue).ToList();
            analysis.AverageRemissionDuration = completedRemissions.Any() ?
                completedRemissions.Average(r => r.DaysInRemission) : 0;

            return analysis;
        }

        private List<RemissionPeriod> FindRemissionPeriods(string patientId, List<ScoreEntry> entries)
        {
            var remissionPeriods = new List<RemissionPeriod>();
            bool inRemission = false;
            RemissionPeriod? currentPeriod = null;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                bool isRemissionScore = entry.BDI2.Value < 14; // FIXED: Use .Value

                if (!inRemission && isRemissionScore)
                {
                    // Entering remission
                    currentPeriod = new RemissionPeriod
                    {
                        PatientId = patientId,
                        RemissionStartDate = entry.Date,
                        ScoreAtRemissionStart = entry.BDI2.Value, // FIXED: Use .Value
                        IsCurrentlyInRemission = true,
                        RemissionStatus = "Current"
                    };
                    inRemission = true;
                }
                else if (inRemission && !isRemissionScore)
                {
                    // Losing remission
                    if (currentPeriod != null)
                    {
                        currentPeriod.RemissionEndDate = entry.Date;
                        currentPeriod.ScoreAtRemissionEnd = entry.BDI2.Value; // FIXED: Use .Value
                        currentPeriod.DaysInRemission = (entry.Date - currentPeriod.RemissionStartDate).Days;
                        currentPeriod.IsCurrentlyInRemission = false;
                        currentPeriod.RemissionStatus = "Lost";

                        remissionPeriods.Add(currentPeriod);
                    }
                    inRemission = false;
                    currentPeriod = null;
                }
            }

            // Handle case where patient is still in remission
            if (inRemission && currentPeriod != null)
            {
                var lastEntry = entries.Last();
                currentPeriod.DaysInRemission = (lastEntry.Date - currentPeriod.RemissionStartDate).Days;
                currentPeriod.RemissionStatus = "Current";
                remissionPeriods.Add(currentPeriod);
            }

            return remissionPeriods;
        }

        public string GenerateRemissionReport(AllTimeRemissionAnalysis analysis)
        {
            var report = new System.Text.StringBuilder();

            report.AppendLine("ALL-TIME REMISSION ANALYSIS REPORT");
            report.AppendLine("==================================");
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
            report.AppendLine();

            report.AppendLine("SUMMARY METRICS:");
            report.AppendLine($"Total Eligible Patients          : {analysis.TotalEligiblePatients}");
            report.AppendLine($"Patients Who Ever Reached Remission: {analysis.PatientsWhoEverReachedRemission}");
            report.AppendLine($"All-Time Remission Rate          : {analysis.AllTimeRemissionRate:F1}%");
            report.AppendLine($"Currently in Remission           : {analysis.CurrentlyInRemission}");
            report.AppendLine($"Lost Remission                   : {analysis.LostRemission}");
            report.AppendLine($"Average Remission Duration       : {analysis.AverageRemissionDuration:F1} days");
            report.AppendLine();

            report.AppendLine("REMISSION PERIODS DETAIL:");
            report.AppendLine(string.Format("{0,-12} {1,-12} {2,-12} {3,-6} {4,-10} {5,-12} {6,-10}",
                                            "Patient ID", "Start Date", "End Date", "Days", "Status", "Start Score", "End Score"));

            foreach (var period in analysis.AllRemissionPeriods
                                           .OrderBy(p => p.PatientId)
                                           .ThenBy(p => p.RemissionStartDate))
            {
                var endDateStr = period.RemissionEndDate?.ToString("yyyy-MM-dd") ?? "Ongoing";
                var endScoreStr = period.ScoreAtRemissionEnd?.ToString() ?? "N/A";

                report.AppendLine(string.Format("{0,-12} {1,-12} {2,-12} {3,-6} {4,-10} {5,-12} {6,-10}",
                                                period.PatientId,
                                                period.RemissionStartDate.ToString("yyyy-MM-dd"),
                                                endDateStr,
                                                period.DaysInRemission,
                                                period.RemissionStatus,
                                                period.ScoreAtRemissionStart,
                                                endScoreStr));
            }

            return report.ToString();
        }

    }
}