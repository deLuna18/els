using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using SubdivisionManagement.Model;
using System.Linq;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Antiforgery;

public class AdminController : Controller
{
    private readonly HomeContext _context;
    private readonly ILogger<AdminController> _logger;
    private readonly IAntiforgery _antiforgery;

    public AdminController(HomeContext context, ILogger<AdminController> logger, IAntiforgery antiforgery)
    {
        _context = context;
        _logger = logger;
        _antiforgery = antiforgery;
    }

    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Login(string username, string password)
    {
        var admin = _context.Admins.FirstOrDefault(a => a.Username == username);


        if (admin == null || !VerifyPassword(password, admin.PasswordHash))
        {
            ViewBag.Error = "Invalid username or password";
            return View();
        }

        HttpContext.Session.SetString("AdminUser", username);
        return RedirectToAction("Dashboard");
    }

    public IActionResult Dashboard()
    {
        if (HttpContext.Session.GetString("AdminUser") == null)
        {
            return RedirectToAction("Login");
        }

        return View("admin_dashboard");
    }

    public IActionResult Logout()
    {
        HttpContext.Session. Clear();
        return RedirectToAction("Login");
    }

    private bool VerifyPassword(string password, string storedHash)
    {
        return HashPassword(password) == storedHash;
    }

    private string HashPassword(string password)
    {
        byte[] salt = Encoding.UTF8.GetBytes("password123");
        return Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 10000,
            numBytesRequested: 256 / 8));
    }

    public async Task<IActionResult> RegisterStaff([FromBody] Staff staff)
    {
        if (staff == null)
        {
            return BadRequest("Invalid data.");
        }

        // Generate a random salt for the password
        var salt = new byte[16];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Hash the password using the generated salt
        var hashedPassword = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: staff.PasswordHash,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 10000,
            numBytesRequested: 256 / 8));

        // Store the hashed password and the salt in the database
        staff.PasswordHash = hashedPassword;
        staff.PasswordSalt = Convert.ToBase64String(salt);

        // Add the staff to the database
        _context.Staffs.Add(staff);
        await _context.SaveChangesAsync();

        return Json(new { message = "Staff successfully registered." });
    }


    public IActionResult Services()
    {
        if (HttpContext.Session.GetString("AdminUser") == null)
        {
            return RedirectToAction("Login");
        }

        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        ViewBag.AntiForgeryToken = tokens.RequestToken;
        return View("admin_services");
    }

    [HttpGet]
    public async Task<IActionResult> GetServiceCategories()
    {
        if (HttpContext.Session.GetString("AdminUser") == null)
            return Unauthorized();

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddServiceCategory([FromBody] ServiceCategory category)
    {
        if (HttpContext.Session.GetString("AdminUser") == null)
            return Unauthorized();

        try
        {
            _context.ServiceCategories.Add(category);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Category added successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding service category");
            return StatusCode(500, new { success = false, message = "Error adding category" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetServiceEmployees()
    {
        if (HttpContext.Session.GetString("AdminUser") == null)
            return Unauthorized();

        try
        {
            var employees = await _context.Staffs
                .Where(s => s.Role == "ServiceEmployee" || s.Role == "Staff")
                .Select(s => new
                {
                    s.Id,
                    s.Username,
                    Name = s.FullName,
                    s.Email,
                    Phone = s.ContactNumber,
                    s.Status,
                    s.Specialization,
                    s.Department,
                    s.Position
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

    [HttpGet]
    public async Task<IActionResult> GetServiceLogs()
    {
        if (HttpContext.Session.GetString("AdminUser") == null)
            return Unauthorized();

        try
        {
            var logs = await _context.ServiceRequests
                .Include(sr => sr.Homeowner)
                .Include(sr => sr.Staff)
                .OrderByDescending(sr => sr.DateSubmitted)
                .Select(sr => new
                {
                    sr.Id,
                    RequestId = $"SR-{sr.Id:D3}",
                    HomeownerName = sr.Homeowner != null ? $"{sr.Homeowner.FirstName} {sr.Homeowner.LastName}" : "N/A",
                    sr.ServiceType,
                    sr.Priority,
                    sr.Status,
                    DateSubmitted = sr.DateSubmitted,
                    sr.Description,
                    sr.DateAccepted,
                    sr.DateStarted,
                    sr.DateCompleted,
                    CompletionTime = sr.DateCompleted.HasValue && sr.DateAccepted.HasValue 
                        ? (sr.DateCompleted.Value - sr.DateAccepted.Value).TotalDays.ToString("F1") + " days"
                        : null,
                    sr.StaffNotes,
                    StaffName = sr.Staff != null ? sr.Staff.FullName : null
                })
                .ToListAsync();

            return Json(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching service logs");
            return StatusCode(500, new { message = "Error fetching service logs" });
        }
    }

    [HttpPut]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateServiceCategory([FromBody] ServiceCategory category)
    {
        if (HttpContext.Session.GetString("AdminUser") == null)
            return Unauthorized();

        try
        {
            var existingCategory = await _context.ServiceCategories.FindAsync(category.Id);
            if (existingCategory == null)
                return NotFound(new { success = false, message = "Category not found" });

            existingCategory.Name = category.Name;
            existingCategory.Description = category.Description;
            existingCategory.Icon = category.Icon;
            existingCategory.DateModified = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Category updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service category");
            return StatusCode(500, new { success = false, message = "Error updating category" });
        }
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteServiceCategory(int id)
    {
        if (HttpContext.Session.GetString("AdminUser") == null)
            return Unauthorized();

        try
        {
            var category = await _context.ServiceCategories.FindAsync(id);
            if (category == null)
                return NotFound(new { success = false, message = "Category not found" });

            _context.ServiceCategories.Remove(category);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Category deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting service category");
            return StatusCode(500, new { success = false, message = "Error deleting category" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddServiceEmployee([FromBody] ServiceEmployeeDto employeeDto)
    {
        if (HttpContext.Session.GetString("AdminUser") == null)
            return Unauthorized();

        try
        {
            var staff = new Staff
            {
                FullName = employeeDto.Name,
                Email = employeeDto.Email,
                ContactNumber = employeeDto.Phone,
                Specialization = employeeDto.Specialization,
                Role = "ServiceEmployee",
                Status = "active",
                Username = employeeDto.Email, // Using email as username
                PasswordHash = HashPassword("password123") // Default password
            };

            _context.Staffs.Add(staff);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Employee added successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding service employee");
            return StatusCode(500, new { success = false, message = "Error adding employee" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEmployeeStatus([FromBody] UpdateEmployeeStatusDto model)
    {
        if (HttpContext.Session.GetString("AdminUser") == null)
            return Unauthorized();

        try
        {
            var employee = await _context.Staffs.FindAsync(model.EmployeeId);
            if (employee == null)
                return NotFound(new { success = false, message = "Employee not found" });

            employee.Status = model.Status;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Employee status updated to {model.Status}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating employee status");
            return StatusCode(500, new { success = false, message = "Error updating employee status" });
        }
    }

    [HttpPut]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateServiceEmployee([FromBody] UpdateServiceEmployeeDto model)
    {
        if (HttpContext.Session.GetString("AdminUser") == null)
            return Unauthorized();

        try
        {
            var employee = await _context.Staffs.FindAsync(model.Id);
            if (employee == null)
                return NotFound(new { success = false, message = "Employee not found" });

            employee.FullName = model.Name;
            employee.Email = model.Email;
            employee.ContactNumber = model.Phone;
            employee.Specialization = model.Specialization;
            employee.Username = model.Email; // Update username to match new email

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Employee updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service employee");
            return StatusCode(500, new { success = false, message = "Error updating employee" });
        }
    }

    public class ServiceEmployeeDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
    }

    public class UpdateEmployeeStatusDto
    {
        public int EmployeeId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class UpdateServiceEmployeeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
    }
}
