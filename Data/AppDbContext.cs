using Microsoft.EntityFrameworkCore;
using SmartAttendance.Models;

namespace SmartAttendance.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Course> Courses => Set<Course>();
        public DbSet<Professor> Professors => Set<Professor>();
        public DbSet<Student> Students => Set<Student>();
        public DbSet<CourseStudent> CourseStudents => Set<CourseStudent>();
        public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CourseStudent>()
                .HasKey(cs => new { cs.CourseId, cs.StudentId });

            modelBuilder.Entity<AttendanceRecord>()
                .HasIndex(a => new { a.CourseId, a.StudentId, a.Date })
                .IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }
}
