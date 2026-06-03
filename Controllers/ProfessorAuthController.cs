using Microsoft.AspNetCore.Mvc;
using SmartAttendance.Data;
using SmartAttendance.Models;
using System.Security.Cryptography;
using System.Text;

namespace SmartAttendance.Controllers
{
    public class ProfessorAuthController : Controller
    {
        private readonly AppDbContext _context;

        public ProfessorAuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(string email, string password, string confirmPassword, string accessKey)
        {
            email = (email ?? "").Trim().ToLower();

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(confirmPassword) ||
                string.IsNullOrWhiteSpace(accessKey))
            {
                ViewBag.Error = "Please complete all fields.";
                return View();
            }

            if (accessKey != "UCV")
            {
                ViewBag.Error = "Invalid access key.";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                return View();
            }

            if (_context.Professors.Any(p => p.Email == email))
            {
                ViewBag.Error = "An account with this email already exists.";
                return View();
            }

            var professor = new Professor
            {
                Email = email,
                PasswordHash = Hash(password),
                FullName = email.Split('@')[0]
            };

            _context.Professors.Add(professor);
            _context.SaveChanges();

            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            email = (email ?? "").Trim().ToLower();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Please enter your email and password.";
                return View();
            }

            var professor = _context.Professors
                .FirstOrDefault(p => p.Email == email);

            if (professor == null || professor.PasswordHash != Hash(password))
            {
                ViewBag.Error = "Invalid email or password.";
                return View();
            }

            HttpContext.Session.SetInt32("ProfessorId", professor.Id);
            HttpContext.Session.SetString("role", "professor");

            return RedirectToAction("Index", "Professor");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        private string Hash(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }
    }
}