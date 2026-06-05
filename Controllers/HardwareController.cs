using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartAttendance.Data;
using SmartAttendance.Models;
using SmartAttendance.ViewModels;

namespace SmartAttendance.Controllers
{
    public class HardwareController : Controller
    {
        private const string DeviceKey = "BOARD01";

        private readonly AppDbContext _context;

        public HardwareController(AppDbContext context)
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

            var activeSession = _context.DeviceSessions
                .Include(s => s.Course)
                .Include(s => s.CurrentStudent)
                .FirstOrDefault(s => s.DeviceKey == DeviceKey && s.IsActive);

            int studentsWithoutTag = _context.CourseStudents
                .Include(cs => cs.Student)
                    .ThenInclude(s => s.RfidTags)
                .Where(cs => cs.CourseId == course.Id)
                .Count(cs => !cs.Student.RfidTags.Any(t => t.IsActive));

            var model = new HardwareControlViewModel
            {
                CourseId = course.Id,
                CourseName = course.Name,
                CourseType = course.IsLab ? "Laboratory" : "Course",
                DeviceKey = DeviceKey,
                StudentsWithoutTag = studentsWithoutTag
            };

            if (activeSession != null)
            {
                model.CurrentMode = activeSession.Mode;
                model.SessionCourseId = activeSession.CourseId;
                model.SessionCourseName = activeSession.Course?.Name ?? "";
                model.CurrentStudentEmail = activeSession.CurrentStudent?.Email ?? "";
                model.DeviceIsBusy = activeSession.CourseId != course.Id && activeSession.Mode != "Idle";
            }

            return View(model);
        }

        [HttpPost]
        public IActionResult StartAssignment(int courseId)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .FirstOrDefault(c => c.Id == courseId && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index", "Professor");

            if (DeviceIsUsedByAnotherCourse(course.Id))
            {
                TempData["HardwareMessage"] = "The device is already active for another course.";
                return RedirectToAction("Index", new { courseId });
            }

            var nextStudent = GetNextStudentWithoutTag(course.Id);

            if (nextStudent == null)
            {
                TempData["HardwareMessage"] = "All enrolled students already have an active RFID tag.";
                return RedirectToAction("Index", new { courseId });
            }

            var session = GetOrCreateSession(course.Id);

            session.Mode = "Assign";
            session.CourseId = course.Id;
            session.CurrentStudentId = nextStudent.StudentId;
            session.SkippedStudentIds = string.Empty;
            session.IsActive = true;
            session.LastSeenAt = DateTime.Now;

            _context.SaveChanges();

            TempData["HardwareMessage"] = "Assignment mode started.";

            return RedirectToAction("Index", new { courseId });
        }

        [HttpPost]
        public IActionResult StartAttendance(int courseId)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .FirstOrDefault(c => c.Id == courseId && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index", "Professor");

            if (DeviceIsUsedByAnotherCourse(course.Id))
            {
                TempData["HardwareMessage"] = "The device is already active for another course.";
                return RedirectToAction("Index", new { courseId });
            }

            var session = GetOrCreateSession(course.Id);

            session.Mode = "Attendance";
            session.CourseId = course.Id;
            session.CurrentStudentId = null;
            session.SkippedStudentIds = string.Empty;
            session.IsActive = true;
            session.LastSeenAt = DateTime.Now;

            _context.SaveChanges();

            TempData["HardwareMessage"] = "Attendance mode started.";

            return RedirectToAction("Index", new { courseId });
        }

        [HttpPost]
        public IActionResult StopDevice(int courseId)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .FirstOrDefault(c => c.Id == courseId && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index", "Professor");

            var session = _context.DeviceSessions
                .FirstOrDefault(s => s.DeviceKey == DeviceKey && s.IsActive);

            if (session != null)
            {
                session.Mode = "Idle";
                session.CourseId = course.Id;
                session.CurrentStudentId = null;
                session.LastSeenAt = DateTime.Now;

                _context.SaveChanges();
            }

            TempData["HardwareMessage"] = "Device stopped.";

            return RedirectToAction("Index", new { courseId });
        }

        private DeviceSession GetOrCreateSession(int courseId)
        {
            var session = _context.DeviceSessions
                .FirstOrDefault(s => s.DeviceKey == DeviceKey && s.IsActive);

            if (session != null)
                return session;

            session = new DeviceSession
            {
                DeviceKey = DeviceKey,
                CourseId = courseId,
                Mode = "Idle",
                IsActive = true,
                StartedAt = DateTime.Now,
                LastSeenAt = DateTime.Now
            };

            _context.DeviceSessions.Add(session);
            _context.SaveChanges();

            return session;
        }

        private bool DeviceIsUsedByAnotherCourse(int courseId)
        {
            return _context.DeviceSessions
                .Any(s =>
                    s.DeviceKey == DeviceKey &&
                    s.IsActive &&
                    s.CourseId != courseId &&
                    s.Mode != "Idle");
        }

        private CourseStudent? GetNextStudentWithoutTag(int courseId)
        {
            return _context.CourseStudents
                .Include(cs => cs.Student)
                    .ThenInclude(s => s.RfidTags)
                .Where(cs => cs.CourseId == courseId)
                .OrderBy(cs => cs.Student.Email)
                .FirstOrDefault(cs => !cs.Student.RfidTags.Any(t => t.IsActive));
        }
    }
}