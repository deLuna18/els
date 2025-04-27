using Microsoft.EntityFrameworkCore;

namespace SubdivisionManagement.Model
{
    public class HomeContext : DbContext
    {
        public HomeContext(DbContextOptions<HomeContext> options) : base(options) { }

        public DbSet<Homeowner> Homeowners { get; set; } = null!;
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Staff> Staffs { get; set; }  
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<ServiceRequest> ServiceRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships if needed, e.g., cascade deletes
            modelBuilder.Entity<ServiceRequest>()
                .HasOne(sr => sr.Homeowner)
                .WithMany() // Assuming Homeowner doesn't have a collection of ServiceRequests navigation property
                .HasForeignKey(sr => sr.HomeownerId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting homeowner if they have requests

            modelBuilder.Entity<ServiceRequest>()
                .HasOne(sr => sr.Staff)
                .WithMany() // Assuming Staff doesn't have a collection of ServiceRequests
                .HasForeignKey(sr => sr.StaffId)
                .OnDelete(DeleteBehavior.SetNull); // Set StaffId to null if staff is deleted
        }
    }
}

