using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Student)]
public class StudentController : Controller
{
    private readonly AppDbContext _db;
    private readonly Localizer _l;

    public StudentController(AppDbContext db, Localizer l)
    {
        _db = db;
        _l = l;
    }

    // ───────────────────────── Profile overview ─────────────────────────

    public async Task<IActionResult> Index()
    {
        var student = await CurrentStudentAsync();
        if (student is null) return PortalNotLinked();

        var enrollments = await EnrollmentsAsync(student.Id);
        var schedule = UpcomingSchedule(enrollments);
        var vm = new StudentPortalViewModel
        {
            Student = student,
            Enrollments = enrollments,
            Schedule = schedule,
            Attendance = await AttendanceQuery(student.Id).OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.Id).Take(10).ToListAsync(),
            Results = await ResultsQuery(student.Id).OrderByDescending(r => r.Exam.Date).Take(6).ToListAsync(),
            Dues = await _db.Dues.AsNoTracking()
                .Include(d => d.Enrollment).ThenInclude(e => e.Course)
                .Where(d => d.StudentId == student.Id && d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
                .OrderBy(d => d.DueDate).Take(6).ToListAsync(),
            Payments = await _db.Payments.AsNoTracking()
                .Where(p => p.StudentId == student.Id)
                .OrderByDescending(p => p.Date).ThenByDescending(p => p.Id).Take(6).ToListAsync(),
            PrivateLessons = await PrivateLessonsAsync(student.Id, onlyUpcoming: true)
        };
        await FillStatsAsync(vm, student.Id);
        return View(vm);
    }

    // ───────────────────────── Weekly schedule ─────────────────────────

    public async Task<IActionResult> Schedule()
    {
        var student = await CurrentStudentAsync();
        if (student is null) return PortalNotLinked();

        var enrollments = await EnrollmentsAsync(student.Id);
        var schedule = UpcomingSchedule(enrollments);
        ViewBag.Student = student;
        ViewBag.Lessons = await PrivateLessonsAsync(student.Id, onlyUpcoming: true);
        return View(schedule);
    }

    // ───────────────────────── Attendance ─────────────────────────

    public async Task<IActionResult> Attendance()
    {
        var student = await CurrentStudentAsync();
        if (student is null) return PortalNotLinked();

        var vm = new StudentPortalViewModel
        {
            Student = student,
            Attendance = await AttendanceQuery(student.Id).OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.Id).Take(150).ToListAsync()
        };
        await FillStatsAsync(vm, student.Id);
        return View(vm);
    }

    // ───────────────────────── Results ─────────────────────────

    public async Task<IActionResult> Results()
    {
        var student = await CurrentStudentAsync();
        if (student is null) return PortalNotLinked();

        var vm = new StudentPortalViewModel
        {
            Student = student,
            Results = await ResultsQuery(student.Id).OrderByDescending(r => r.Exam.Date).Take(250).ToListAsync()
        };
        await FillStatsAsync(vm, student.Id);
        return View(vm);
    }

    // ───────────────────────── Fees & payments ─────────────────────────

    public async Task<IActionResult> Payments()
    {
        var student = await CurrentStudentAsync();
        if (student is null) return PortalNotLinked();

        var vm = new StudentPortalViewModel
        {
            Student = student,
            Dues = await _db.Dues.AsNoTracking()
                .Include(d => d.Enrollment).ThenInclude(e => e.Course)
                .Where(d => d.StudentId == student.Id)
                .OrderBy(d => d.DueDate).ToListAsync(),
            Payments = await _db.Payments.AsNoTracking()
                .Where(p => p.StudentId == student.Id)
                .OrderByDescending(p => p.Date).ThenByDescending(p => p.Id).Take(200).ToListAsync()
        };
        await FillStatsAsync(vm, student.Id);
        return View(vm);
    }

    // ───────────────────────── Helpers ─────────────────────────

    private async Task<Student?> CurrentStudentAsync()
    {
        var studentId = User.StudentId();
        if (studentId.HasValue)
        {
            var byId = await _db.Students.AsNoTracking()
                .Include(s => s.Grade).ThenInclude(g => g.Stage)
                .Include(s => s.Parent)
                .FirstOrDefaultAsync(s => s.Id == studentId.Value);
            if (byId is not null) return byId;
        }
        var userId = User.CurrentUserId();
        return await _db.Students.AsNoTracking()
            .Include(s => s.Grade).ThenInclude(g => g.Stage)
            .Include(s => s.Parent)
            .FirstOrDefaultAsync(s => s.UserId == userId);
    }

    private Task<List<Enrollment>> EnrollmentsAsync(int studentId) =>
        _db.Enrollments.AsNoTracking()
            .Include(e => e.Course).ThenInclude(c => c.Subject)
            .Include(e => e.Course).ThenInclude(c => c.Teacher)
            .Include(e => e.Course).ThenInclude(c => c.Grade).ThenInclude(g => g.Stage)
            .Include(e => e.Section).ThenInclude(s => s!.Course).ThenInclude(c => c.Subject)
            .Include(e => e.Section).ThenInclude(s => s!.Course).ThenInclude(c => c.Teacher)
            .Include(e => e.Section).ThenInclude(s => s!.Schedules).ThenInclude(x => x.Classroom)
            .AsSplitQuery()
            .Where(e => e.StudentId == studentId && e.Status != EnrollmentStatus.Cancelled)
            .OrderBy(e => e.Course.NameAr).ToListAsync();

    private static List<ClassSchedule> UpcomingSchedule(List<Enrollment> enrollments) =>
        enrollments.Where(e => e.Section is not null)
            .SelectMany(e => e.Section!.Schedules)
            .OrderBy(x => ((int)x.DayOfWeek + 6) % 7).ThenBy(x => x.StartTime)
            .ToList();

    private IQueryable<StudentAttendance> AttendanceQuery(int studentId) =>
        _db.StudentAttendances.AsNoTracking()
            .Include(a => a.Section).ThenInclude(s => s.Course).ThenInclude(c => c.Subject)
            .Where(a => a.StudentId == studentId);

    private IQueryable<ExamResult> ResultsQuery(int studentId) =>
        _db.ExamResults.AsNoTracking()
            .Include(r => r.Exam).ThenInclude(e => e.Course).ThenInclude(c => c.Subject)
            .Include(r => r.Exam).ThenInclude(e => e.Course).ThenInclude(c => c.Grade)
            .Where(r => r.StudentId == studentId);

    private Task<List<PrivateLesson>> PrivateLessonsAsync(int studentId, bool onlyUpcoming = false)
    {
        var query = _db.PrivateLessons.AsNoTracking()
            .Include(l => l.Subject).Include(l => l.Teacher).Include(l => l.Grade)
            .Where(l => l.StudentId == studentId);
        if (onlyUpcoming)
            query = query.Where(l => l.Status == PrivateLessonStatus.Scheduled && l.Date >= DateTime.Today);
        return query.OrderBy(l => l.Date).ThenBy(l => l.StartTime).Take(onlyUpcoming ? 12 : 6).ToListAsync();
    }

    private async Task FillStatsAsync(StudentPortalViewModel vm, int studentId)
    {
        vm.Sessions = await _db.StudentAttendances.CountAsync(a => a.StudentId == studentId);
        vm.Present = await _db.StudentAttendances.CountAsync(a => a.StudentId == studentId
            && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late));
        vm.Absent = await _db.StudentAttendances.CountAsync(a => a.StudentId == studentId
            && a.Status == AttendanceStatus.Absent);
        vm.Late = await _db.StudentAttendances.CountAsync(a => a.StudentId == studentId
            && a.Status == AttendanceStatus.Late);
        vm.Excused = await _db.StudentAttendances.CountAsync(a => a.StudentId == studentId
            && a.Status == AttendanceStatus.Excused);

        var marks = await _db.ExamResults.AsNoTracking()
            .Include(r => r.Exam)
            .Where(r => r.StudentId == studentId && r.Exam.MaxMark > 0)
            .Select(r => new { r.Mark, r.Exam.MaxMark })
            .ToListAsync();
        vm.AverageMark = marks.Count == 0
            ? 0
            : Math.Round(marks.Average(m => m.Mark * 100m / m.MaxMark), 1);

        vm.Outstanding = await _db.Dues
            .Where(d => d.StudentId == studentId && d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
            .SumAsync(d => (long?)(d.Amount - d.PaidAmount)) ?? 0;
    }

    private IActionResult PortalNotLinked() => View("~/Views/Shared/PortalNotLinked.cshtml");
}
