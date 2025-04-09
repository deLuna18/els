using System.ComponentModel.DataAnnotations;

namespace SubdivisionManagement.Model
{
    public class Homeowner
    {
        public int Id { get; set; }

        [Required]
        public string FirstName { get; set; } = null!;

        public string? MiddleName { get; set; }

        [Required]
        public string LastName { get; set; } = null!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string PasswordHash { get; set; } = null!;

         [Required]
        [Phone]
        public string PhoneNumber { get; set; } = null!;

        public string Phase { get; set; } = null!;

        public string Street { get; set; } = null!;

        public string Block { get; set; } = null!;

        public string HouseNumber { get; set; } = null!;

        public string EmergencyContactName { get; set; } = null!;

        public string EmergencyContactNumber { get; set; } = null!;

        [Required]
        public string Status { get; set; } = "pending"; // Set default value to "pending"

        public int? StaffId { get; set; }
        public Staff? Staff { get; set; }
    }
}
