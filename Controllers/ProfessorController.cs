using Microsoft.AspNetCore.Mvc;
using SmartAttendance.Data;
using SmartAttendance.Models;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace SmartAttendance.Controllers
{
    public class ProfessorController : Controller
    {
        private readonly AppDbContext _context;

        public ProfessorController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var courses = _context.Courses
                .Where(c => c.ProfessorId == professorId)
                .ToList();

            return View(courses);
        }

        public IActionResult Course(int id)
        {
            var role = HttpContext.Session.GetString("role");
            if (role != "professor")
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .Include(c => c.CourseStudents)
                    .ThenInclude(cs => cs.Student)
                .FirstOrDefault(c => c.Id == id);

            if (course == null)
                return RedirectToAction("Index");

            return View(course);
        }

        [HttpPost]
        public IActionResult AddCourse(string name, string type)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = new Course
            {
                Name = name.Trim(),
                IsLab = type == "lab",
                ProfessorId = professorId.Value,
                StudentCount = 0,
                AttendancePercent = 0
            };

            _context.Courses.Add(course);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult EditStudents(int courseId)
        {
            var course = _context.Courses
                .Include(c => c.CourseStudents)
                .ThenInclude(cs => cs.Student)
                .FirstOrDefault(c => c.Id == courseId);

            if (course == null)
                return NotFound();

            ViewBag.CourseName = course.Name;
            ViewBag.CourseId = course.Id;

            return View();
        }

        [HttpPost]
        public IActionResult EditStudents(int courseId, string studentEmails)
        {
            if (string.IsNullOrWhiteSpace(studentEmails))
                return RedirectToAction("Index");

            var emails = studentEmails
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim().ToLower())
                .Distinct()
                .ToList();

            var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

            var course = _context.Courses
                .Include(c => c.CourseStudents)
                .FirstOrDefault(c => c.Id == courseId);

            if (course == null)
                return NotFound();

            foreach (var email in emails)
            {
                if (!emailRegex.IsMatch(email))
                    continue;

                var student = _context.Students.FirstOrDefault(s => s.Email == email);

                if (student == null)
                {
                    student = new Student
                    {
                        Email = email,
                        PasswordHash = "" // student își va seta parola la register
                    };
                    _context.Students.Add(student);
                    _context.SaveChanges();
                }

                bool alreadyAdded = _context.CourseStudents
                    .Any(cs => cs.CourseId == courseId && cs.StudentId == student.Id);

                if (!alreadyAdded)
                {
                    _context.CourseStudents.Add(new CourseStudent
                    {
                        CourseId = courseId,
                        StudentId = student.Id
                    });
                }
            }

            _context.SaveChanges();
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult DeleteCourse(int id)
        {
            var course = _context.Courses.FirstOrDefault(c => c.Id == id);
            if (course != null)
            {
                _context.Courses.Remove(course);
                _context.SaveChanges();
            }

            return RedirectToAction("Index");
        }



        [HttpGet]
        public IActionResult AddStudents(int courseId)
        {
            var course = _context.Courses.FirstOrDefault(c => c.Id == courseId);
            if (course == null) return NotFound();

            ViewBag.CourseId = courseId;
            ViewBag.CourseName = course.Name;
            return View();
        }

        [HttpPost]
        public IActionResult AddStudents(int courseId, string emails)
        {
            if (string.IsNullOrWhiteSpace(emails))
                return RedirectToAction("Index");

            var lines = emails
                .Split('\n')
                .Select(e => e.Trim().ToLower())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct();

            foreach (var email in lines)
            {
                if (!IsValidEmail(email)) continue;

                var student = _context.Students.FirstOrDefault(s => s.Email == email);
                if (student == null)
                {
                    student = new Student
                    {
                        Email = email,
                        PasswordHash = ""
                    };
                    _context.Students.Add(student);
                    _context.SaveChanges();
                }

                bool alreadyLinked = _context.CourseStudents
                    .Any(cs => cs.CourseId == courseId && cs.StudentId == student.Id);

                if (!alreadyLinked)
                {
                    _context.CourseStudents.Add(new CourseStudent
                    {
                        CourseId = courseId,
                        StudentId = student.Id
                    });
                }
            }

            _context.SaveChanges();
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult RemoveStudent(int courseId, int studentId)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .FirstOrDefault(c => c.Id == courseId && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index");

            var link = _context.CourseStudents
                .FirstOrDefault(cs => cs.CourseId == courseId && cs.StudentId == studentId);

            if (link != null)
            {
                _context.CourseStudents.Remove(link);
                _context.SaveChanges();
            }

            return RedirectToAction("Course", new { id = courseId });
        }

        private bool IsValidEmail(string email)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                email,
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$"
            );
        }

        private string Hash(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return Convert.ToBase64String(
                sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input))
            );
        }

    }
}
