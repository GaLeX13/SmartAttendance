using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartAttendance.Data;

namespace SmartAttendance.Controllers
{
    public class StudentController : Controller
    {
        private readonly AppDbContext _context;

        public StudentController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var role = HttpContext.Session.GetString("role");
            if (role != "student")
                return RedirectToAction("Login", "StudentAuth");

            var email = HttpContext.Session.GetString("student_email");
            if (email == null)
                return RedirectToAction("Login", "StudentAuth");

            var student = _context.Students
                .Include(s => s.CourseStudents)
                    .ThenInclude(cs => cs.Course)
                .FirstOrDefault(s => s.Email == email);

            if (student == null)
                return RedirectToAction("Login", "StudentAuth");

            return View(student);
        }
    }
}
