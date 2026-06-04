namespace SmartAttendance.Models
{
    public class Student
    {
        public int Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string? PasswordHash { get; set; }

        public ICollection<CourseStudent> CourseStudents { get; set; } = new List<CourseStudent>();

        public ICollection<RfidTag> RfidTags { get; set; } = new List<RfidTag>();
    }
}