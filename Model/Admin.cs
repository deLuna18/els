using System.ComponentModel.DataAnnotations;

namespace SubdivisionManagement.Model
{
    public class Admin
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        // Constructor to ensure properties are initialized
        public Admin(string username, string passwordHash)
        {
            Username = username;
            PasswordHash = passwordHash;
        }
    }
}
