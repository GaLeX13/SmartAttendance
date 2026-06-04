namespace SmartAttendance.ViewModels
{
    public class RfidTagManagerViewModel
    {
        public int CourseId { get; set; }

        public string CourseName { get; set; } = string.Empty;

        public string CourseType { get; set; } = string.Empty;

        public List<RfidStudentTagRowViewModel> Students { get; set; } = new();
    }

    public class RfidStudentTagRowViewModel
    {
        public int StudentId { get; set; }

        public string StudentEmail { get; set; } = string.Empty;

        public string? CurrentUid { get; set; }

        public bool HasActiveTag { get; set; }
    }
}