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

        public DbSet<RfidTag> RfidTags => Set<RfidTag>();

        public DbSet<DeviceSession> DeviceSessions => Set<DeviceSession>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CourseStudent>()
                .HasKey(cs => new { cs.CourseId, cs.StudentId });

            modelBuilder.Entity<AttendanceRecord>()
                .HasIndex(a => new { a.CourseId, a.StudentId, a.Date })
                .IsUnique();

            modelBuilder.Entity<RfidTag>()
                .HasIndex(t => t.Uid)
                .IsUnique()
                .HasFilter("[IsActive] = 1");

            modelBuilder.Entity<RfidTag>()
                .HasIndex(t => t.StudentId)
                .IsUnique()
                .HasFilter("[IsActive] = 1");

            modelBuilder.Entity<RfidTag>()
                .HasOne(t => t.Student)
                .WithMany(s => s.RfidTags)
                .HasForeignKey(t => t.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DeviceSession>()
                .HasIndex(s => s.DeviceKey)
                .IsUnique()
                .HasFilter("[IsActive] = 1");

            modelBuilder.Entity<DeviceSession>()
                .HasOne(s => s.Course)
                .WithMany()
                .HasForeignKey(s => s.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DeviceSession>()
                .HasOne(s => s.CurrentStudent)
                .WithMany()
                .HasForeignKey(s => s.CurrentStudentId)
                .OnDelete(DeleteBehavior.NoAction);

            base.OnModelCreating(modelBuilder);
        }
    }
}