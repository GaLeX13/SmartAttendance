namespace SmartAttendance.Models
{
    public class Course
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        public bool IsLab { get; set; }

        public int StudentCount { get; set; }

        public int AttendancePercent { get; set; }

        public int ProfessorId { get; set; }

        public Professor Professor { get; set; } = null!;

        public int MinimumAttendanceRequired { get; set; } = 50;

        public string ProfessorContactEmail { get; set; } = "";

        public bool AutoFillAbsencesEnabled { get; set; }

        public int AutoFillDayOfWeek { get; set; }

        public TimeSpan AutoFillTime { get; set; } = new TimeSpan(12, 0, 0);

        public ICollection<CourseStudent> CourseStudents { get; set; } = new List<CourseStudent>();
    }
}