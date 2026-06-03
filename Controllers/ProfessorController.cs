using Microsoft.AspNetCore.Mvc;
using SmartAttendance.Data;
using SmartAttendance.Models;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartAttendance.ViewModels;
using Microsoft.EntityFrameworkCore;
using SmartAttendance.ViewModels;
using System.Globalization;

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

            int totalRequiredRecords = presentCount + absentCount;
            int effectiveAbsences = Math.Max(0, absentCount - recoveredCount);

            int attendancePercent = totalRequiredRecords == 0
                ? 0
                : (int)Math.Round(((totalRequiredRecords - effectiveAbsences) * 100.0) / totalRequiredRecords);


            var chartGroups = records
    .GroupBy(a => a.Date.Date)
    .OrderBy(g => g.Key)
    .Select(g =>
    {
        int dayPresent = g.Count(x => x.Status == "Present");
        int dayAbsent = g.Count(x => x.Status == "Absent");
        int dayRecovered = g.Count(x => x.Status == "Recovered");

        int dayTotalRequired = dayPresent + dayAbsent;
        int dayEffectiveAbsences = Math.Max(0, dayAbsent - dayRecovered);

        int dayPercent = dayTotalRequired == 0
            ? 0
            : (int)Math.Round(((dayTotalRequired - dayEffectiveAbsences) * 100.0) / dayTotalRequired);

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

            return RedirectToAction("EditAttendance", new { courseId = courseId, studentId = studentId });
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
                .FirstOrDefault(a => a.Id == id && a.CourseId == courseId && a.StudentId == studentId);

            if (record != null)
            {
                _context.AttendanceRecords.Remove(record);
                _context.SaveChanges();
            }

            return RedirectToAction("EditAttendance", new { courseId = courseId, studentId = studentId });
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

            int totalRequiredRecords = presentCount + absentCount;
            int effectiveAbsences = Math.Max(0, absentCount - recoveredCount);

            int attendancePercent = totalRequiredRecords == 0
                ? 0
                : (int)Math.Round(((totalRequiredRecords - effectiveAbsences) * 100.0) / totalRequiredRecords);

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

                int runningTotalRequired = runningPresent + runningAbsent;
                int runningEffectiveAbsences = Math.Max(0, runningAbsent - runningRecovered);

                int runningAttendancePercent = runningTotalRequired == 0
                    ? 0
                    : (int)Math.Round(((runningTotalRequired - runningEffectiveAbsences) * 100.0) / runningTotalRequired);

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
            ViewBag.TotalRequiredRecords = totalRequiredRecords;
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
                        PasswordHash = "" //pt parola in db 
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
        public IActionResult CompleteMissingAttendance(int id, string selectedWeek)
        {
            var professorId = HttpContext.Session.GetInt32("ProfessorId");
            if (professorId == null)
                return RedirectToAction("Login", "ProfessorAuth");

            var course = _context.Courses
                .Include(c => c.CourseStudents)
                .FirstOrDefault(c => c.Id == id && c.ProfessorId == professorId);

            if (course == null)
                return RedirectToAction("Index");

            if (!TryGetWeekRange(selectedWeek, out var weekStart, out var weekEnd))
            {
                TempData["SettingsMessage"] = "Invalid week selected.";
                return RedirectToAction("CourseSettings", new { id = course.Id });
            }

            bool weekHasAttendanceActivity = _context.AttendanceRecords
                .Any(a =>
                    a.CourseId == course.Id &&
                    a.Date >= weekStart &&
                    a.Date <= weekEnd &&
                    a.Status != "Recovered");

            if (!weekHasAttendanceActivity)
            {
                TempData["SettingsMessage"] = "No attendance activity was found for the selected week. No absences were added.";
                return RedirectToAction("CourseSettings", new { id = course.Id });
            }

            int addedAbsences = 0;

            foreach (var courseStudent in course.CourseStudents)
            {
                bool studentHasNormalMark = _context.AttendanceRecords
                    .Any(a =>
                        a.CourseId == course.Id &&
                        a.StudentId == courseStudent.StudentId &&
                        a.Date >= weekStart &&
                        a.Date <= weekEnd &&
                        a.Status != "Recovered");

                if (!studentHasNormalMark)
                {
                    _context.AttendanceRecords.Add(new AttendanceRecord
                    {
                        CourseId = course.Id,
                        StudentId = courseStudent.StudentId,
                        Date = weekEnd,
                        Status = "Absent"
                    });

                    addedAbsences++;
                }
            }

            _context.SaveChanges();

            TempData["SettingsMessage"] = $"{addedAbsences} missing attendance records were marked as absent.";

            return RedirectToAction("CourseSettings", new { id = course.Id });
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

        private List<ClassStudentAttendanceSummaryViewModel>? BuildClassReport(int courseId, int professorId, string sort, string filter, int threshold)
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

    }
}
