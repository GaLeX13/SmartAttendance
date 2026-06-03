namespace SmartAttendance.Models
{
    public class AttendanceRecord
    {
        public int Id { get; set; }

        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;

        public int StudentId { get; set; }
        public Student Student { get; set; } = null!;

        public DateTime Date { get; set; }

        public string Status { get; set; } = "Absent";
    }
}