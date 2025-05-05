using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SubdivisionManagement.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Text;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using System.ComponentModel.DataAnnotations;

namespace Subdivision_Management_System.Controllers;

public class HomeController : Controller
{
    private readonly HomeContext _context;
    private readonly ILogger<HomeController> _logger;
    private readonly IAntiforgery _antiforgery;

    public HomeController(HomeContext context, ILogger<HomeController> logger, IAntiforgery antiforgery)
    {
        _context = context;
        _logger = logger;
        _antiforgery = antiforgery;
    }

    public IActionResult Index()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        ViewBag.AntiForgeryToken = tokens.RequestToken;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password)
    { 
        var homeowner = await _context.Homeowners.FirstOrDefaultAsync(h => h.Email == email);
        
        if (homeowner != null && VerifyPassword(password, homeowner.PasswordHash))
        {
            if (homeowner.Status.ToLower() != "approved")
            {
                ViewBag.ShowErrorMessage = true;
                ViewBag.ErrorMessage = homeowner.Status.ToLower() == "pending" 
                    ? "Your account is pending approval. Please wait for admin approval."
                    : "Your account has been disapproved. Please contact the administrator.";
                return View("Index");
            }

            HttpContext.Session.SetInt32("UserId", homeowner.Id);
            return RedirectToAction("Dashboard");
        }

        ViewBag.ShowErrorMessage = true;
        ViewBag.ErrorMessage = "Invalid email or password. Please try again.";
        return View("Index");
    }

    private bool VerifyPassword(string password, string storedHash)
    {
        byte[] salt = Encoding.UTF8.GetBytes("password123");
        string hashedPassword = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 10000,
            numBytesRequested: 256 / 8));
        return hashedPassword == storedHash;
    }

    public IActionResult Dashboard()
    {
        var announcements = _context.Announcements
            .Include(a => a.Staff)
            .OrderByDescending(a => a.DateCreated)
            .Take(4)
            .ToList();
            
        return View(announcements);
    }

    public IActionResult Contact()
    {
        return View();
    }

    public IActionResult Announcements()
    {
        if (!IsUserLoggedIn()) return RedirectToAction("Index");

        var announcements = _context.Announcements
            .Include(a => a.Staff)
            .OrderByDescending(a => a.DateCreated)
            .ToList()
            .GroupBy(a => a.Type)
            .ToDictionary(g => g.Key, g => g.ToList());
            
        return View(announcements);
    }

    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(Homeowner homeowner)
    {
        if (ModelState.IsValid)
        {
            homeowner.PasswordHash = HashPassword(homeowner.PasswordHash);
            homeowner.Status = "pending";
            _context.Homeowners.Add(homeowner);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "You've Successfully Registered. Please wait until your registration is approved.";
            return RedirectToAction("Index", "Home");
        }
        return View(homeowner);
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

    private int? GetLoggedInUserId()
    {
        return HttpContext.Session.GetInt32("UserId");
    }

    private bool IsUserLoggedIn()
    {
        return GetLoggedInUserId() != null;
    }

    [HttpGet]
    public IActionResult Profile()
    {
        var loggedInUserId = GetLoggedInUserId();
        if (loggedInUserId == null) return RedirectToAction("Index");
        
        var homeowner = _context.Homeowners.FirstOrDefault(h => h.Id == loggedInUserId.Value);

        if (homeowner == null)
        {
            return RedirectToAction("Index");
        }

        return View(homeowner);
    }

    public IActionResult EditProfile()
    {
        return View("edit_profile");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    public IActionResult Billing()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Services()
    {
        if (!IsUserLoggedIn()) return RedirectToAction("Index");
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        ViewBag.AntiForgeryToken = tokens.RequestToken;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetHomeownerServiceRequests()
    {
        var loggedInUserId = GetLoggedInUserId();
        if (loggedInUserId == null) return Unauthorized();

        var requests = await _context.ServiceRequests
            .Where(r => r.HomeownerId == loggedInUserId.Value)
            .OrderByDescending(r => r.DateSubmitted)
            .Select(r => new 
            {
                r.Id,
                RequestId = $"SR-{r.Id:D3}",
                r.ServiceType,
                r.Status,
                DateSubmitted = r.DateSubmitted.ToString("yyyy-MM-dd"),
                r.Description,
                r.Priority,
                DateCompleted = r.DateCompleted != null ? r.DateCompleted.Value.ToString("yyyy-MM-dd") : null,
                CompletionTime = r.DateCompleted != null ? (r.DateCompleted.Value - r.DateSubmitted).TotalDays.ToString("F1") + " days" : null,
                r.StaffNotes
            })
            .ToListAsync();

        return Json(requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestService([FromBody] ServiceRequestDto requestDto)
    {
        var loggedInUserId = GetLoggedInUserId();
        if (loggedInUserId == null) return Unauthorized();
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var serviceRequest = new ServiceRequest
        {
            HomeownerId = loggedInUserId.Value,
            ServiceType = requestDto.ServiceType,
            Description = requestDto.Description,
            Priority = requestDto.Priority,
            Status = "Pending",
            DateSubmitted = DateTime.UtcNow
        };

        _context.ServiceRequests.Add(serviceRequest);
        await _context.SaveChangesAsync();

        return Json(new { 
            success = true, 
            message = "Service request submitted successfully!",
            request = new {
                Id = serviceRequest.Id,
                RequestId = $"SR-{serviceRequest.Id:D3}",
                serviceRequest.ServiceType,
                serviceRequest.Status,
                DateSubmitted = serviceRequest.DateSubmitted.ToString("yyyy-MM-dd"),
                serviceRequest.Description,
                serviceRequest.Priority,
                DateCompleted = (string?)null,
                CompletionTime = (string?)null,
                StaffNotes = (string?)null
            }
        });
    }

    public class ServiceRequestDto
    {
        [Required] public string ServiceType { get; set; } = string.Empty;
        [Required] public string Description { get; set; } = string.Empty;
        [Required] public string Priority { get; set; } = string.Empty;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestVisitorPass([FromBody] VisitorPassDto passDto)
    {
        try
        {
            var loggedInUserId = GetLoggedInUserId();
            if (loggedInUserId == null) return Unauthorized(new { success = false, message = "Please log in to continue." });
            if (!ModelState.IsValid) return BadRequest(new { success = false, message = "Invalid form data." });

            var visitorPass = new VisitorPass
            {
                HomeownerId = loggedInUserId.Value,
                VisitorName = passDto.VisitorName,
                VisitDate = DateTime.Parse(passDto.VisitDate),
                Purpose = passDto.Purpose,
                Status = "Pending",
                RequestDate = DateTime.UtcNow
            };

            _context.VisitorPasses.Add(visitorPass);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Visitor pass request submitted successfully!",
                pass = new
                {
                    visitorPass.Id,
                    visitorPass.VisitorName,
                    VisitDate = visitorPass.VisitDate.ToString("yyyy-MM-dd"),
                    visitorPass.Purpose,
                    visitorPass.Status
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting visitor pass request");
            return StatusCode(500, new { success = false, message = "An error occurred while submitting the request." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterVehicle([FromBody] VehicleRegistrationDto regDto)
    {
        try
        {
            var loggedInUserId = GetLoggedInUserId();
            if (loggedInUserId == null) return Unauthorized(new { success = false, message = "Please log in to continue." });
            if (!ModelState.IsValid) return BadRequest(new { success = false, message = "Invalid form data." });

            var registration = new VehicleRegistration
            {
                HomeownerId = loggedInUserId.Value,
                VehicleMake = regDto.VehicleMake,
                VehicleModel = regDto.VehicleModel,
                PlateNumber = regDto.PlateNumber,
                Status = "Pending",
                RegistrationDate = DateTime.UtcNow
            };

            _context.VehicleRegistrations.Add(registration);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Vehicle registration submitted successfully!",
                registration = new
                {
                    registration.Id,
                    registration.VehicleMake,
                    registration.VehicleModel,
                    registration.PlateNumber,
                    registration.Status
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting vehicle registration");
            return StatusCode(500, new { success = false, message = "An error occurred while submitting the registration." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetVisitorPasses()
    {
        try
        {
            var loggedInUserId = GetLoggedInUserId();
            if (loggedInUserId == null) return Unauthorized();

            var passes = await _context.VisitorPasses
                .Where(v => v.HomeownerId == loggedInUserId.Value)
                .OrderByDescending(v => v.RequestDate)
                .Select(v => new
                {
                    v.Id,
                    v.VisitorName,
                    VisitDate = v.VisitDate.ToString("yyyy-MM-dd"),
                    v.Purpose,
                    v.Status
                })
                .ToListAsync();

            return Json(passes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving visitor passes");
            return StatusCode(500, new { message = "An error occurred while retrieving visitor passes." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetVehicleRegistrations()
    {
        try
        {
            var loggedInUserId = GetLoggedInUserId();
            if (loggedInUserId == null) return Unauthorized();

            var registrations = await _context.VehicleRegistrations
                .Where(v => v.HomeownerId == loggedInUserId.Value)
                .OrderByDescending(v => v.RegistrationDate)
                .Select(v => new
                {
                    v.Id,
                    v.VehicleMake,
                    v.VehicleModel,
                    v.PlateNumber,
                    v.Status
                })
                .ToListAsync();

            return Json(registrations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle registrations");
            return StatusCode(500, new { message = "An error occurred while retrieving vehicle registrations." });
        }
    }

    public class VisitorPassDto
    {
        [Required]
        public string VisitorName { get; set; } = string.Empty;
        
        [Required]
        public string VisitDate { get; set; } = string.Empty;
        
        [Required]
        public string Purpose { get; set; } = string.Empty;
    }

    public class VehicleRegistrationDto
    {
        [Required]
        public string VehicleMake { get; set; } = string.Empty;
        
        [Required]
        public string VehicleModel { get; set; } = string.Empty;
        
        [Required]
        public string PlateNumber { get; set; } = string.Empty;
    }

    public IActionResult security_visitors()
    {
        if (!IsUserLoggedIn()) return RedirectToAction("Index");
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        ViewBag.AntiForgeryToken = tokens.RequestToken;
        return View();
    }

    public IActionResult Community_forum()
    {
        if (!IsUserLoggedIn()) return RedirectToAction("Index");
        return View();
    }

    public IActionResult facility_reservation()
    {
        if (!IsUserLoggedIn()) return RedirectToAction("Index");
        return View();
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index");
    }
}