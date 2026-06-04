namespace SmartAttendance.ViewModels
{
    public class HardwareControlViewModel
    {
        public int CourseId { get; set; }

        public string CourseName { get; set; } = string.Empty;

        public string CourseType { get; set; } = string.Empty;

        public string DeviceKey { get; set; } = string.Empty;

        public string CurrentMode { get; set; } = "Idle";

        public int? SessionCourseId { get; set; }

        public string SessionCourseName { get; set; } = string.Empty;

        public string CurrentStudentEmail { get; set; } = string.Empty;

        public int StudentsWithoutTag { get; set; }

        public bool DeviceIsBusy { get; set; }
    }
}