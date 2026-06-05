using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartAttendance.Data;
using SmartAttendance.Models;

namespace SmartAttendance.Controllers
{
    [ApiController]
    [Route("api/device")]
    public class DeviceApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DeviceApiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("state")]
        public IActionResult State(string deviceKey)
        {
            if (string.IsNullOrWhiteSpace(deviceKey))
            {
                return Ok(new
                {
                    success = false,
                    mode = "Idle",
                    message = "Missing device key",
                    displayLine1 = "Device error",
                    displayLine2 = "Missing key"
                });
            }

            var session = _context.DeviceSessions
                .Include(s => s.Course)
                .Include(s => s.CurrentStudent)
                .FirstOrDefault(s => s.DeviceKey == deviceKey && s.IsActive);

            if (session == null || session.Mode == "Idle")
            {
                return Ok(new
                {
                    success = true,
                    mode = "Idle",
                    message = "Device is idle",
                    displayLine1 = "Device ready",
                    displayLine2 = "Waiting..."
                });
            }

            session.LastSeenAt = DateTime.Now;
            _context.SaveChanges();

            if (session.Mode == "Assign")
            {
                if (session.CurrentStudentId == null)
                {
                    var skippedIds = GetSkippedStudentIds(session);
                    var nextStudent = GetNextStudentWithoutTag(session.CourseId, null, skippedIds);

                    if (nextStudent == null)
                    {
                        session.Mode = "Idle";
                        session.CurrentStudentId = null;
                        _context.SaveChanges();

                        return Ok(new
                        {
                            success = true,
                            mode = "Idle",
                            courseId = session.CourseId,
                            courseName = session.Course.Name,
                            message = "No more students available in this assignment session",
                            displayLine1 = "Assignment done",
                            displayLine2 = "No more students"
                        });
                    }

                    session.CurrentStudentId = nextStudent.StudentId;
                    _context.SaveChanges();
                    session.CurrentStudent = nextStudent.Student;
                }

                return Ok(new
                {
                    success = true,
                    mode = "Assign",
                    courseId = session.CourseId,
                    courseName = session.Course.Name,
                    studentId = session.CurrentStudentId,
                    studentEmail = session.CurrentStudent?.Email ?? "",
                    message = "Waiting for RFID tag assignment",
                    displayLine1 = "Assign tag to",
                    displayLine2 = Shorten(session.CurrentStudent?.Email ?? "")
                });
            }

            if (session.Mode == "Attendance")
            {
                return Ok(new
                {
                    success = true,
                    mode = "Attendance",
                    courseId = session.CourseId,
                    courseName = session.Course.Name,
                    message = "Waiting for attendance scan",
                    displayLine1 = "Scan tag",
                    displayLine2 = "for attendance"
                });
            }

            return Ok(new
            {
                success = true,
                mode = "Idle",
                message = "Unknown mode",
                displayLine1 = "Device ready",
                displayLine2 = "Waiting..."
            });
        }

        [HttpPost("assign-scan")]
        public IActionResult AssignScan([FromBody] DeviceScanRequest request)
        {
            var session = _context.DeviceSessions
                .Include(s => s.Course)
                .Include(s => s.CurrentStudent)
                .FirstOrDefault(s => s.DeviceKey == request.DeviceKey && s.IsActive);

            if (session == null || session.Mode != "Assign")
            {
                return Ok(new
                {
                    success = false,
                    message = "Device is not in assignment mode",
                    displayLine1 = "Wrong mode",
                    displayLine2 = "Not assign"
                });
            }

            if (session.CurrentStudentId == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "No student selected for assignment",
                    displayLine1 = "No student",
                    displayLine2 = "selected"
                });
            }

            var normalizedUid = NormalizeUid(request.Uid);

            if (string.IsNullOrWhiteSpace(normalizedUid))
            {
                return Ok(new
                {
                    success = false,
                    message = "Invalid RFID UID",
                    displayLine1 = "Invalid tag",
                    displayLine2 = "Try again"
                });
            }

            int currentStudentId = session.CurrentStudentId.Value;
            string assignedStudentEmail = session.CurrentStudent?.Email ?? "";

            bool uidAlreadyUsed = _context.RfidTags
                .Any(t => t.IsActive && t.Uid == normalizedUid);

            if (uidAlreadyUsed)
            {
                return Ok(new
                {
                    success = false,
                    message = "This RFID tag is already assigned",
                    displayLine1 = "Tag already",
                    displayLine2 = "assigned"
                });
            }

            bool studentAlreadyHasTag = _context.RfidTags
                .Any(t => t.IsActive && t.StudentId == currentStudentId);

            var skippedIds = GetSkippedStudentIds(session);

            if (studentAlreadyHasTag)
            {
                var nextStudentAfterExistingTag = GetNextStudentWithoutTag(session.CourseId, currentStudentId, skippedIds);

                session.CurrentStudentId = nextStudentAfterExistingTag?.StudentId;
                session.LastSeenAt = DateTime.Now;

                if (nextStudentAfterExistingTag == null)
                {
                    session.Mode = "Idle";
                }

                _context.SaveChanges();

                return Ok(new
                {
                    success = false,
                    mode = session.Mode,
                    message = "Student already has an active RFID tag",
                    displayLine1 = "Student has",
                    displayLine2 = "a tag"
                });
            }

            var tag = new RfidTag
            {
                Uid = normalizedUid,
                StudentId = currentStudentId,
                IsActive = true,
                AssignedAt = DateTime.Now
            };

            _context.RfidTags.Add(tag);
            _context.SaveChanges();

            var nextStudent = GetNextStudentWithoutTag(session.CourseId, currentStudentId, skippedIds);

            session.CurrentStudentId = nextStudent?.StudentId;
            session.LastSeenAt = DateTime.Now;

            if (nextStudent == null)
            {
                session.Mode = "Idle";
            }

            _context.SaveChanges();

            return Ok(new
            {
                success = true,
                message = "RFID tag assigned successfully",
                assignedTo = assignedStudentEmail,
                uid = normalizedUid,
                nextStudentEmail = nextStudent?.Student.Email ?? "",
                mode = session.Mode,
                displayLine1 = "Tag assigned",
                displayLine2 = Shorten(assignedStudentEmail)
            });
        }

        [HttpPost("attendance-scan")]
        public IActionResult AttendanceScan([FromBody] DeviceScanRequest request)
        {
            var session = _context.DeviceSessions
                .Include(s => s.Course)
                .FirstOrDefault(s => s.DeviceKey == request.DeviceKey && s.IsActive);

            if (session == null || session.Mode != "Attendance")
            {
                return Ok(new
                {
                    success = false,
                    message = "Device is not in attendance mode",
                    displayLine1 = "Wrong mode",
                    displayLine2 = "Not attendance"
                });
            }

            var normalizedUid = NormalizeUid(request.Uid);

            if (string.IsNullOrWhiteSpace(normalizedUid))
            {
                return Ok(new
                {
                    success = false,
                    message = "Invalid RFID UID",
                    displayLine1 = "Invalid tag",
                    displayLine2 = "Try again"
                });
            }

            var tag = _context.RfidTags
                .Include(t => t.Student)
                .FirstOrDefault(t => t.IsActive && t.Uid == normalizedUid);

            if (tag == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "Unknown RFID tag",
                    displayLine1 = "Unknown tag",
                    displayLine2 = "Access denied"
                });
            }

            bool studentInCourse = _context.CourseStudents
                .Any(cs => cs.CourseId == session.CourseId && cs.StudentId == tag.StudentId);

            if (!studentInCourse)
            {
                return Ok(new
                {
                    success = false,
                    message = "Student is not enrolled in this course",
                    studentEmail = tag.Student.Email,
                    displayLine1 = "Not in course",
                    displayLine2 = Shorten(tag.Student.Email)
                });
            }

            var today = DateTime.Today;

            var existingRecord = _context.AttendanceRecords
                .FirstOrDefault(a =>
                    a.CourseId == session.CourseId &&
                    a.StudentId == tag.StudentId &&
                    a.Date == today);

            if (existingRecord != null)
            {
                return Ok(new
                {
                    success = false,
                    message = $"Student already has an attendance mark: {existingRecord.Status}",
                    studentEmail = tag.Student.Email,
                    status = existingRecord.Status,
                    displayLine1 = "Already marked",
                    displayLine2 = existingRecord.Status
                });
            }

            var record = new AttendanceRecord
            {
                CourseId = session.CourseId,
                StudentId = tag.StudentId,
                Date = today,
                Status = "Present"
            };

            _context.AttendanceRecords.Add(record);

            session.LastSeenAt = DateTime.Now;

            _context.SaveChanges();

            return Ok(new
            {
                success = true,
                message = "Attendance marked successfully",
                studentEmail = tag.Student.Email,
                status = "Present",
                displayLine1 = "Present",
                displayLine2 = Shorten(tag.Student.Email)
            });
        }

        [HttpPost("skip")]
        public IActionResult Skip([FromBody] DeviceSkipRequest request)
        {
            var session = _context.DeviceSessions
                .Include(s => s.Course)
                .Include(s => s.CurrentStudent)
                .FirstOrDefault(s => s.DeviceKey == request.DeviceKey && s.IsActive);

            if (session == null || session.Mode != "Assign")
            {
                return Ok(new
                {
                    success = false,
                    message = "Device is not in assignment mode",
                    displayLine1 = "Wrong mode",
                    displayLine2 = "Cannot skip"
                });
            }

            if (session.CurrentStudentId == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "No student selected",
                    displayLine1 = "No student",
                    displayLine2 = "selected"
                });
            }

            int skippedStudentId = session.CurrentStudentId.Value;
            string skippedEmail = session.CurrentStudent?.Email ?? "";

            AddSkippedStudentId(session, skippedStudentId);

            var skippedIds = GetSkippedStudentIds(session);
            var nextStudent = GetNextStudentWithoutTag(session.CourseId, skippedStudentId, skippedIds);

            if (nextStudent == null)
            {
                session.Mode = "Idle";
                session.CurrentStudentId = null;
                session.LastSeenAt = DateTime.Now;

                _context.SaveChanges();

                return Ok(new
                {
                    success = true,
                    mode = "Idle",
                    message = "No more students available in this assignment session",
                    skippedStudent = skippedEmail,
                    displayLine1 = "No more",
                    displayLine2 = "students"
                });
            }

            session.CurrentStudentId = nextStudent.StudentId;
            session.LastSeenAt = DateTime.Now;

            _context.SaveChanges();

            return Ok(new
            {
                success = true,
                mode = "Assign",
                message = "Student skipped",
                skippedStudent = skippedEmail,
                nextStudentEmail = nextStudent.Student.Email,
                displayLine1 = "Next student",
                displayLine2 = Shorten(nextStudent.Student.Email)
            });
        }

        private CourseStudent? GetNextStudentWithoutTag(int courseId, int? afterStudentId = null, List<int>? skippedStudentIds = null)
        {
            var skippedIds = skippedStudentIds ?? new List<int>();

            var students = _context.CourseStudents
                .Include(cs => cs.Student)
                .Where(cs => cs.CourseId == courseId)
                .OrderBy(cs => cs.Student.Email)
                .ToList();

            if (!students.Any())
                return null;

            int startIndex = 0;

            if (afterStudentId != null)
            {
                int foundIndex = students.FindIndex(cs => cs.StudentId == afterStudentId.Value);

                if (foundIndex >= 0)
                {
                    startIndex = foundIndex + 1;
                }
            }

            for (int i = 0; i < students.Count; i++)
            {
                int index = (startIndex + i) % students.Count;
                var candidate = students[index];

                if (skippedIds.Contains(candidate.StudentId))
                    continue;

                bool hasActiveTag = _context.RfidTags
                    .Any(t => t.StudentId == candidate.StudentId && t.IsActive);

                if (!hasActiveTag)
                    return candidate;
            }

            return null;
        }

        private List<int> GetSkippedStudentIds(DeviceSession session)
        {
            if (string.IsNullOrWhiteSpace(session.SkippedStudentIds))
                return new List<int>();

            return session.SkippedStudentIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(value =>
                {
                    bool parsed = int.TryParse(value, out int id);
                    return parsed ? id : 0;
                })
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }

        private void AddSkippedStudentId(DeviceSession session, int studentId)
        {
            var skippedIds = GetSkippedStudentIds(session);

            if (!skippedIds.Contains(studentId))
            {
                skippedIds.Add(studentId);
            }

            session.SkippedStudentIds = string.Join(",", skippedIds);
        }

        private string NormalizeUid(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return string.Empty;

            var cleaned = new string(
                uid
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToUpperInvariant)
                    .ToArray()
            );

            if (cleaned.Length < 4 || cleaned.Length % 2 != 0)
                return string.Empty;

            var parts = new List<string>();

            for (int i = 0; i < cleaned.Length; i += 2)
            {
                parts.Add(cleaned.Substring(i, 2));
            }

            return string.Join(":", parts);
        }

        private string Shorten(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            if (value.Length <= 16)
                return value;

            return value.Substring(0, 16);
        }
    }

    public class DeviceScanRequest
    {
        public string DeviceKey { get; set; } = string.Empty;

        public string Uid { get; set; } = string.Empty;
    }

    public class DeviceSkipRequest
    {
        public string DeviceKey { get; set; } = string.Empty;
    }
}