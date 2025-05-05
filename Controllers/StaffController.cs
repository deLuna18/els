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
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace SubdivisionManagement.Controllers
{
    public class StaffController : Controller
    {
        private readonly HomeContext _context;
        private readonly IAntiforgery _antiforgery;
        private readonly ILogger<StaffController> _logger;

        public StaffController(HomeContext context, IAntiforgery antiforgery, ILogger<StaffController> logger)
        {
            _context = context;
            _antiforgery = antiforgery;
            _logger = logger;
        }

        // Helper to check if staff is logged in
        private bool IsStaffLoggedIn(out string? username)
        {
            username = HttpContext.Session.GetString("StaffUser");
            return !string.IsNullOrEmpty(username);
        }

        // Helper to get logged in staff user
        private Staff? GetLoggedInStaffUser(string? username)
        {
            if (string.IsNullOrEmpty(username))
                return null;
            return _context.Staffs.FirstOrDefault(s => s.Username == username);
        }

        // GET: Staff/Login (Displays the login page)
        public IActionResult Login()
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            ViewBag.AntiForgeryToken = tokens.RequestToken;
            return View();
        }

        // POST: Staff/Login (Handles the login logic)
        [HttpPost]
        [ValidateAntiForgeryToken]
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
            if (!IsStaffLoggedIn(out var username))
            {
                return RedirectToAction("Login");
            }

            var staff = GetLoggedInStaffUser(username);
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

        [HttpGet]
        public IActionResult Staff_Services()
        {
            if (!IsStaffLoggedIn(out _))
            {
                return RedirectToAction("Login");
            }
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            ViewBag.AntiForgeryToken = tokens.RequestToken;
            return View();
        }

        // GET: /Staff/GetServiceRequests
        [HttpGet]
        public async Task<IActionResult> GetServiceRequests()
        {
            if (!IsStaffLoggedIn(out _)) return Unauthorized();

            try 
            {
                var rawRequests = await _context.ServiceRequests
                    .Include(r => r.Homeowner)
                    .OrderByDescending(r => r.DateSubmitted)
                    .ToListAsync(); 

                var projectedRequests = rawRequests.Select(r => new 
                {
                    r.Id,
                    RequestId = $"SR-{r.Id:D3}",
                    HomeownerName = (r.Homeowner != null) ? $"{r.Homeowner.FirstName} {r.Homeowner.LastName}" : "N/A",
                    r.HomeownerId,
                    r.ServiceType,
                    r.Priority,
                    r.Status,
                    DateSubmitted = r.DateSubmitted.ToString("o"),
                    r.Description,
                    DateCompleted = r.DateCompleted.HasValue ? r.DateCompleted.Value.ToString("o") : null,
                    CompletionTime = CalculateCompletionTime(r.DateAccepted, r.DateCompleted),
                    r.StaffNotes,
                    r.StaffId,
                    DateAccepted = r.DateAccepted?.ToString("o"),
                    DateStarted = r.DateStarted?.ToString("o")
                }).ToList(); 

                return Json(projectedRequests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching service requests in GetServiceRequests action.");
                return StatusCode(500, new { message = "An internal error occurred while retrieving service requests." }); 
            }
        }

        // GET: /Staff/GetServiceEmployees
        [HttpGet]
        public async Task<IActionResult> GetServiceEmployees()
        {
            if (!IsStaffLoggedIn(out _)) return Unauthorized();

            try
            {
                var employees = await _context.Staffs
                    .Where(s => s.Role == "ServiceEmployee")
                    .Select(s => new
                    {
                        s.Id,
                        Name = s.FullName,
                        s.Email,
                        Phone = s.ContactNumber,
                        s.Status,
                        s.Specialization
                    })
                    .ToListAsync();

                return Json(employees);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching service employees");
                return StatusCode(500, new { message = "Error fetching service employees" });
            }
        }

        // POST: /Staff/UpdateServiceRequestStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateServiceRequestStatus([FromBody] UpdateServiceRequestDto updateDto)
        {
            if (!IsStaffLoggedIn(out var username)) return Unauthorized();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var staff = GetLoggedInStaffUser(username);
            if (staff == null) return Forbid();

            var request = await _context.ServiceRequests.FindAsync(updateDto.RequestId);
            if (request == null)
            {
                return NotFound(new { success = false, message = "Service request not found." });
            }

            if (!IsValidStatusTransition(request.Status, updateDto.NewStatus))
            {
                return BadRequest(new { success = false, message = $"Invalid status transition from {request.Status} to {updateDto.NewStatus}." });
            }

            // Update assigned employee if provided
            if (updateDto.AssignedEmployeeId.HasValue)
            {
                var employee = await _context.Staffs.FindAsync(updateDto.AssignedEmployeeId.Value);
                if (employee == null || employee.Role != "ServiceEmployee")
                {
                    return BadRequest(new { success = false, message = "Invalid employee assignment." });
                }
                request.StaffId = employee.Id;
            }

            request.Status = updateDto.NewStatus;
            request.StaffNotes = updateDto.StaffNotes ?? request.StaffNotes;

            switch (updateDto.NewStatus.ToLower())
            {
                case "accepted":
                    request.DateAccepted = DateTime.UtcNow;
                    break;
                case "in-progress":
                    if (request.DateAccepted == null) request.DateAccepted = DateTime.UtcNow;
                    request.DateStarted = DateTime.UtcNow;
                    break;
                case "completed":
                    if (request.DateAccepted == null) request.DateAccepted = DateTime.UtcNow;
                    if (request.DateStarted == null) request.DateStarted = DateTime.UtcNow;
                    request.DateCompleted = DateTime.UtcNow;
                    break;
                case "rejected":
                    request.DateAccepted = null;
                    request.DateStarted = null;
                    request.DateCompleted = null;
                    break;
            }

            try
            {
                _context.ServiceRequests.Update(request);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Request status updated to {updateDto.NewStatus}." });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { success = false, message = "The request was modified by another user. Please refresh and try again." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating service request status for ID {RequestId}", updateDto.RequestId);
                return StatusCode(500, new { success = false, message = "An internal error occurred while updating the status." });
            }
        }

        // POST: /Staff/UpdateStaffNotes
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStaffNotes([FromBody] UpdateStaffNotesDto notesDto)
        {
             if (!IsStaffLoggedIn(out var username)) return Unauthorized();
             if (!ModelState.IsValid) return BadRequest(ModelState);

             var staff = GetLoggedInStaffUser(username);
             if (staff == null) return Forbid();

             var request = await _context.ServiceRequests.FindAsync(notesDto.RequestId);
             if (request == null)
             {
                 return NotFound(new { success = false, message = "Service request not found." });
             }

             
             request.StaffNotes = notesDto.StaffNotes;
             request.StaffId = staff.Id; 
             try
             {
                 _context.ServiceRequests.Update(request);
                 await _context.SaveChangesAsync();
                 return Json(new { success = true, message = "Staff notes updated successfully." });
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Error updating staff notes for ID {RequestId}", notesDto.RequestId);
                 return StatusCode(500, new { success = false, message = "An internal error occurred while updating notes." });
             }
        }

        private bool IsValidStatusTransition(string currentStatus, string newStatus)
        {
            currentStatus = currentStatus.ToLower();
            newStatus = newStatus.ToLower();

            if (currentStatus == newStatus) return true; 
            if (currentStatus == "completed" || currentStatus == "rejected") return false; 

            switch (currentStatus)
            {
                case "pending":
                    return newStatus == "accepted" || newStatus == "rejected";
                case "accepted":
                    return newStatus == "in-progress" || newStatus == "completed" || newStatus == "rejected"; 
                case "in-progress":
                    return newStatus == "completed" || newStatus == "rejected"; 
                default:
                    return false; 
            }
        }

        public class UpdateServiceRequestDto
        {
            [Required] public int RequestId { get; set; }
            [Required] public string NewStatus { get; set; } = string.Empty;
            public string? StaffNotes { get; set; }
            public int? AssignedEmployeeId { get; set; }
        }

        public class UpdateStaffNotesDto
        {
            [Required] public int RequestId { get; set; }
            [Required] public string StaffNotes { get; set; } = string.Empty;
        }

        private string? CalculateCompletionTime(DateTime? start, DateTime? end)
        {
             if (!start.HasValue || !end.HasValue || end.Value < start.Value) 
             {
                return null; 
             }
             return CalculateBusinessTime(start.Value, end.Value); 
        }

        private string CalculateBusinessTime(DateTime start, DateTime end)
        {
            TimeSpan duration = end - start;
            if (duration.TotalDays >= 1)
            {
                return $"{duration.TotalDays:F1} days";
            }
            else if (duration.TotalHours >= 1)
            {
                return $"{duration.TotalHours:F1} hours";
            }
            else
            {
                return $"{duration.TotalMinutes:F0} minutes";
            }
        }

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
        [ValidateAntiForgeryToken]
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
                if (!IsStaffLoggedIn(out var username)) return Unauthorized();
                var staff = GetLoggedInStaffUser(username);
                
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
                if (!IsStaffLoggedIn(out var username)) return Unauthorized();
                var staff = GetLoggedInStaffUser(username);
                
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
                if (!IsStaffLoggedIn(out _)) return Unauthorized(); // Check login

                var announcement = _context.Announcements.FirstOrDefault(a => a.Id == id);
                if (announcement == null)
                {
                    return Json(new { success = false, message = "Announcement not found." });
                }

                // Update announcement details
                announcement.Type = type;
                announcement.Description = description;

                // Handle image upload if provided
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

                // Return success response
                return Json(new { success = true, message = "Announcement updated successfully." });
            }
            catch (Exception ex)
            {
                // Log the exception and return an error response
                Console.WriteLine($"Error: {ex.Message}");
                return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAnnouncement(int id)
        {
            try
            {
                if (!IsStaffLoggedIn(out _)) return Unauthorized(); // Check login

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

        public IActionResult Staff_Contact_And_Support()
        {
            if (!IsStaffLoggedIn(out _))
            {
                return RedirectToAction("Login");
            }

            try
            {
                var contactRequests = _context.ContactRequests
                    .Include(c => c.Homeowner)
                    .OrderByDescending(c => c.DateSubmitted)
                    .ToList();

                // Return the view with the model
                return View(contactRequests);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading contact requests: {ex.Message}");
                return View(new List<ContactRequest>());
            }
        }
        

        public IActionResult Announcements()
        {
            if (!IsStaffLoggedIn(out _)) return RedirectToAction("Login"); // Check login

            var announcements = _context.Announcements
                .Include(a => a.Staff)
                .OrderByDescending(a => a.DateCreated)
                .ToList();
            
            return View("staff_announcement", announcements);
        }

        public IActionResult SecurityVisitors()
        {
            if (!IsStaffLoggedIn(out _)) return RedirectToAction("Login");

            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            ViewBag.AntiForgeryToken = tokens.RequestToken;
            return View("staff_security_visitors");
        }

        public IActionResult Staff_Community_Forum()
        {
            if (!IsStaffLoggedIn(out _)) return RedirectToAction("Login"); // Check login

            return View("staff_community_forum");
        }

        public class UpdateStatusModel
        {
            public int HomeownerId { get; set; }
            public string NewStatus { get; set; } = string.Empty;
        }

        // GET: /Staff/GetServiceCategories
        [HttpGet]
        public async Task<IActionResult> GetServiceCategories()
        {
            if (!IsStaffLoggedIn(out _)) return Unauthorized();

            try
            {
                var categories = await _context.ServiceCategories
                    .OrderBy(c => c.Name)
                    .Select(c => new
                    {
                        c.Id,
                        c.Name,
                        c.Description,
                        c.Icon
                    })
                    .ToListAsync();

                return Json(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching service categories");
                return StatusCode(500, new { message = "Error fetching service categories" });
            }
        }

        public class VisitorPassActionDto
        {
            [Required]
            public int PassId { get; set; }
            
            [Required]
            public string Action { get; set; } = string.Empty; // "approve", "reject", "revoke"
        }

        public class VehicleRegistrationActionDto
        {
            [Required]
            public int RegistrationId { get; set; }
            
            [Required]
            public string Action { get; set; } = string.Empty; // "approve", "reject", "revoke"
        }

        public class VisitorExitDto
        {
            [Required]
            public int PassId { get; set; }
            
            public DateTime ExitTime { get; set; } = DateTime.UtcNow;
        }

        [HttpGet]
        public async Task<IActionResult> GetPendingVisitorPasses()
        {
            var passes = await _context.VisitorPasses
                .Include(v => v.Homeowner)
                .Where(v => v.Status == "Pending")
                .OrderByDescending(v => v.RequestDate)
                .Select(v => new
                {
                    v.Id,
                    v.VisitorName,
                    VisitDate = v.VisitDate.ToString("yyyy-MM-dd"),
                    v.Purpose,
                    v.Status,
                    HomeownerName = v.Homeowner.FirstName + " " + v.Homeowner.LastName
                })
                .ToListAsync();

            return Json(passes);
        }

        [HttpGet]
        public async Task<IActionResult> GetApprovedVisitorPasses()
        {
            var passes = await _context.VisitorPasses
                .Include(v => v.Homeowner)
                .Where(v => v.Status == "Approved")
                .OrderByDescending(v => v.RequestDate)
                .Select(v => new
                {
                    v.Id,
                    v.VisitorName,
                    VisitDate = v.VisitDate.ToString("yyyy-MM-dd"),
                    v.Purpose,
                    v.Status,
                    HomeownerName = v.Homeowner.FirstName + " " + v.Homeowner.LastName
                })
                .ToListAsync();

            return Json(passes);
        }

        [HttpGet]
        public async Task<IActionResult> GetPendingVehicleRegistrations()
        {
            var registrations = await _context.VehicleRegistrations
                .Include(v => v.Homeowner)
                .Where(v => v.Status == "Pending")
                .OrderByDescending(v => v.RegistrationDate)
                .Select(v => new
                {
                    v.Id,
                    v.VehicleMake,
                    v.VehicleModel,
                    v.PlateNumber,
                    v.Status,
                    HomeownerName = v.Homeowner.FirstName + " " + v.Homeowner.LastName
                })
                .ToListAsync();

            return Json(registrations);
        }

        [HttpGet]
        public async Task<IActionResult> GetApprovedVehicleRegistrations()
        {
            var registrations = await _context.VehicleRegistrations
                .Include(v => v.Homeowner)
                .Where(v => v.Status == "Approved")
                .OrderByDescending(v => v.RegistrationDate)
                .Select(v => new
                {
                    v.Id,
                    v.VehicleMake,
                    v.VehicleModel,
                    v.PlateNumber,
                    v.Status,
                    HomeownerName = v.Homeowner.FirstName + " " + v.Homeowner.LastName,
                    RegistrationDate = v.RegistrationDate.ToString("yyyy-MM-dd")
                })
                .ToListAsync();

            return Json(registrations);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateVisitorPassStatus([FromBody] VisitorPassActionDto actionDto)
        {
            if (!IsStaffLoggedIn(out var username)) return Unauthorized();

            try
            {
                var staff = GetLoggedInStaffUser(username);
                var pass = await _context.VisitorPasses
                    .Include(v => v.Homeowner)
                    .FirstOrDefaultAsync(v => v.Id == actionDto.PassId);

                if (pass == null)
                {
                    return NotFound(new { success = false, message = "Visitor pass not found." });
                }

                switch (actionDto.Action.ToLower())
                {
                    case "approve":
                        if (pass.Status != "Pending")
                        {
                            return BadRequest(new { success = false, message = "Can only approve pending passes." });
                        }
                        pass.Status = "Approved";
                        pass.ApprovalDate = DateTime.UtcNow;
                        pass.EntryTime = DateTime.UtcNow; // Record entry time when approved
                        break;

                    case "reject":
                        if (pass.Status != "Pending")
                        {
                            return BadRequest(new { success = false, message = "Can only reject pending passes." });
                        }
                        pass.Status = "Rejected";
                        break;

                    case "revoke":
                        if (pass.Status != "Approved")
                        {
                            return BadRequest(new { success = false, message = "Can only revoke approved passes." });
                        }
                        pass.Status = "Revoked";
                        break;

                    default:
                        return BadRequest(new { success = false, message = "Invalid action." });
                }

                _context.VisitorPasses.Update(pass);
                await _context.SaveChangesAsync();

                // Return additional information for immediate display
                return Json(new { 
                    success = true, 
                    message = $"Visitor pass {actionDto.Action.ToLower()}ed successfully!",
                    passDetails = new {
                        id = pass.Id,
                        visitorName = pass.VisitorName,
                        visitDate = pass.VisitDate.ToString("yyyy-MM-dd"),
                        purpose = pass.Purpose,
                        status = pass.Status,
                        entryTime = pass.EntryTime?.ToString("yyyy-MM-dd HH:mm"),
                        homeownerName = pass.Homeowner != null ? $"{pass.Homeowner.FirstName} {pass.Homeowner.LastName}" : "N/A"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating visitor pass status for ID {PassId}", actionDto.PassId);
                return StatusCode(500, new { success = false, message = "An error occurred while updating the visitor pass." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateVehicleRegistrationStatus([FromBody] VehicleRegistrationActionDto actionDto)
        {
            if (!IsStaffLoggedIn(out var username))
                return Unauthorized(new { success = false, message = "Please log in to continue." });

            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid request data." });

            try
            {
                var staff = GetLoggedInStaffUser(username);
                var registration = await _context.VehicleRegistrations.FindAsync(actionDto.RegistrationId);
                if (registration == null)
                {
                    return NotFound(new { success = false, message = "Vehicle registration not found." });
                }

                switch (actionDto.Action.ToLower())
                {
                    case "approve":
                        if (registration.Status != "Pending")
                        {
                            return BadRequest(new { success = false, message = "Can only approve pending registrations." });
                        }
                        registration.Status = "Approved";
                        registration.ApprovalDate = DateTime.UtcNow;
                        break;

                    case "reject":
                        if (registration.Status != "Pending")
                        {
                            return BadRequest(new { success = false, message = "Can only reject pending registrations." });
                        }
                        registration.Status = "Rejected";
                        break;

                    case "revoke":
                        if (registration.Status != "Approved")
                        {
                            return BadRequest(new { success = false, message = "Can only revoke approved registrations." });
                        }
                        registration.Status = "Revoked";
                        break;

                    default:
                        return BadRequest(new { success = false, message = "Invalid action." });
                }

                _context.VehicleRegistrations.Update(registration);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Vehicle registration {actionDto.Action.ToLower()}ed successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating vehicle registration status for ID {RegistrationId}", actionDto.RegistrationId);
                return StatusCode(500, new { success = false, message = "An error occurred while updating the vehicle registration." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordVisitorExit([FromBody] VisitorExitDto exitDto)
        {
            if (!IsStaffLoggedIn(out var username)) return Unauthorized();

            try
            {
                var pass = await _context.VisitorPasses
                    .Include(v => v.Homeowner)
                    .FirstOrDefaultAsync(v => v.Id == exitDto.PassId);

                if (pass == null)
                {
                    return NotFound(new { success = false, message = "Visitor pass not found." });
                }

                if (pass.Status != "Approved" || pass.ExitTime.HasValue)
                {
                    return BadRequest(new { success = false, message = "Invalid visitor pass status or exit already recorded." });
                }

                // Record exit time
                pass.ExitTime = DateTime.UtcNow;
                pass.Status = "Completed";

                await _context.SaveChangesAsync();

                // Return additional information for immediate display
                return Json(new { 
                    success = true, 
                    message = "Visitor exit recorded successfully!",
                    exitDetails = new {
                        visitorName = pass.VisitorName,
                        entryTime = pass.EntryTime?.ToString("yyyy-MM-dd HH:mm"),
                        exitTime = pass.ExitTime?.ToString("yyyy-MM-dd HH:mm"),
                        purpose = pass.Purpose,
                        homeownerName = pass.Homeowner != null ? $"{pass.Homeowner.FirstName} {pass.Homeowner.LastName}" : "N/A"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording visitor exit for pass ID {PassId}", exitDto.PassId);
                return StatusCode(500, new { success = false, message = "An error occurred while recording the visitor exit." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveVisitors()
        {
            if (!IsStaffLoggedIn(out _)) return Unauthorized();

            try
            {
                var today = DateTime.UtcNow.Date;
                
                var activeVisitors = await _context.VisitorPasses
                    .Include(v => v.Homeowner)
                    .Where(v => v.Status == "Approved" && 
                            v.ExitTime == null && 
                            v.EntryTime != null &&
                            v.VisitDate.Date == today)
                    .OrderBy(v => v.EntryTime)
                    .Select(v => new
                    {
                        v.Id,
                        v.VisitorName,
                        EntryTime = v.EntryTime.HasValue ? v.EntryTime.Value.ToString("yyyy-MM-dd HH:mm") : null,
                        v.Purpose,
                        HomeownerName = v.Homeowner != null ? $"{v.Homeowner.FirstName} {v.Homeowner.LastName}" : "N/A",
                        VisitDuration = v.EntryTime.HasValue ? CalculateVisitDuration(v.EntryTime.Value) : "Not recorded"
                    })
                    .ToListAsync();

                return Json(activeVisitors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active visitors");
                return StatusCode(500, new { message = "Error retrieving active visitors. Please try again later." });
            }
        }

        private string CalculateVisitDuration(DateTime entryTime)
        {
            var duration = DateTime.UtcNow - entryTime;
            if (duration.TotalHours >= 24)
            {
                return $"{Math.Floor(duration.TotalDays)} days";
            }
            if (duration.TotalHours >= 1)
            {
                return $"{Math.Floor(duration.TotalHours)} hours";
            }
            return $"{Math.Floor(duration.TotalMinutes)} minutes";
        }

        [HttpGet]
        public async Task<IActionResult> GetTodayExits()
        {
            if (!IsStaffLoggedIn(out _)) return Unauthorized();

            try
            {
                var today = DateTime.Today;
                var todayExits = await _context.VisitorPasses
                    .Include(v => v.Homeowner)
                    .Where(v => v.ExitTime.HasValue && v.ExitTime.Value.Date == today)
                    .OrderByDescending(v => v.ExitTime)
                    .Select(v => new
                    {
                        v.Id,
                        v.VisitorName,
                        EntryTime = v.EntryTime.HasValue ? v.EntryTime.Value.ToString("yyyy-MM-dd HH:mm") : null,
                        ExitTime = v.ExitTime.HasValue ? v.ExitTime.Value.ToString("yyyy-MM-dd HH:mm") : null,
                        v.Purpose,
                        HomeownerName = v.Homeowner != null ? $"{v.Homeowner.FirstName} {v.Homeowner.LastName}" : "N/A",
                        v.Status
                    })
                    .ToListAsync();

                return Json(todayExits);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching today's visitor exits");
                return StatusCode(500, new { message = "Error retrieving visitor exits" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSecurityCounts()
        {
            if (!IsStaffLoggedIn(out _)) return Unauthorized();

            try
            {
                var pendingVisitors = await _context.VisitorPasses
                    .CountAsync(v => v.Status == "Pending");

                var activeVisitors = await _context.VisitorPasses
                    .CountAsync(v => v.Status == "Approved" && v.VisitDate.Date == DateTime.Today);

                var pendingVehicles = await _context.VehicleRegistrations
                    .CountAsync(v => v.Status == "Pending");

                return Json(new
                {
                    pendingVisitors,
                    activeVisitors,
                    pendingVehicles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security counts");
                return StatusCode(500, new { message = "Error retrieving security counts" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllVehicleRegistrations()
        {
            if (!IsStaffLoggedIn(out _)) return Unauthorized();

            try
            {
                var registrations = await _context.VehicleRegistrations
                    .Include(v => v.Homeowner)
                    .OrderByDescending(v => v.RegistrationDate)
                    .Select(v => new
                    {
                        v.Id,
                        v.VehicleMake,
                        v.VehicleModel,
                        v.PlateNumber,
                        RegistrationDate = v.RegistrationDate.ToString("yyyy-MM-dd"),
                        v.Status,
                        HomeownerName = v.Homeowner != null ? $"{v.Homeowner.FirstName} {v.Homeowner.LastName}" : "N/A"
                    })
                    .ToListAsync();

                return Json(registrations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching vehicle registrations");
                return StatusCode(500, new { message = "Error retrieving vehicle registrations" });
            }
        }
    }
}