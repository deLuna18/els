using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using SubdivisionManagement.Model;
using System.Linq;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using System.IO;
using System;
using Microsoft.EntityFrameworkCore;

namespace SubdivisionManagement.Controllers
{
    public class StaffController : Controller
    {
        private readonly HomeContext _context;
        private readonly IAntiforgery _antiforgery;

        public StaffController(HomeContext context, IAntiforgery antiforgery)
        {
            _context = context;
            _antiforgery = antiforgery;
        }

        // GET: Staff/Login (Displays the login page)
        public IActionResult Login()
        {
            return View();
        }

        // POST: Staff/Login (Handles the login logic)
        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            var staff = _context.Staffs.FirstOrDefault(s => s.Username == username);

            if (staff == null || !VerifyPassword(password, staff.PasswordHash, staff.PasswordSalt))
            {
                ViewBag.Error = "Invalid username or password";
                return View();
            }

            HttpContext.Session.SetString("StaffUser", username);
            return RedirectToAction("Dashboard");
        }

        public IActionResult Dashboard()
        {
            var username = HttpContext.Session.GetString("StaffUser");

            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login");
            }

            var staff = _context.Staffs.FirstOrDefault(s => s.Username == username);
            if (staff == null)
            {
                return RedirectToAction("Login");
            }

            // Get all homeowners (not just pending)
            var homeowners = _context.Homeowners.ToList();
            ViewBag.Homeowners = homeowners;

            // Get announcements and add to ViewBag
            var announcements = _context.Announcements
                .Include(a => a.Staff)
                .OrderByDescending(a => a.DateCreated)
                .ToList();
            ViewBag.Announcements = announcements;

            return View("staffdashboard", staff);
        }

        public IActionResult Staff_Services()
        {
            // Check if staff is logged in
            var username = HttpContext.Session.GetString("StaffUser");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login");
            }

            return View();
        }

        // Staff Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // Password verification logic
        private bool VerifyPassword(string password, string storedHash, string? storedSalt)
        {
            // Check if the storedSalt is null before proceeding
            if (storedSalt == null)
            {
                // If the salt is null, return false or handle accordingly
                return false;
            }

            // Convert the stored salt from base64 string to byte array
            byte[] salt = Convert.FromBase64String(storedSalt);

            // Log the salt being used (optional, for debugging)
            Console.WriteLine($"Salt: {Convert.ToBase64String(salt)}");

            // Compute the hash from the input password using the salt
            var computedHash = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));

            // Log the computed hash and stored hash
            Console.WriteLine($"Computed Hash: {computedHash}");
            Console.WriteLine($"Stored Hash: {storedHash}");

            // Compare the computed hash with the stored hash
            return computedHash == storedHash;
        }

        // GET: Staff/Register (Displays the registration page)
        public IActionResult Register()
        {
            return View();
        }

        // POST: Staff/Register (Handles the registration logic)
        [HttpPost]
        public async Task<IActionResult> Register(Staff staff)
        {
            if (staff == null)
            {
                return BadRequest("Invalid data.");
            }

            // Generate a random salt for password
            var salt = new byte[16];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Hash the password with the generated salt
            var hashedPassword = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: staff.PasswordHash,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));

            // Store the hashed password and the salt in the database
            staff.PasswordHash = hashedPassword;
            staff.PasswordSalt = Convert.ToBase64String(salt); // Save the salt as well

            // Add the staff to the database
            _context.Staffs.Add(staff);
            await _context.SaveChangesAsync();

            return RedirectToAction("Login");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateHomeownerStatus([FromBody] UpdateStatusModel model)
        {
            try
            {
                var homeowner = _context.Homeowners.Find(model.HomeownerId);
                if (homeowner == null)
                {
                    return Json(new { success = false, message = "Homeowner not found" });
                }

                // Get the current staff user
                var username = HttpContext.Session.GetString("StaffUser");
                var staff = _context.Staffs.FirstOrDefault(s => s.Username == username);
                
                if (staff == null)
                {
                    return Json(new { success = false, message = "Staff not found" });
                }

                try
                {
                    homeowner.Status = model.NewStatus;
                    homeowner.StaffId = staff.Id;  // Update to StaffId
                    _context.SaveChanges();

                    return Json(new { success = true, message = $"Status updated to {model.NewStatus}" });
                }
                catch (Exception ex)
                {
                    // Log the inner exception details
                    Console.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAnnouncement(IFormFile image, string type, string description)
        {
            try
            {
                var username = HttpContext.Session.GetString("StaffUser");
                var staff = _context.Staffs.FirstOrDefault(s => s.Username == username);
                
                if (staff == null)
                {
                    return Json(new { success = false, message = "Staff not found" });
                }

                var announcement = new Announcement
                {
                    Type = type,
                    Description = description,
                    StaffId = staff.Id
                };

                if (image != null && image.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "announcements");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + image.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(fileStream);
                    }

                    announcement.ImagePath = "/uploads/announcements/" + uniqueFileName;
                }

                _context.Announcements.Add(announcement);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Announcement added successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAnnouncement(int id, string type, string description, IFormFile? image)
        {
            try
            {
                var announcement = _context.Announcements.FirstOrDefault(a => a.Id == id);
                if (announcement == null)
                {
                    TempData["Error"] = "Announcement not found.";
                    return RedirectToAction("Announcements");
                }

                announcement.Type = type;
                announcement.Description = description;

                if (image != null && image.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "announcements");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + image.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(fileStream);
                    }

                    announcement.ImagePath = "/uploads/announcements/" + uniqueFileName;
                }

                _context.Announcements.Update(announcement);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Announcement updated successfully.";
                return RedirectToAction("Announcements"); // Redirect to the Announcements view
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred: {ex.Message}";
                return RedirectToAction("Announcements");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAnnouncement(int id)
        {
            try
            {
                var announcement = _context.Announcements.FirstOrDefault(a => a.Id == id);
                if (announcement == null)
                {
                    return Json(new { success = false, message = "Announcement not found." });
                }

                _context.Announcements.Remove(announcement);
                _context.SaveChanges();

                return Json(new { success = true, message = "Announcement deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        public IActionResult Announcements()
        {
            var announcements = _context.Announcements
                .Include(a => a.Staff)
                .OrderByDescending(a => a.DateCreated)
                .ToList();
            
            return View("staff_announcement", announcements);
        }

        public IActionResult Staff_Security_Visitors()
        {
            var username = HttpContext.Session.GetString("StaffUser");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login");
            }

            return View("staff_security_visitors");
        }

        public IActionResult Staff_Community_Forum()
        {
            var username = HttpContext.Session.GetString("StaffUser");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login");
            }

            return View("staff_community_forum");
        }
    }

    public class UpdateStatusModel
    {
        public int HomeownerId { get; set; }
        public string NewStatus { get; set; } = string.Empty;
    }
}
