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
            if (accessKey != "UCV")
            {
                ViewBag.Error = "Cheia de acces este invalidă.";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Parolele nu coincid.";
                return View();
            }

            if (_context.Professors.Any(p => p.Email == email))
            {
                ViewBag.Error = "Există deja un cont.";
                return View();
            }

            var professor = new Professor
            {
                Email = email.Trim().ToLower(),
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
            email = email.Trim().ToLower();

            var professor = _context.Professors
                .FirstOrDefault(p => p.Email == email);

            if (professor == null)
            {
                ViewBag.Error = "Email sau parolă greșite";
                return View();
            }

            var hashed = Hash(password);
            if (professor.PasswordHash != hashed)
            {
                ViewBag.Error = "Email sau parolă greșite";
                return View();
            }

            
            HttpContext.Session.SetInt32("ProfessorId", professor.Id);
            HttpContext.Session.SetString("role", "professor");

            return RedirectToAction("Index", "Professor");
        }


        private string Hash(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }
    }
}
