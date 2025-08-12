using PatientTrackerWPF.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PatientTrackerWPF.Services
{
    /// <summary>
    /// Calculates cohort-level and patient-level outcome metrics
    ///              – PHQ-9 response  (≥ 50 % improvement)
    ///              – BDI-II remission (score ≤ 14)
    /// and whether either outcome was ever achieved.
    /// </summary>
    public class ClinicalMetricsService
    {
        // ──────────────────────────────────────────────────────────────────────────
        //  INNER MODELS
        // ──────────────────────────────────────────────────────────────────────────

        #region Patient-level DTO
        public sealed class PatientOutcome
        {
            public string PatientId { get; set; } = "";

            // ---- PHQ-9 -----------------------------------------------------------
            public int? BaselinePHQ9 { get; set; }
            public int? MostRecentPHQ9 { get; set; }
            public DateTime PHQ9BaselineDate { get; set; }
            public DateTime PHQ9MostRecentDate { get; set; }
            public double PHQ9PercentImprovement { get; set; }
            public bool HasResponse { get; set; }     // current
            public bool EverAchievedResponse { get; set; }     // historical
            public DateTime? FirstResponseDate { get; set; }
            public double BestPHQ9Improvement { get; set; }

            // ---- BDI-II ----------------------------------------------------------
            public int? BaselineBDI2 { get; set; }
            public int? MostRecentBDI2 { get; set; }
            public DateTime BDI2BaselineDate { get; set; }
            public DateTime BDI2MostRecentDate { get; set; }
            public double BDI2PercentImprovement { get; set; }
            public bool HasRemission { get; set; }     // current
            public bool EverAchievedRemission { get; set; }     // historical
            public DateTime? FirstRemissionDate { get; set; }
            public int? LowestBDI2Score { get; set; }

            // ---- misc ------------------------------------------------------------
            public int TotalAssessments { get; set; }
            public int DaysBetweenAssessments { get; set; }

            // helpers
            public DateTime BaselineDate => PHQ9BaselineDate  != default ? PHQ9BaselineDate : BDI2BaselineDate;
            public DateTime MostRecentDate => PHQ9MostRecentDate!= default ? PHQ9MostRecentDate : BDI2MostRecentDate;
        }
        #endregion

        #region Cohort-level DTO
        public sealed class ClinicalMetrics
        {
            public int TotalPatients { get; set; }
            public int PatientsWithMultipleAssessments { get; set; }

            // PHQ-9 – current & ever
            public int EligibleForResponse { get; set; }
            public int ResponseCount { get; set; }
            public double ResponseRate { get; set; }
            public int EverAchievedResponseCount { get; set; }
            public double EverAchievedResponseRate { get; set; }

            // BDI-II – current & ever
            public int EligibleForRemission { get; set; }
            public int RemissionCount { get; set; }
            public double RemissionRate { get; set; }
            public int EverAchievedRemissionCount { get; set; }
            public double EverAchievedRemissionRate { get; set; }

            // Misc
            public double AverageImprovement { get; set; }
            public List<PatientOutcome> PatientOutcomes { get; set; } = new();
            public DateTime CalculatedOn { get; set; } = DateTime.Now;
        }
        #endregion

        // ──────────────────────────────────────────────────────────────────────────
        //  PUBLIC API
        // ──────────────────────────────────────────────────────────────────────────

        public ClinicalMetrics CalculateCombinedMetrics(
            Dictionary<string, List<ScoreEntry>> patientData)
        {
            var metrics = new ClinicalMetrics();
            var responseOutcomes = new List<PatientOutcome>();
            var remissionOutcomes = new List<PatientOutcome>();

            // ----- per-patient ----------------------------------------------------
            foreach (var (patientId, rawEntries) in patientData)
            {
                var entries = rawEntries
                              .Where(e => e.Date != default)
                              .OrderBy(e => e.Date)
                              .ToList();

                if (entries.Count < 2) continue;   // need ≥ 2 assessments

                var outcome = new PatientOutcome
                {
                    PatientId        = patientId,
                    TotalAssessments = entries.Count
                };

                CalcPhq9(outcome, entries, responseOutcomes);
                CalcBdi2(outcome, entries, remissionOutcomes);
                CalcDuration(outcome);

                metrics.PatientOutcomes.Add(outcome);
            }

            // ----- cohort roll-ups ----------------------------------------------
            metrics.TotalPatients                   = patientData.Count;
            metrics.PatientsWithMultipleAssessments = metrics.PatientOutcomes.Count;

            // PHQ-9
            metrics.EligibleForResponse         = responseOutcomes.Count;
            metrics.ResponseCount               = responseOutcomes.Count(o => o.HasResponse);
            metrics.EverAchievedResponseCount   = responseOutcomes.Count(o => o.EverAchievedResponse);
            metrics.ResponseRate                = Rate(metrics.ResponseCount,
                                                      metrics.EligibleForResponse);
            metrics.EverAchievedResponseRate    = Rate(metrics.EverAchievedResponseCount,
                                                      metrics.EligibleForResponse);

            // BDI-II
            metrics.EligibleForRemission        = remissionOutcomes.Count;
            metrics.RemissionCount              = remissionOutcomes.Count(o => o.HasRemission);
            metrics.EverAchievedRemissionCount  = remissionOutcomes.Count(o => o.EverAchievedRemission);
            metrics.RemissionRate               = Rate(metrics.RemissionCount,
                                                      metrics.EligibleForRemission);
            metrics.EverAchievedRemissionRate   = Rate(metrics.EverAchievedRemissionCount,
                                                      metrics.EligibleForRemission);

            // Average % improvement (PHQ-9)
            metrics.AverageImprovement = responseOutcomes
                                         .Select(o => o.PHQ9PercentImprovement)
                                         .DefaultIfEmpty(0)
                                         .Average();

            return metrics;
        }

        // Back-compat wrapper
        public ClinicalMetrics CalculateBDI2Metrics(Dictionary<string, List<ScoreEntry>> d)
            => CalculateCombinedMetrics(d);

        // ------------------------------------------------------------------------

        public string GenerateMetricsReport(ClinicalMetrics m)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=============================================================");
            sb.AppendLine("           RECONNECT CLINICAL OUTCOMES REPORT");
            sb.AppendLine("=============================================================");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            sb.AppendLine("SUMMARY");
            sb.AppendLine("─────────────────────────────────────────────────────────");
            sb.AppendLine($"Total Patients                 : {m.TotalPatients}");
            sb.AppendLine($"Patients ≥2 Assessments        : {m.PatientsWithMultipleAssessments}");
            sb.AppendLine();

            sb.AppendLine("PHQ-9 RESPONSE (≥50 % Δ)");
            sb.AppendLine("─────────────────────────────────────────────────────────");
            sb.AppendLine($"Eligible patients              : {m.EligibleForResponse}");
            sb.AppendLine($"Current response               : {m.ResponseCount}  ({m.ResponseRate:F1} %)");
            sb.AppendLine($"Ever achieved response         : {m.EverAchievedResponseCount}  ({m.EverAchievedResponseRate:F1} %)");
            sb.AppendLine($"Average improvement            : {m.AverageImprovement:F1} %");
            sb.AppendLine();

            sb.AppendLine("BDI-II REMISSION (≤14)");
            sb.AppendLine("─────────────────────────────────────────────────────────");
            sb.AppendLine($"Eligible patients              : {m.EligibleForRemission}");
            sb.AppendLine($"Current remission              : {m.RemissionCount}  ({m.RemissionRate:F1} %)");
            sb.AppendLine($"Ever achieved remission        : {m.EverAchievedRemissionCount}  ({m.EverAchievedRemissionRate:F1} %)");
            sb.AppendLine();

            sb.AppendLine("INDIVIDUAL OUTCOMES");
            sb.AppendLine("─────────────────────────────────────────────────────────");
            sb.AppendLine("PtID | PHQ-9 Resp | BDI-II Rem | Best Δ% | Lowest BDI-II");
            sb.AppendLine("─────────────────────────────────────────────────────────");

            foreach (var o in m.PatientOutcomes.OrderBy(o => o.PatientId))
            {
                var resp = o.BaselinePHQ9.HasValue ? (o.HasResponse ? "YES" : "NO ") : "N/A";
                var rem = o.MostRecentBDI2.HasValue ? (o.HasRemission ? "YES" : "NO ") : "N/A";
                var best = o.BaselinePHQ9.HasValue ? $"{o.BestPHQ9Improvement,6:F1}" : "  N/A ";
                var lowest = o.LowestBDI2Score?.ToString() ?? "N/A";

                sb.AppendLine($"{o.PatientId,-4} | {resp,-9} | {rem,-9} | {best} | {lowest,6}");
            }

            sb.AppendLine("=============================================================");
            return sb.ToString();
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  PRIVATE HELPERS
        // ──────────────────────────────────────────────────────────────────────────

        private static void CalcPhq9(PatientOutcome o, List<ScoreEntry> entries,
                                     ICollection<PatientOutcome> bucket)
        {
            var phq = entries.Where(e => e.PHQ9.HasValue).ToList();
            if (phq.Count < 2) return;

            var baseline = phq.First();
            var recent = phq.Last();

            o.BaselinePHQ9       = baseline.PHQ9.Value;
            o.MostRecentPHQ9     = recent.PHQ9.Value;
            o.PHQ9BaselineDate   = baseline.Date;
            o.PHQ9MostRecentDate = recent.Date;

            if (o.BaselinePHQ9 > 0)
            {
                o.PHQ9PercentImprovement =
                    100.0 * (o.BaselinePHQ9.Value - o.MostRecentPHQ9.Value) / o.BaselinePHQ9.Value;
                o.HasResponse = o.PHQ9PercentImprovement >= 50.0;
            }

            // ever-achieved
            double best = 0;
            DateTime? first = null;
            foreach (var e in phq.Skip(1))
            {
                var imp = 100.0 * (baseline.PHQ9.Value - e.PHQ9.Value) / baseline.PHQ9.Value;
                if (imp > best) best = imp;
                if (!first.HasValue && imp >= 50.0) first = e.Date;
            }

            o.BestPHQ9Improvement  = best;
            o.EverAchievedResponse = first.HasValue;
            o.FirstResponseDate    = first;

            bucket.Add(o);
        }

        private static void CalcBdi2(PatientOutcome o, List<ScoreEntry> entries,
                                     ICollection<PatientOutcome> bucket)
        {
            var bdi = entries.Where(e => e.BDI2.HasValue).ToList();
            if (bdi.Count < 2) return;

            var baseline = bdi.First();
            var recent = bdi.Last();

            o.BaselineBDI2       = baseline.BDI2.Value;
            o.MostRecentBDI2     = recent.BDI2.Value;
            o.BDI2BaselineDate   = baseline.Date;
            o.BDI2MostRecentDate = recent.Date;

            if (o.BaselineBDI2 > 0)
            {
                o.BDI2PercentImprovement =
                    100.0 * (o.BaselineBDI2.Value - o.MostRecentBDI2.Value) / o.BaselineBDI2.Value;
            }

            o.HasRemission = o.MostRecentBDI2 <= 14;

            var firstRem = bdi.FirstOrDefault(e => e.BDI2 <= 14);
            o.EverAchievedRemission = firstRem != null;
            o.FirstRemissionDate    = firstRem?.Date;
            o.LowestBDI2Score       = bdi.Min(e => e.BDI2.Value);

            bucket.Add(o);
        }

        private static void CalcDuration(PatientOutcome o)
        {
            if (o.PHQ9BaselineDate != default && o.PHQ9MostRecentDate != default)
                o.DaysBetweenAssessments =
                    (o.PHQ9MostRecentDate - o.PHQ9BaselineDate).Days;
            else if (o.BDI2BaselineDate != default && o.BDI2MostRecentDate != default)
                o.DaysBetweenAssessments =
                    (o.BDI2MostRecentDate - o.BDI2BaselineDate).Days;
        }

        private static double Rate(int n, int d) => d == 0 ? 0 : 100.0 * n / d;
    }
}
