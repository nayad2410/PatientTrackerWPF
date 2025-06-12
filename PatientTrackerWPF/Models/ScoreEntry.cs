using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PatientTrackerWPF.Models
{
    public class ScoreEntry
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Patient ID")]
        public string PatientId { get; set; } = string.Empty;

        [Range(0, 100)]
        [Display(Name = "PHQ-9 Score")]
        public int PHQ9 { get; set; }

        [Range(0, 100)]
        [Display(Name = "GAD-7 Score")]
        public int GAD7 { get; set; }

        [Range(0, 100)]
        [Display(Name = "PCL-5 Score")]
        public int PCL5 { get; set; }

        [Range(0, 100)]
        [Display(Name = "BDI-II Score")]
        public int BDI2 { get; set; }

        [Range(0, 100)]
        [Display(Name = "Y-BOCS Score")]
        public int YBOCS { get; set; }

        [StringLength(2000)]
        [Display(Name = "Treatment Notes")]
        public string Note { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Assessment Date")]
        [Column(TypeName = "datetime2")]
        public DateTime Date { get; set; } = DateTime.Today;

        // Audit fields for tracking changes
        [Display(Name = "Created Date")]
        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Updated Date")]
        [Column(TypeName = "datetime2")]
        public DateTime? UpdatedAt { get; set; }

        [StringLength(100)]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [StringLength(100)]
        [Display(Name = "Updated By")]
        public string? UpdatedBy { get; set; }

        // Computed properties for easier analysis
        [NotMapped]
        public bool HasAnyScores => PHQ9 > 0 || GAD7 > 0 || PCL5 > 0 || BDI2 > 0 || YBOCS > 0;

        [NotMapped]
        public bool IsNotesOnly => !HasAnyScores && !string.IsNullOrWhiteSpace(Note);

        [NotMapped]
        public string DisplayText => $"{PatientId} - {Date:yyyy-MM-dd} - {(IsNotesOnly ? "Notes Only" : "Assessment")}";

        // Helper method to get severity level based on PHQ-9 score
        [NotMapped]
        public string PHQ9Severity
        {
            get
            {
                return PHQ9 switch
                {
                    0 => "None",
                    >= 1 and <= 4 => "Minimal",
                    >= 5 and <= 9 => "Mild",
                    >= 10 and <= 14 => "Moderate",
                    >= 15 and <= 19 => "Moderately Severe",
                    >= 20 => "Severe",
                    _ => "Invalid"
                };
            }
        }

        // Helper method to get severity level based on GAD-7 score
        [NotMapped]
        public string GAD7Severity
        {
            get
            {
                return GAD7 switch
                {
                    0 => "None",
                    >= 1 and <= 4 => "Minimal",
                    >= 5 and <= 9 => "Mild",
                    >= 10 and <= 14 => "Moderate",
                    >= 15 => "Severe",
                    _ => "Invalid"
                };
            }
        }
    }
}