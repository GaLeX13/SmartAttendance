using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartAttendance.Data;
using SmartAttendance.Models;
using SmartAttendance.ViewModels;

namespace SmartAttendance.Controllers
{
    public class StudentController : Controller
    {
        private readonly AppDbContext _context;

        public StudentController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var role = HttpContext.Session.GetString("role");
            var email = HttpContext.Session.GetString("student_email");

            if (role != "student" || string.IsNullOrWhiteSpace(email))
                return RedirectToAction("Login", "StudentAuth");

            var student = _context.Students
                .Include(s => s.CourseStudents)
                    .ThenInclude(cs => cs.Course)
                .FirstOrDefault(s => s.Email == email);

            if (student == null)
                return RedirectToAction("Login", "StudentAuth");

            var summaries = new List<StudentCourseSummaryViewModel>();

            foreach (var courseStudent in student.CourseStudents)
            {
                var course = courseStudent.Course;

                var records = _context.AttendanceRecords
                    .Where(a => a.CourseId == course.Id && a.StudentId == student.Id)
                    .ToList();

                int presentCount = records.Count(a => a.Status == "Present");
                int absentCount = records.Count(a => a.Status == "Absent");
                int recoveredCount = records.Count(a => a.Status == "Recovered");

                int countedSessions = presentCount + absentCount;
                int effectiveAbsences = Math.Max(0, absentCount - recoveredCount);
                int attendedSessions = countedSessions - effectiveAbsences;

                int attendancePercent = countedSessions == 0
                    ? 0
                    : (int)Math.Round((attendedSessions * 100.0) / countedSessions);

                summaries.Add(new StudentCourseSummaryViewModel
                {
                    Course = course,
                    AttendedSessions = attendedSessions,
                    CountedSessions = countedSessions,
                    AttendancePercent = attendancePercent
                });
            }

            summaries = summaries
                .OrderBy(s => s.Course.Name)
                .ToList();

            ViewBag.Email = student.Email;

            return View(summaries);
        }

        [HttpGet]
        public IActionResult CourseDetails(int id)
        {
            var role = HttpContext.Session.GetString("role");
            var email = HttpContext.Session.GetString("student_email");

            if (role != "student" || string.IsNullOrWhiteSpace(email))
                return RedirectToAction("Login", "StudentAuth");

            var student = _context.Students
                .FirstOrDefault(s => s.Email == email);

            if (student == null)
                return RedirectToAction("Login", "StudentAuth");

            var course = _context.Courses
                .Include(c => c.CourseStudents)
                .FirstOrDefault(c =>
                    c.Id == id &&
                    c.CourseStudents.Any(cs => cs.StudentId == student.Id));

            if (course == null)
                return RedirectToAction("Index");

            var recordsAscending = _context.AttendanceRecords
                .Where(a => a.CourseId == course.Id && a.StudentId == student.Id)
                .OrderBy(a => a.Date)
                .ToList();

            var recordsDescending = recordsAscending
                .OrderByDescending(a => a.Date)
                .ToList();

            int presentCount = recordsAscending.Count(a => a.Status == "Present");
            int absentCount = recordsAscending.Count(a => a.Status == "Absent");
            int recoveredCount = recordsAscending.Count(a => a.Status == "Recovered");

            int countedSessions = presentCount + absentCount;
            int effectiveAbsences = Math.Max(0, absentCount - recoveredCount);

            int attendancePercent = countedSessions == 0
                ? 0
                : (int)Math.Round(((countedSessions - effectiveAbsences) * 100.0) / countedSessions);

            var barLabels = recordsAscending
                .Select(a => a.Date.ToString("dd.MM"))
                .ToList();

            var barValues = recordsAscending
                .Select(a =>
                {
                    if (a.Status == "Present") return 100;
                    if (a.Status == "Recovered") return 100;
                    return 0;
                })
                .ToList();

            var barStatuses = recordsAscending
                .Select(a => a.Status)
                .ToList();

            var lineLabels = new List<string>();
            var lineValues = new List<int>();

            int runningPresent = 0;
            int runningAbsent = 0;
            int runningRecovered = 0;

            foreach (var record in recordsAscending)
            {
                if (record.Status == "Present")
                    runningPresent++;

                if (record.Status == "Absent")
                    runningAbsent++;

                if (record.Status == "Recovered")
                    runningRecovered++;

                int runningCountedSessions = runningPresent + runningAbsent;
                int runningEffectiveAbsences = Math.Max(0, runningAbsent - runningRecovered);

                int runningAttendancePercent = runningCountedSessions == 0
                    ? 0
                    : (int)Math.Round(((runningCountedSessions - runningEffectiveAbsences) * 100.0) / runningCountedSessions);

                lineLabels.Add(record.Date.ToString("dd.MM"));
                lineValues.Add(runningAttendancePercent);
            }

            ViewBag.CourseId = course.Id;
            ViewBag.CourseName = course.Name;
            ViewBag.CourseType = course.IsLab ? "Laboratory" : "Course";

            ViewBag.StudentEmail = student.Email;
            
            ViewBag.MinimumAttendanceRequired = course.MinimumAttendanceRequired;
            ViewBag.ProfessorContactEmail = course.ProfessorContactEmail;
            ViewBag.AutoFillAbsencesEnabled = course.AutoFillAbsencesEnabled;
            ViewBag.AutoFillDayOfWeek = course.AutoFillDayOfWeek;
            ViewBag.AutoFillTime = course.AutoFillTime;

            ViewBag.PresentCount = presentCount;
            ViewBag.AbsentCount = absentCount;
            ViewBag.RecoveredCount = recoveredCount;
            ViewBag.CountedSessions = countedSessions;
            ViewBag.EffectiveAbsences = effectiveAbsences;
            ViewBag.AttendancePercent = attendancePercent;

            ViewBag.BarChartLabels = barLabels;
            ViewBag.BarChartValues = barValues;
            ViewBag.BarChartStatuses = barStatuses;

            ViewBag.LineChartLabels = lineLabels;
            ViewBag.LineChartValues = lineValues;

            return View(recordsDescending);
        
        }

        [HttpGet]
        public IActionResult CourseDetailsPdf(int id)
        {
            var role = HttpContext.Session.GetString("role");
            var email = HttpContext.Session.GetString("student_email");

            if (role != "student" || string.IsNullOrWhiteSpace(email))
                return RedirectToAction("Login", "StudentAuth");

            var student = _context.Students
                .FirstOrDefault(s => s.Email == email);

            if (student == null)
                return RedirectToAction("Login", "StudentAuth");

            var course = _context.Courses
                .Include(c => c.CourseStudents)
                .FirstOrDefault(c =>
                    c.Id == id &&
                    c.CourseStudents.Any(cs => cs.StudentId == student.Id));

            if (course == null)
                return RedirectToAction("Index");

            var recordsAscending = _context.AttendanceRecords
                .Where(a => a.CourseId == course.Id && a.StudentId == student.Id)
                .OrderBy(a => a.Date)
                .ToList();

            var recordsDescending = recordsAscending
                .OrderByDescending(a => a.Date)
                .ToList();

            int presentCount = recordsAscending.Count(a => a.Status == "Present");
            int absentCount = recordsAscending.Count(a => a.Status == "Absent");
            int recoveredCount = recordsAscending.Count(a => a.Status == "Recovered");

            int countedSessions = presentCount + absentCount;
            int effectiveAbsences = Math.Max(0, absentCount - recoveredCount);

            int attendancePercent = countedSessions == 0
                ? 0
                : (int)Math.Round(((countedSessions - effectiveAbsences) * 100.0) / countedSessions);

            string eligibilityStatus;

            if (countedSessions == 0)
            {
                eligibilityStatus = "Not evaluated yet";
            }
            else if (attendancePercent >= course.MinimumAttendanceRequired)
            {
                eligibilityStatus = "Eligible";
            }
            else
            {
                eligibilityStatus = "Not eligible";
            }

            ViewBag.CourseName = course.Name;
            ViewBag.CourseType = course.IsLab ? "Laboratory" : "Course";
            ViewBag.StudentEmail = student.Email;

            ViewBag.EligibilityStatus = eligibilityStatus;

            ViewBag.PresentCount = presentCount;
            ViewBag.AbsentCount = absentCount;
            ViewBag.RecoveredCount = recoveredCount;
            ViewBag.CountedSessions = countedSessions;
            ViewBag.EffectiveAbsences = effectiveAbsences;
            ViewBag.AttendancePercent = attendancePercent;

            ViewBag.GeneratedAt = DateTime.Now;

            return View("CourseDetailsPdf", recordsDescending);
        }
    }
}