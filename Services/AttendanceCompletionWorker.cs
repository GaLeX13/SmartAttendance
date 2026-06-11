using Microsoft.EntityFrameworkCore;
using SmartAttendance.Data;

namespace SmartAttendance.Services
{
    public class AttendanceCompletionWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AttendanceCompletionWorker> _logger;

        public AttendanceCompletionWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<AttendanceCompletionWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Attendance completion worker started.");

            await CheckCoursesAsync(stoppingToken);

            using var timer =
                new PeriodicTimer(TimeSpan.FromMinutes(1));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await CheckCoursesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
            }
        }

        private async Task CheckCoursesAsync(
            CancellationToken cancellationToken)
        {
            TimeZoneInfo? romanianTimeZone =
                GetRomanianTimeZone();

            if (romanianTimeZone == null)
                return;

            DateTime romanianNow =
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    romanianTimeZone);

            List<int> courseIds;

            try
            {
                using var scope =
                    _scopeFactory.CreateScope();

                var context =
                    scope.ServiceProvider
                        .GetRequiredService<AppDbContext>();

                courseIds = await context.Courses
                    .AsNoTracking()
                    .Where(course =>
                        course.AutoFillAbsencesEnabled)
                    .Select(course => course.Id)
                    .ToListAsync(cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Automatic attendance settings could not be loaded.");

                return;
            }

            foreach (int courseId in courseIds)
            {
                try
                {
                    await ProcessCourseAsync(
                        courseId,
                        romanianNow,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Automatic attendance completion failed for course {CourseId}.",
                        courseId);
                }
            }
        }

        private async Task ProcessCourseAsync(
            int courseId,
            DateTime romanianNow,
            CancellationToken cancellationToken)
        {
            using var scope =
                _scopeFactory.CreateScope();

            var context =
                scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

            var completionService =
                scope.ServiceProvider
                    .GetRequiredService<AttendanceCompletionService>();

            var course = await context.Courses
                .FirstOrDefaultAsync(
                    item =>
                        item.Id == courseId &&
                        item.AutoFillAbsencesEnabled,
                    cancellationToken);

            if (course == null)
                return;

            DateTime weekStart =
                AttendanceCompletionService.GetWeekStart(
                    romanianNow.Date);

            int scheduledDayOffset =
                (course.AutoFillDayOfWeek -
                 (int)DayOfWeek.Monday + 7) % 7;

            DateTime scheduledLocalTime =
                weekStart
                    .AddDays(scheduledDayOffset)
                    .Add(course.AutoFillTime);

            if (romanianNow < scheduledLocalTime)
                return;

            if (course.LastAutoFillWeekStart.HasValue)
            {
                DateTime lastProcessedWeek =
                    AttendanceCompletionService.GetWeekStart(
                        course.LastAutoFillWeekStart.Value);

                if (lastProcessedWeek == weekStart)
                {
                    _logger.LogDebug(
                        "Automatic attendance completion was already processed for course {CourseId}, week {WeekStart}.",
                        course.Id,
                        weekStart);

                    return;
                }
            }

            var result =
                await completionService.CompleteWeekAsync(
                    course.Id,
                    weekStart,
                    cancellationToken);

            if (!result.HasAttendanceActivity)
            {
                _logger.LogInformation(
                    "Automatic completion skipped course {CourseId}, week {WeekStart}, because no Present or Recovered activity exists.",
                    course.Id,
                    weekStart);

                return;
            }

            course.LastAutoFillWeekStart = weekStart;

            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Automatic attendance completion processed course {CourseId}, week {WeekStart}. Added absences: {AddedAbsences}.",
                course.Id,
                weekStart,
                result.AddedAbsences);
        }

        private TimeZoneInfo? GetRomanianTimeZone()
        {
            string[] timeZoneIds =
            {
                "Europe/Bucharest",
                "E. Europe Standard Time"
            };

            foreach (string timeZoneId in timeZoneIds)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(
                        timeZoneId);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            _logger.LogError(
                "The Romanian time zone could not be loaded.");

            return null;
        }
    }
}