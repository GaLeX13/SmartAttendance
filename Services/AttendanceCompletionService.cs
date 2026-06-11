using Microsoft.EntityFrameworkCore;
using SmartAttendance.Data;
using SmartAttendance.Models;

namespace SmartAttendance.Services
{
    public class AttendanceCompletionResult
    {
        public bool HasAttendanceActivity { get; set; }

        public int AddedAbsences { get; set; }

        public DateTime WeekStart { get; set; }

        public DateTime AbsenceDate { get; set; }
    }

    public class AttendanceCompletionService
    {
        private readonly AppDbContext _context;

        public AttendanceCompletionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AttendanceCompletionResult> CompleteWeekAsync(
            int courseId,
            DateTime dateInWeek,
            CancellationToken cancellationToken = default)
        {
            DateTime weekStart = GetWeekStart(dateInWeek);
            DateTime weekEndExclusive = weekStart.AddDays(7);
            DateTime absenceDate = weekStart.AddDays(6).Date;

            var weeklyRecords = await _context.AttendanceRecords
                .AsNoTracking()
                .Where(record =>
                    record.CourseId == courseId &&
                    record.Date >= weekStart &&
                    record.Date < weekEndExclusive)
                .Select(record => new
                {
                    record.StudentId,
                    record.Status
                })
                .ToListAsync(cancellationToken);

            bool hasAttendanceActivity = weeklyRecords.Any(record =>
                record.Status == "Present" ||
                record.Status == "Recovered");

            if (!hasAttendanceActivity)
            {
                return new AttendanceCompletionResult
                {
                    HasAttendanceActivity = false,
                    AddedAbsences = 0,
                    WeekStart = weekStart,
                    AbsenceDate = absenceDate
                };
            }

            var enrolledStudentIds = await _context.CourseStudents
                .AsNoTracking()
                .Where(courseStudent => courseStudent.CourseId == courseId)
                .Select(courseStudent => courseStudent.StudentId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var studentsWithExistingRecords = weeklyRecords
                .Select(record => record.StudentId)
                .ToHashSet();

            var missingStudentIds = enrolledStudentIds
                .Where(studentId =>
                    !studentsWithExistingRecords.Contains(studentId))
                .ToList();

            if (missingStudentIds.Count > 0)
            {
                var absenceRecords = missingStudentIds
                    .Select(studentId => new AttendanceRecord
                    {
                        CourseId = courseId,
                        StudentId = studentId,
                        Date = absenceDate,
                        Status = "Absent"
                    })
                    .ToList();

                await _context.AttendanceRecords.AddRangeAsync(
                    absenceRecords,
                    cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
            }

            return new AttendanceCompletionResult
            {
                HasAttendanceActivity = true,
                AddedAbsences = missingStudentIds.Count,
                WeekStart = weekStart,
                AbsenceDate = absenceDate
            };
        }

        public static DateTime GetWeekStart(DateTime date)
        {
            DateTime normalizedDate = date.Date;

            int daysSinceMonday =
                ((int)normalizedDate.DayOfWeek -
                 (int)DayOfWeek.Monday + 7) % 7;

            return normalizedDate.AddDays(-daysSinceMonday);
        }
    }
}