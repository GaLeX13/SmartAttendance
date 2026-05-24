using Microsoft.AspNetCore.Mvc;
using SmartAttendance.Data;
using SmartAttendance.Models;
using System.Security.Cryptography;
using System.Text;

namespace SmartAttendance.Controllers
{
    public class StudentAuthController : Controller
    {
        private readonly AppDbContext _context;

        public StudentAuthController(AppDbContext context)
        {
            _context = context;
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
            var hash = Hash(password);

            var student = _context.Students
                .FirstOrDefault(s => s.Email == email && s.PasswordHash == hash);

            if (student == null)
            {
                ViewBag.Error = "Email sau parolă greșită.";
                return View();
            }

            HttpContext.Session.SetString("role", "student");
            HttpContext.Session.SetString("student_email", student.Email);

            return RedirectToAction("Index", "Student");
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(string email, string password, string confirmPassword)
        {
            email = (email ?? "").Trim().ToLower();

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                ViewBag.Error = "Completează toate câmpurile.";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Parolele nu coincid.";
                return View();
            }

            var student = _context.Students.FirstOrDefault(s => s.Email == email);

            if (student != null && !string.IsNullOrEmpty(student.PasswordHash))
            {
                ViewBag.Error = "Există deja un cont activ cu acest email.";
                return View();
            }

            if (student == null)
            {
                student = new Student
                {
                    Email = email,
                    PasswordHash = Hash(password)
                };
                _context.Students.Add(student);
            }
            else
            {
             
                student.PasswordHash = Hash(password);
            }

            _context.SaveChanges();
            return RedirectToAction("Login");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        private string Hash(string input)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(
                sha.ComputeHash(Encoding.UTF8.GetBytes(input))
            );
        }
    }
}
