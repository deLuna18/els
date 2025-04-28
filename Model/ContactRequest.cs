using SubdivisionManagement.Model;

namespace SubdivisionManagement.Model
{
    public class ContactRequest
    {
        public int Id { get; set; }
        public int HomeownerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string QueryType { get; set; }
        public string Message { get; set; }
        public DateTime DateSubmitted { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Pending";
        
        // Navigation property
        public virtual Homeowner Homeowner { get; set; }
    }
}