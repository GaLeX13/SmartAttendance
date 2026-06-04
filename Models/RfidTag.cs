namespace SmartAttendance.Models
{
    public class RfidTag
    {
        public int Id { get; set; }

        public string Uid { get; set; } = string.Empty;

        public int StudentId { get; set; }

        public Student Student { get; set; } = null!;

        public bool IsActive { get; set; } = true;

        public DateTime AssignedAt { get; set; } = DateTime.Now;

        public DateTime? DeactivatedAt { get; set; }
    }
}