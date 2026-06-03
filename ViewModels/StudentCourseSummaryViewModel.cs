using SmartAttendance.Models;

namespace SmartAttendance.ViewModels
{
    public class StudentCourseSummaryViewModel
    {
        public Course Course { get; set; } = null!;

        public int AttendedSessions { get; set; }

        public int CountedSessions { get; set; }

        public int AttendancePercent { get; set; }
    }
}