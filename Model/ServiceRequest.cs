using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SubdivisionManagement.Model
{
    public class ServiceRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int HomeownerId { get; set; } // Foreign key to Homeowner

        [ForeignKey("HomeownerId")]
        public virtual Homeowner? Homeowner { get; set; }

        public int? StaffId { get; set; } // Foreign key to Staff (nullable)

        [ForeignKey("StaffId")]
        public virtual Staff? Staff { get; set; }

        [Required]
        [MaxLength(100)]
        public string ServiceType { get; set; } = string.Empty; // e.g., Maintenance, Security, Landscaping

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Priority { get; set; } = string.Empty; // e.g., Low, Medium, High, Urgent

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // e.g., Pending, Accepted, Rejected, In-Progress, Completed

        [Required]
        public DateTime DateSubmitted { get; set; } = DateTime.UtcNow;

        public DateTime? DateAccepted { get; set; }
        public DateTime? DateStarted { get; set; }
        public DateTime? DateCompleted { get; set; }

        public string? StaffNotes { get; set; } // Notes added by staff
    }
} 