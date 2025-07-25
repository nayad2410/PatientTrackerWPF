using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel.DataAnnotations;

namespace PatientTrackerWPF.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } // VIEW, CREATE, UPDATE, DELETE, LOGIN, LOGOUT

        [MaxLength(100)]
        public string? PatientId { get; set; }

        [MaxLength(45)]
        public string? IPAddress { get; set; }

        [MaxLength(500)]
        public string? Details { get; set; }

        public User User { get; set; } = null!;
    }
}
