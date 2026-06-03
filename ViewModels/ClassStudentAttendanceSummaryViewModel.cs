namespace SmartAttendance.ViewModels
{
    public class ClassStudentAttendanceSummaryViewModel
    {
        public int CourseId { get; set; }

        public int StudentId { get; set; }

        public string StudentEmail { get; set; } = "";

        public int PresentCount { get; set; }

        public int AbsentCount { get; set; }

        public int RecoveredCount { get; set; }

        public int CountedSessions { get; set; }

        public int EffectiveAbsences { get; set; }

        public int AttendedSessions { get; set; }

        public int AttendancePercent { get; set; }
    }
}