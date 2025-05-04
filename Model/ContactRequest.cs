using SubdivisionManagement.Model;

namespace SubdivisionManagement.Model
{
    public class ContactRequest
    {
        public int Id { get; set; }
        public int HomeownerId { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
        public required string QueryType { get; set; }
        public required string Message { get; set; }
        public DateTime DateSubmitted { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Pending";
        
        // Navigation property
        public required virtual Homeowner Homeowner { get; set; }
    }
}