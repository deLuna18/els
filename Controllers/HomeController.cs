using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SubdivisionManagement.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Subdivision_Management_System.Controllers;

public class HomeController : Controller
{
    private readonly HomeContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(HomeContext context, ILogger<HomeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    { 
        var homeowner = await _context.Homeowners.FirstOrDefaultAsync(h => h.Email == email);
        
        if (homeowner != null && VerifyPassword(password, homeowner.PasswordHash))
        {
            // Check homeowner status
            if (homeowner.Status.ToLower() != "approved")
            {
                ViewBag.ShowErrorMessage = true;
                ViewBag.ErrorMessage = homeowner.Status.ToLower() == "pending" 
                    ? "Your account is pending approval. Please wait for admin approval."
                    : "Your account has been disapproved. Please contact the administrator.";
                return View("Index");
            }

            // Set session or authentication cookie here if needed
            HttpContext.Session.SetInt32("UserId", homeowner.Id);
            return RedirectToAction("Dashboard");
        }

        ViewBag.ShowErrorMessage = true;
        ViewBag.ErrorMessage = "Invalid email or password. Please try again.";
        return View("Index");
    }

    private bool VerifyPassword(string password, string storedHash)
    {
        string hashedPassword = HashPassword(password);
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
    public async Task<IActionResult> Register(Homeowner homeowner)
    {
        if (ModelState.IsValid)
        {
            homeowner.PasswordHash = HashPassword(homeowner.PasswordHash); // Hash the password
            homeowner.Status = "pending"; // Set initial status to pending
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

    private int GetLoggedInUserId()
    {
        // Retrieve the logged-in user's ID from the session
        int? userId = HttpContext.Session.GetInt32("UserId");

        // If the session does not contain a user ID, return 0 or handle it as needed
        return userId ?? 0;
    }

    [HttpGet]
    public IActionResult Profile()
    {
        int loggedInUserId = GetLoggedInUserId(); // Use the helper method to get the user ID
        var homeowner = _context.Homeowners.FirstOrDefault(h => h.Id == loggedInUserId);

        if (homeowner == null)
        {
            return RedirectToAction("Index"); // Redirect if no user is found
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

    public IActionResult Services()
    {
        return View();
    }

    public IActionResult security_visitors()
    {
        return View();
    }

    public IActionResult Community_forum()
    {
        return View();
    }

    public IActionResult facility_reservation()
    {
        return View();
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index");
    }
}