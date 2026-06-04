namespace SmartAttendance.Models
{
    public class DeviceSession
    {
        public int Id { get; set; }

        public string DeviceKey { get; set; } = string.Empty;

        public int CourseId { get; set; }

        public Course Course { get; set; } = null!;

        public string Mode { get; set; } = "Idle";

        public int? CurrentStudentId { get; set; }

        public Student? CurrentStudent { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime StartedAt { get; set; } = DateTime.Now;

        public DateTime LastSeenAt { get; set; } = DateTime.Now;
    }
}