using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PatientTrackerWPF.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Salt { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Display(Name = "Role")]
        public string Role { get; set; } = "User"; // Admin, Doctor, Technician, User

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Last Login")]
        [Column(TypeName = "datetime2")]
        public DateTime? LastLogin { get; set; }

        [Display(Name = "Failed Login Attempts")]
        public int FailedLoginAttempts { get; set; } = 0;

        [Display(Name = "Account Locked Until")]
        [Column(TypeName = "datetime2")]
        public DateTime? LockedUntil { get; set; }

        [Display(Name = "Password Reset Token")]
        [StringLength(100)]
        public string? PasswordResetToken { get; set; }

        [Display(Name = "Password Reset Expires")]
        [Column(TypeName = "datetime2")]
        public DateTime? PasswordResetExpires { get; set; }

        [Display(Name = "Created Date")]
        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Updated Date")]
        [Column(TypeName = "datetime2")]
        public DateTime? UpdatedAt { get; set; }

        [StringLength(50)]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [StringLength(50)]
        [Display(Name = "Updated By")]
        public string? UpdatedBy { get; set; }

        // Navigation property for audit trails
        public virtual ICollection<ScoreEntry>? ScoreEntriesCreated { get; set; } = new List<ScoreEntry>();
        public virtual ICollection<ScoreEntry>? ScoreEntriesUpdated { get; set; } = new List<ScoreEntry>();

        // Computed properties
        [NotMapped]
        public bool IsLocked => LockedUntil.HasValue && LockedUntil > DateTime.UtcNow;

        [NotMapped]
        public string DisplayName => $"{FullName} ({Username})";

        [NotMapped]
        public bool CanResetPassword => !string.IsNullOrEmpty(PasswordResetToken) &&
                                       PasswordResetExpires.HasValue &&
                                       PasswordResetExpires > DateTime.UtcNow;
    }

    // Enum for user roles
    public enum UserRole
    {
        Admin,
        Doctor,
        Technician,
        Researcher,
        User
    }
}