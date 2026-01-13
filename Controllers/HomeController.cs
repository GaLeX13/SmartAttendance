using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmartAttendance.Models;

namespace SmartAttendance.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Student()
        {
            return RedirectToAction("Login", "StudentAuth");
        }

        public IActionResult Professor()
        {
            return RedirectToAction("Login", "ProfessorAuth");
        }
    }
}
