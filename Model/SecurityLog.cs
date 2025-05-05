using System;
using System.ComponentModel.DataAnnotations;

namespace SubdivisionManagement.Model
{
    public class SecurityLog
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Type { get; set; } = string.Empty;
        
        [Required]
        public string Message { get; set; } = string.Empty;
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public int? StaffId { get; set; }
        public Staff? Staff { get; set; }
        
        [Required]
        public string Status { get; set; } = "Pending";
        
        public int? HandledBy { get; set; }
        public DateTime? HandledAt { get; set; }
        public string? Resolution { get; set; }
    }
} 