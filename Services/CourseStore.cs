using SmartAttendance.Models;

namespace SmartAttendance.Services
{
    public static class CourseStore
    {
        private static readonly List<Course> _courses = new();
        private static int _id = 1;

        public static List<Course> GetAll() => _courses;

        public static void Add(string name, bool isLab)
        {
            _courses.Add(new Course
            {
                Id = _id++,
                Name = name.Trim(),
                IsLab = isLab,
                StudentCount = 0,
                AttendancePercent = 0
            });
        }

        public static void Delete(int id)
        {
            var c = _courses.FirstOrDefault(x => x.Id == id);
            if (c != null) _courses.Remove(c);
        }
    }
}
