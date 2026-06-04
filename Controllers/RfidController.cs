using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartAttendance.Data;
using SmartAttendance.Models;
using SmartAttendance.ViewModels;

namespace SmartAttendance.Controllers
{
    public class RfidController : Controller
    {
        private readonly AppDbContext _context;

        public RfidController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index(int courseId)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .FirstOrDefault(c => c.Id == courseId && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index", "Professor");

            var students = _context.CourseStudents
                .Where(cs => cs.CourseId == course.Id)
                .Include(cs => cs.Student)
                    .ThenInclude(s => s.RfidTags)
                .OrderBy(cs => cs.Student.Email)
                .ToList();

            var model = new RfidTagManagerViewModel
            {
                CourseId = course.Id,
                CourseName = course.Name,
                CourseType = course.IsLab ? "Laboratory" : "Course",
                Students = students.Select(cs =>
                {
                    var activeTag = cs.Student.RfidTags
                        .FirstOrDefault(t => t.IsActive);

                    return new RfidStudentTagRowViewModel
                    {
                        StudentId = cs.StudentId,
                        StudentEmail = cs.Student.Email,
                        CurrentUid = activeTag?.Uid,
                        HasActiveTag = activeTag != null
                    };
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult SaveTag(int courseId, int studentId, string uid)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .FirstOrDefault(c => c.Id == courseId && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index", "Professor");

            bool studentInCourse = _context.CourseStudents
                .Any(cs => cs.CourseId == courseId && cs.StudentId == studentId);

            if (!studentInCourse)
            {
                TempData["RfidMessage"] = "The selected student is not enrolled in this course.";
                return RedirectToAction("Index", new { courseId });
            }

            var normalizedUid = NormalizeUid(uid);

            if (string.IsNullOrWhiteSpace(normalizedUid))
            {
                TempData["RfidMessage"] = "Invalid RFID UID.";
                return RedirectToAction("Index", new { courseId });
            }

            bool uidUsedByAnotherStudent = _context.RfidTags
                .Any(t =>
                    t.IsActive &&
                    t.Uid == normalizedUid &&
                    t.StudentId != studentId);

            if (uidUsedByAnotherStudent)
            {
                TempData["RfidMessage"] = "This RFID tag is already assigned to another student.";
                return RedirectToAction("Index", new { courseId });
            }

            var activeTag = _context.RfidTags
                .FirstOrDefault(t => t.StudentId == studentId && t.IsActive);

            if (activeTag == null)
            {
                activeTag = new RfidTag
                {
                    StudentId = studentId,
                    Uid = normalizedUid,
                    IsActive = true,
                    AssignedAt = DateTime.Now
                };

                _context.RfidTags.Add(activeTag);
            }
            else
            {
                activeTag.Uid = normalizedUid;
                activeTag.AssignedAt = DateTime.Now;
                activeTag.DeactivatedAt = null;
                activeTag.IsActive = true;
            }

            _context.SaveChanges();

            TempData["RfidMessage"] = "RFID tag saved successfully.";

            return RedirectToAction("Index", new { courseId });
        }

        [HttpPost]
        public IActionResult RemoveTag(int courseId, int studentId)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .FirstOrDefault(c => c.Id == courseId && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index", "Professor");

            bool studentInCourse = _context.CourseStudents
                .Any(cs => cs.CourseId == courseId && cs.StudentId == studentId);

            if (!studentInCourse)
            {
                TempData["RfidMessage"] = "The selected student is not enrolled in this course.";
                return RedirectToAction("Index", new { courseId });
            }

            var activeTag = _context.RfidTags
                .FirstOrDefault(t => t.StudentId == studentId && t.IsActive);

            if (activeTag != null)
            {
                activeTag.IsActive = false;
                activeTag.DeactivatedAt = DateTime.Now;

                _context.SaveChanges();

                TempData["RfidMessage"] = "RFID tag removed successfully.";
            }

            return RedirectToAction("Index", new { courseId });
        }

        private string NormalizeUid(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return string.Empty;

            var cleaned = new string(
                uid
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToUpperInvariant)
                    .ToArray()
            );

            if (cleaned.Length < 4 || cleaned.Length % 2 != 0)
                return string.Empty;

            var parts = new List<string>();

            for (int i = 0; i < cleaned.Length; i += 2)
            {
                parts.Add(cleaned.Substring(i, 2));
            }

            return string.Join(":", parts);
        }
    }
}