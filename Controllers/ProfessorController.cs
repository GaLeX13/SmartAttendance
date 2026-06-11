using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartAttendance.Data;
using SmartAttendance.Models;
using SmartAttendance.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using SmartAttendance.Services;

namespace SmartAttendance.Controllers
{
    public class ProfessorController : Controller
    {
        private readonly AppDbContext _context;
        private readonly AttendanceCompletionService _attendanceCompletionService;

        public ProfessorController(
            AppDbContext context,
            AttendanceCompletionService attendanceCompletionService)
        {
            _context = context;
            _attendanceCompletionService = attendanceCompletionService;
        }

        public IActionResult Index()
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");

            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var professor = _context.Professors
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == professorId.Value);

            ViewBag.ProfessorDisplayName =
                GetProfessorDisplayName(professor?.Email);

            var courses = _context.Courses
                .AsNoTracking()
                .Include(c => c.CourseStudents)
                .Where(c => c.ProfessorId == professorId.Value)
                .OrderBy(c => c.Name)
                .ToList();

            var records =
                (
                    from attendance in _context.AttendanceRecords.AsNoTracking()
                    join course in _context.Courses.AsNoTracking()
                        on attendance.CourseId equals course.Id
                    where course.ProfessorId == professorId.Value
                    select attendance
                )
                .ToList();

            foreach (var course in courses)
            {
                course.StudentCount = course.CourseStudents.Count;

                var courseRecords = records
                    .Where(a => a.CourseId == course.Id)
                    .ToList();

                int presentCount = courseRecords.Count(
                    a => a.Status == "Present");

                int absentCount = courseRecords.Count(
                    a => a.Status == "Absent");

                int recoveredCount = courseRecords.Count(
                    a => a.Status == "Recovered");

                int countedSessions =
                    presentCount + absentCount;

                int effectiveAbsences =
                    Math.Max(0, absentCount - recoveredCount);

                course.AttendancePercent =
                    countedSessions == 0
                        ? 0
                        : (int)Math.Round(
                            ((countedSessions - effectiveAbsences) * 100.0)
                            / countedSessions);
            }

            return View(courses);
        }

        public IActionResult Course(int id)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .Include(c => c.CourseStudents)
                    .ThenInclude(cs => cs.Student)
                .FirstOrDefault(c => c.Id == id && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index");

            var records = _context.AttendanceRecords
                .Where(a => a.CourseId == id)
                .ToList();

            int presentCount = records.Count(a => a.Status == "Present");
            int absentCount = records.Count(a => a.Status == "Absent");
            int recoveredCount = records.Count(a => a.Status == "Recovered");

            int countedSessions = presentCount + absentCount;
            int effectiveAbsences = Math.Max(0, absentCount - recoveredCount);

            int attendancePercent = countedSessions == 0
                ? 0
                : (int)Math.Round(((countedSessions - effectiveAbsences) * 100.0) / countedSessions);

            var chartGroups = records
                .GroupBy(a => a.Date.Date)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    int dayPresent = g.Count(x => x.Status == "Present");
                    int dayAbsent = g.Count(x => x.Status == "Absent");
                    int dayRecovered = g.Count(x => x.Status == "Recovered");

                    int dayCountedSessions = dayPresent + dayAbsent;
                    int dayEffectiveAbsences = Math.Max(0, dayAbsent - dayRecovered);

                    int dayPercent = dayCountedSessions == 0
                        ? 0
                        : (int)Math.Round(((dayCountedSessions - dayEffectiveAbsences) * 100.0) / dayCountedSessions);

                    return new
                    {
                        Label = g.Key.ToString("dd.MM"),
                        Percent = dayPercent
                    };
                })
                .ToList();

            ViewBag.PresentCount = presentCount;
            ViewBag.AbsentCount = absentCount;
            ViewBag.RecoveredCount = recoveredCount;
            ViewBag.AttendancePercent = attendancePercent;
            ViewBag.AttendanceRecords = records;

            ViewBag.ChartLabels = chartGroups.Select(x => x.Label).ToList();
            ViewBag.ChartValues = chartGroups.Select(x => x.Percent).ToList();

            return View(course);
        }

        [HttpPost]
        public IActionResult AddCourse(string name, string type)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            if (string.IsNullOrWhiteSpace(name))
                return RedirectToAction("Index");

            var professor = _context.Professors
                .FirstOrDefault(p => p.Id == professorId.Value);

            var course = new Course
            {
                Name = name.Trim(),
                IsLab = type == "lab",
                ProfessorId = professorId.Value,
                StudentCount = 0,
                AttendancePercent = 0,
                MinimumAttendanceRequired = 50,
                ProfessorContactEmail = professor?.Email ?? "",
                AutoFillAbsencesEnabled = false,
                AutoFillDayOfWeek = 0,
                AutoFillTime = new TimeSpan(12, 0, 0)
            };

            _context.Courses.Add(course);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult CourseSettings(int id)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .FirstOrDefault(c => c.Id == id && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index");

            return View(course);
        }

        [HttpPost]
        public IActionResult CourseSettings(
            int id,
            int minimumAttendanceRequired,
            string professorContactEmail,
            string? autoFillAbsencesEnabled,
            int autoFillDayOfWeek,
            string autoFillTime)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .FirstOrDefault(c => c.Id == id && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index");

            minimumAttendanceRequired = Math.Clamp(minimumAttendanceRequired, 0, 100);

            if (!TimeSpan.TryParse(autoFillTime, out var parsedTime))
                parsedTime = new TimeSpan(12, 0, 0);

            course.MinimumAttendanceRequired = minimumAttendanceRequired;
            course.ProfessorContactEmail = (professorContactEmail ?? "").Trim();
            course.AutoFillAbsencesEnabled = autoFillAbsencesEnabled == "true";
            course.AutoFillDayOfWeek = autoFillDayOfWeek;
            course.AutoFillTime = parsedTime;

            _context.SaveChanges();

            return RedirectToAction("Course", new { id = course.Id });
        }

        [HttpGet]
        public IActionResult EditAttendance(int courseId, int studentId)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .FirstOrDefault(c => c.Id == courseId && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index");

            var student = _context.Students
                .FirstOrDefault(s => s.Id == studentId);

            if (student == null)
                return RedirectToAction("Course", new { id = courseId });

            var isLinked = _context.CourseStudents
                .Any(cs => cs.CourseId == courseId && cs.StudentId == studentId);

            if (!isLinked)
                return RedirectToAction("Course", new { id = courseId });

            var records = _context.AttendanceRecords
                .Where(a => a.CourseId == courseId && a.StudentId == studentId)
                .OrderByDescending(a => a.Date)
                .ToList();

            ViewBag.CourseId = course.Id;
            ViewBag.CourseName = course.Name;
            ViewBag.StudentId = student.Id;
            ViewBag.StudentEmail = student.Email;

            return View(records);
        }

        [HttpPost]
        public IActionResult SaveAttendance(int courseId, int studentId, DateTime date, string status)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .FirstOrDefault(c => c.Id == courseId && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index");

            var isLinked = _context.CourseStudents
                .Any(cs => cs.CourseId == courseId && cs.StudentId == studentId);

            if (!isLinked)
                return RedirectToAction("Course", new { id = courseId });

            status = (status ?? "").Trim();

            if (status != "Present" && status != "Absent" && status != "Recovered")
                status = "Absent";

            var cleanDate = date.Date;

            var record = _context.AttendanceRecords
                .FirstOrDefault(a =>
                    a.CourseId == courseId &&
                    a.StudentId == studentId &&
                    a.Date == cleanDate);

            if (record == null)
            {
                record = new AttendanceRecord
                {
                    CourseId = courseId,
                    StudentId = studentId,
                    Date = cleanDate,
                    Status = status
                };

                _context.AttendanceRecords.Add(record);
            }
            else
            {
                record.Status = status;
            }

            _context.SaveChanges();

            return RedirectToAction("EditAttendance", new { courseId, studentId });
        }

        [HttpPost]
        public IActionResult DeleteAttendance(int id, int courseId, int studentId)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .FirstOrDefault(c => c.Id == courseId && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index");

            var record = _context.AttendanceRecords
                .FirstOrDefault(a =>
                    a.Id == id &&
                    a.CourseId == courseId &&
                    a.StudentId == studentId);

            if (record != null)
            {
                _context.AttendanceRecords.Remove(record);
                _context.SaveChanges();
            }

            return RedirectToAction("EditAttendance", new { courseId, studentId });
        }

        [HttpGet]
        public IActionResult StudentAttendance(int courseId, int studentId)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var records = PrepareStudentAttendanceData(courseId, studentId, professorId.Value);

            if (records == null)
                return RedirectToAction("Index");

            return View(records);
        }

        [HttpGet]
        public IActionResult StudentAttendancePdf(int courseId, int studentId)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var records = PrepareStudentAttendanceData(courseId, studentId, professorId.Value);

            if (records == null)
                return RedirectToAction("Index");

            return View("StudentAttendancePdf", records);
        }

        [HttpGet]
        public IActionResult ClassReport(int courseId, string sort = "alphabetical", string filter = "all", int threshold = 50)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var students = BuildClassReport(courseId, professorId.Value, sort, filter, threshold);

            if (students == null)
                return RedirectToAction("Index");

            return View(students);
        }

        [HttpGet]
        public IActionResult ClassReportPdf(int courseId, string sort = "alphabetical", string filter = "all", int threshold = 50)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var students = BuildClassReport(courseId, professorId.Value, sort, filter, threshold);

            if (students == null)
                return RedirectToAction("Index");

            return View("ClassReportPdf", students);
        }

        [HttpGet]
        public IActionResult ClassExamSheetPdf(int courseId, string sort = "alphabetical", string filter = "all", int threshold = 50)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var students = BuildClassReport(courseId, professorId.Value, sort, filter, threshold);

            if (students == null)
                return RedirectToAction("Index");

            return View("ClassExamSheetPdf", students);
        }

        [HttpPost]
        public IActionResult DeleteCourse(int id)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .FirstOrDefault(c => c.Id == id && c.ProfessorId == professorId);

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
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .FirstOrDefault(c => c.Id == courseId && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index");

            ViewBag.CourseId = course.Id;
            ViewBag.CourseName = course.Name;

            return View();
        }

        [HttpPost]
        public IActionResult AddStudents(int courseId, string emails)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .FirstOrDefault(c => c.Id == courseId && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index");

            if (string.IsNullOrWhiteSpace(emails))
                return RedirectToAction("Course", new { id = courseId });

            var lines = emails
                .Split('\n')
                .Select(e => e.Trim().ToLowerInvariant())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct()
                .ToList();

            foreach (var email in lines)
            {
                if (!IsValidEmail(email))
                    continue;

                var student = _context.Students
                    .FirstOrDefault(s => s.Email == email);

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

            return RedirectToAction("Course", new { id = courseId });
        }

        [HttpPost]
        public async Task<IActionResult> CompleteMissingAttendance(
    int id,
    string selectedWeek,
    CancellationToken cancellationToken)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");

            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = await _context.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.Id == id &&
                         c.ProfessorId == professorId.Value,
                    cancellationToken);

            if (course == null)
                return RedirectToAction("Index");

            if (!TryGetWeekRange(
                selectedWeek,
                out var weekStart,
                out _))
            {
                TempData["HardwareMessage"] =
                    "Invalid week selected.";

                return RedirectToAction(
                    "Index",
                    "Hardware",
                    new { courseId = course.Id });
            }

            var result =
                await _attendanceCompletionService.CompleteWeekAsync(
                    course.Id,
                    weekStart,
                    cancellationToken);

            if (!result.HasAttendanceActivity)
            {
                TempData["HardwareMessage"] =
                    "No Present or Recovered attendance activity was found for the selected week. No absences were added.";
            }
            else if (result.AddedAbsences == 0)
            {
                TempData["HardwareMessage"] =
                    "Attendance completion finished. No missing attendance records were found.";
            }
            else
            {
                TempData["HardwareMessage"] =
                    $"{result.AddedAbsences} missing attendance records were marked as absent.";
            }

            return RedirectToAction(
                "Index",
                "Hardware",
                new { courseId = course.Id });
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

        private List<AttendanceRecord>? PrepareStudentAttendanceData(int courseId, int studentId, int professorId)
        {
            var course = _context.Courses
                .FirstOrDefault(c => c.Id == courseId && c.ProfessorId == professorId);

            if (course == null)
                return null;

            var student = _context.Students
                .FirstOrDefault(s => s.Id == studentId);

            if (student == null)
                return null;

            var isLinked = _context.CourseStudents
                .Any(cs => cs.CourseId == courseId && cs.StudentId == studentId);

            if (!isLinked)
                return null;

            var recordsAscending = _context.AttendanceRecords
                .Where(a => a.CourseId == courseId && a.StudentId == studentId)
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
                    if (a.Status == "Present")
                        return 100;

                    if (a.Status == "Recovered")
                        return 100;

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

            ViewBag.StudentId = student.Id;
            ViewBag.StudentEmail = student.Email;

            ViewBag.PresentCount = presentCount;
            ViewBag.AbsentCount = absentCount;
            ViewBag.RecoveredCount = recoveredCount;
            ViewBag.TotalRequiredRecords = countedSessions;
            ViewBag.EffectiveAbsences = effectiveAbsences;
            ViewBag.AttendancePercent = attendancePercent;

            ViewBag.BarChartLabels = barLabels;
            ViewBag.BarChartValues = barValues;
            ViewBag.BarChartStatuses = barStatuses;

            ViewBag.LineChartLabels = lineLabels;
            ViewBag.LineChartValues = lineValues;

            ViewBag.GeneratedAt = DateTime.Now;

            return recordsDescending;
        }

        private List<ClassStudentAttendanceSummaryViewModel>? BuildClassReport(
            int courseId,
            int professorId,
            string sort,
            string filter,
            int threshold)
        {
            threshold = Math.Clamp(threshold, 0, 100);

            var professor = _context.Professors
                .FirstOrDefault(p => p.Id == professorId);

            var course = _context.Courses
                .Include(c => c.CourseStudents)
                    .ThenInclude(cs => cs.Student)
                .FirstOrDefault(c => c.Id == courseId && c.ProfessorId == professorId);

            if (course == null)
                return null;

            var students = new List<ClassStudentAttendanceSummaryViewModel>();

            foreach (var courseStudent in course.CourseStudents)
            {
                var student = courseStudent.Student;

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

                students.Add(new ClassStudentAttendanceSummaryViewModel
                {
                    CourseId = course.Id,
                    StudentId = student.Id,
                    StudentEmail = student.Email,
                    PresentCount = presentCount,
                    AbsentCount = absentCount,
                    RecoveredCount = recoveredCount,
                    CountedSessions = countedSessions,
                    EffectiveAbsences = effectiveAbsences,
                    AttendedSessions = attendedSessions,
                    AttendancePercent = attendancePercent
                });
            }

            if (filter == "above")
            {
                students = students
                    .Where(s => s.AttendancePercent >= threshold)
                    .ToList();
            }

            if (filter == "below")
            {
                students = students
                    .Where(s => s.AttendancePercent <= threshold)
                    .ToList();
            }

            if (sort == "attendanceAsc")
            {
                students = students
                    .OrderBy(s => s.AttendancePercent)
                    .ThenBy(s => s.StudentEmail)
                    .ToList();
            }
            else if (sort == "attendanceDesc")
            {
                students = students
                    .OrderByDescending(s => s.AttendancePercent)
                    .ThenBy(s => s.StudentEmail)
                    .ToList();
            }
            else
            {
                students = students
                    .OrderBy(s => s.StudentEmail)
                    .ToList();
            }

            int classAverage = students.Count == 0
                ? 0
                : (int)Math.Round(students.Average(s => s.AttendancePercent));

            string sortLabel = sort switch
            {
                "attendanceAsc" => "Attendance ascending",
                "attendanceDesc" => "Attendance descending",
                _ => "Alphabetical"
            };

            string filterLabel = filter switch
            {
                "above" => $"Attendance at least {threshold}%",
                "below" => $"Attendance at most {threshold}%",
                _ => "All students"
            };

            ViewBag.CourseId = course.Id;
            ViewBag.CourseName = course.Name;
            ViewBag.CourseType = course.IsLab ? "Laboratory" : "Course";
            ViewBag.ProfessorName = professor?.FullName ?? "";
            ViewBag.TotalStudents = course.CourseStudents.Count;
            ViewBag.DisplayedStudents = students.Count;
            ViewBag.ClassAverage = classAverage;

            ViewBag.Sort = sort;
            ViewBag.Filter = filter;
            ViewBag.Threshold = threshold;
            ViewBag.SortLabel = sortLabel;
            ViewBag.FilterLabel = filterLabel;
            ViewBag.GeneratedAt = DateTime.Now;

            return students;
        }

        private bool IsValidEmail(string email)
        {
            return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }

        private bool TryGetWeekRange(string selectedWeek, out DateTime weekStart, out DateTime weekEnd)
        {
            weekStart = DateTime.MinValue;
            weekEnd = DateTime.MinValue;

            if (string.IsNullOrWhiteSpace(selectedWeek))
                return false;

            var parts = selectedWeek.Split("-W");

            if (parts.Length != 2)
                return false;

            if (!int.TryParse(parts[0], out int year))
                return false;

            if (!int.TryParse(parts[1], out int week))
                return false;

            try
            {
                weekStart = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday).Date;
                weekEnd = weekStart.AddDays(6);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetProfessorDisplayName(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return "Professor";

            var localPart = email.Split('@')[0];

            localPart = localPart
                .Replace(".", " ")
                .Replace("_", " ")
                .Replace("-", " ");

            var cleaned = new string(localPart.Where(c => !char.IsDigit(c)).ToArray()).Trim();

            if (string.IsNullOrWhiteSpace(cleaned))
                return "Professor";

            var words = cleaned
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant());

            return string.Join(" ", words);
        }
    }
}