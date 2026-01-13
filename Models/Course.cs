namespace SmartAttendance.Models
{
    public class Course
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public bool IsLab { get; set; }

        public int StudentCount { get; set; }

        public int AttendancePercent { get; set; }

     
        public int ProfessorId { get; set; }
        public Professor Professor { get; set; } = null!;

    
        public ICollection<CourseStudent> CourseStudents { get; set; }
            = new List<CourseStudent>();
    }
}
