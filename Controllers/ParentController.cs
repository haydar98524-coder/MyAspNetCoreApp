using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Parent)]
public class ParentController : Controller
{
    private readonly AppDbContext _db;
    private readonly Localizer _l;

    public ParentController(AppDbContext db, Localizer l)
    {
        _db = db;
        _l = l;
    }

    // ───────────────────────── Overview ─────────────────────────

    public async Task<IActionResult> Index()
    {
        var parent = await CurrentParentAsync();
        if (parent is null) return PortalNotLinked();

        var children = await ChildrenAsync(parent.Id);
        var ids = children.Select(s => s.Id).ToList();
        var summaries = await BuildChildSummariesAsync(children, false);
        var outstanding = await OutstandingAsync(ids);
        var year = DateTime.Today.Year;

        var vm = new ParentPortalViewModel
        {
            Parent = parent,
            Children = summaries,
            Outstanding = outstanding,
            PaidYear = ids.Count == 0 ? 0 : await _db.Payments
                .Where(p => ids.Contains(p.StudentId) && p.Date.Year == year)
                .SumAsync(p => (long?)p.Amount) ?? 0,
            Dues = ids.Count == 0 ? new() : await _db.Dues
                .Include(d => d.Student).Include(d => d.Enrollment).ThenInclude(e => e.Course)
                .Where(d => ids.Contains(d.StudentId) && d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
                .OrderBy(d => d.DueDate).Take(8).ToListAsync(),
            Payments = ids.Count == 0 ? new() : await _db.Payments
                .Include(p => p.Student)
                .Where(p => ids.Contains(p.StudentId))
                .OrderByDescending(p => p.Date).ThenByDescending(p => p.Id).Take(8).ToListAsync(),
            UpcomingLessons = await UpcomingLessonsAsync(parent.Id, ids),
            Notifications = await _db.Notifications.AsNoTracking()
                .Where(n => n.UserId == User.CurrentUserId())
                .OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync()
        };
        ViewBag.Settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1);
        return View(vm);
    }

    // ───────────────────────── Children ─────────────────────────

    public async Task<IActionResult> Children()
    {
        var parent = await CurrentParentAsync();
        if (parent is null) return PortalNotLinked();
        var children = await ChildrenAsync(parent.Id);
        ViewBag.Parent = parent;
        return View(await BuildChildSummariesAsync(children, true));
    }

    // ───────────────────────── Fees & payments ─────────────────────────

    public async Task<IActionResult> Payments()
    {
        var parent = await CurrentParentAsync();
        if (parent is null) return PortalNotLinked();

        var children = await ChildrenAsync(parent.Id);
        var ids = children.Select(s => s.Id).ToList();
        var year = DateTime.Today.Year;
        var vm = new ParentPortalViewModel
        {
            Parent = parent,
            Children = await BuildChildSummariesAsync(children, false),
            Outstanding = await OutstandingAsync(ids),
            PaidYear = ids.Count == 0 ? 0 : await _db.Payments
                .Where(p => ids.Contains(p.StudentId) && p.Date.Year == year)
                .SumAsync(p => (long?)p.Amount) ?? 0,
            Dues = ids.Count == 0 ? new() : await _db.Dues
                .Include(d => d.Student).Include(d => d.Enrollment).ThenInclude(e => e.Course)
                .Where(d => ids.Contains(d.StudentId))
                .OrderBy(d => d.DueDate).ToListAsync(),
            Payments = ids.Count == 0 ? new() : await _db.Payments
                .Include(p => p.Student)
                .Where(p => ids.Contains(p.StudentId))
                .OrderByDescending(p => p.Date).ThenByDescending(p => p.Id).Take(200).ToListAsync()
        };
        return View(vm);
    }

    // ───────────────────────── Attendance ─────────────────────────

    public async Task<IActionResult> Attendance(int? studentId, string? status)
    {
        var parent = await CurrentParentAsync();
        if (parent is null) return PortalNotLinked();

        var children = await ChildrenAsync(parent.Id);
        var ids = children.Select(s => s.Id).ToList();
        if (studentId.HasValue && !ids.Contains(studentId.Value)) studentId = null;
        AttendanceStatus? parsed = Enum.TryParse<AttendanceStatus>(status, out var st) ? st : null;

        var query = _db.StudentAttendances.AsNoTracking()
            .Include(a => a.Student)
            .Include(a => a.Section).ThenInclude(s => s.Course).ThenInclude(c => c.Subject)
            .Where(a => ids.Contains(a.StudentId));
        if (studentId.HasValue) query = query.Where(a => a.StudentId == studentId.Value);
        if (parsed.HasValue) query = query.Where(a => a.Status == parsed.Value);

        var sessions = await query.CountAsync();
        var present = await query.CountAsync(a => a.Status == AttendanceStatus.Present);
        var absent = await query.CountAsync(a => a.Status == AttendanceStatus.Absent);
        var late = await query.CountAsync(a => a.Status == AttendanceStatus.Late);
        var excused = await query.CountAsync(a => a.Status == AttendanceStatus.Excused);

        var vm = new ParentAttendanceViewModel
        {
            Records = await query.OrderByDescending(a => a.Date).ThenByDescending(a => a.Id)
                .Take(150).ToListAsync(),
            Children = children,
            StudentId = studentId,
            Status = parsed?.ToString(),
            Sessions = sessions,
            Present = present + late,
            Absent = absent,
            Late = late,
            Excused = excused
        };
        return View(vm);
    }

    // ───────────────────────── Results ─────────────────────────

    public async Task<IActionResult> Results(int? studentId, ExamType? type)
    {
        var parent = await CurrentParentAsync();
        if (parent is null) return PortalNotLinked();

        var children = await ChildrenAsync(parent.Id);
        var ids = children.Select(s => s.Id).ToList();
        if (studentId.HasValue && !ids.Contains(studentId.Value)) studentId = null;

        var query = _db.ExamResults.AsNoTracking()
            .Include(r => r.Student)
            .Include(r => r.Exam).ThenInclude(e => e.Course).ThenInclude(c => c.Subject)
            .Include(r => r.Exam).ThenInclude(e => e.Course).ThenInclude(c => c.Grade)
            .Where(r => ids.Contains(r.StudentId));
        if (studentId.HasValue) query = query.Where(r => r.StudentId == studentId.Value);
        if (type.HasValue) query = query.Where(r => r.Exam.Type == type.Value);

        var results = await query.OrderByDescending(r => r.Exam.Date).ThenBy(r => r.Student.FullNameAr)
            .Take(250).ToListAsync();
        var percentages = results
            .Where(r => r.Exam.MaxMark > 0)
            .Select(r => r.Mark * 100m / r.Exam.MaxMark).ToList();

        var vm = new ParentResultsViewModel
        {
            Results = results,
            Children = children,
            StudentId = studentId,
            Type = type,
            Average = percentages.Count == 0 ? 0 : Math.Round(percentages.Average(), 1),
            Passed = results.Count(r => r.Mark >= r.Exam.PassMark),
            Failed = results.Count(r => r.Mark < r.Exam.PassMark)
        };
        return View(vm);
    }

    // ───────────────────────── Helpers ─────────────────────────

    private async Task<Parent?> CurrentParentAsync()
    {
        var parentId = User.ParentId();
        if (parentId.HasValue)
        {
            var byId = await _db.Parents.FirstOrDefaultAsync(p => p.Id == parentId.Value);
            if (byId is not null) return byId;
        }
        var userId = User.CurrentUserId();
        return await _db.Parents.FirstOrDefaultAsync(p => p.UserId == userId);
    }

    private Task<List<Student>> ChildrenAsync(int parentId) =>
        _db.Students.AsNoTracking()
            .Include(s => s.Grade).ThenInclude(g => g.Stage)
            .Include(s => s.Parent)
            .Where(s => s.ParentId == parentId)
            .OrderBy(s => s.FullNameAr).ToListAsync();

    private async Task<List<ChildSummary>> BuildChildSummariesAsync(List<Student> children, bool includeCourses)
    {
        var ids = children.Select(s => s.Id).ToList();
        if (ids.Count == 0) return new();

        var enrollments = await _db.Enrollments.AsNoTracking()
            .Include(e => e.Course).ThenInclude(c => c.Subject)
            .Include(e => e.Course).ThenInclude(c => c.Teacher)
            .Include(e => e.Course).ThenInclude(c => c.Grade)
            .Include(e => e.Section).ThenInclude(s => s!.Schedules)
            .Where(e => ids.Contains(e.StudentId) && e.Status != EnrollmentStatus.Cancelled)
            .OrderBy(e => e.Course.NameAr).ToListAsync();

        var attendance = await _db.StudentAttendances.AsNoTracking()
            .Where(a => ids.Contains(a.StudentId))
            .GroupBy(a => a.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Total = g.Count(),
                Present = g.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)
            })
            .ToListAsync();

        var marks = await _db.ExamResults.AsNoTracking()
            .Include(r => r.Exam)
            .Where(r => ids.Contains(r.StudentId) && r.Exam.MaxMark > 0)
            .Select(r => new { r.StudentId, r.Mark, r.Exam.MaxMark })
            .ToListAsync();

        var dues = await _db.Dues.AsNoTracking()
            .Where(d => ids.Contains(d.StudentId) && d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
            .Select(d => new { d.StudentId, d.Amount, d.PaidAmount })
            .ToListAsync();

        return children.Select(s =>
        {
            var att = attendance.FirstOrDefault(a => a.StudentId == s.Id);
            var studentMarks = marks.Where(m => m.StudentId == s.Id).ToList();
            return new ChildSummary
            {
                Student = s,
                Enrollments = enrollments.Count(e => e.StudentId == s.Id),
                Sessions = att?.Total ?? 0,
                Present = att?.Present ?? 0,
                AverageMark = studentMarks.Count == 0
                    ? 0
                    : Math.Round(studentMarks.Average(m => m.Mark * 100m / m.MaxMark), 1),
                Outstanding = dues.Where(d => d.StudentId == s.Id)
                    .Sum(d => Math.Max(0, d.Amount - d.PaidAmount)),
                Courses = includeCourses
                    ? enrollments.Where(e => e.StudentId == s.Id).ToList()
                    : new()
            };
        }).ToList();
    }

    private async Task<long> OutstandingAsync(List<int> ids) =>
        ids.Count == 0 ? 0 : await _db.Dues
            .Where(d => ids.Contains(d.StudentId) && d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
            .SumAsync(d => (long?)(d.Amount - d.PaidAmount)) ?? 0;

    private Task<List<PrivateLesson>> UpcomingLessonsAsync(int parentId, List<int> childIds) =>
        _db.PrivateLessons.AsNoTracking()
            .Include(l => l.Subject).Include(l => l.Student).Include(l => l.Teacher).Include(l => l.Grade)
            .Where(l => l.Status == PrivateLessonStatus.Scheduled && l.Date >= DateTime.Today
                && (l.ParentId == parentId || (l.StudentId.HasValue && childIds.Contains(l.StudentId.Value))))
            .OrderBy(l => l.Date).ThenBy(l => l.StartTime).Take(6).ToListAsync();

    private IActionResult PortalNotLinked() => View("~/Views/Shared/PortalNotLinked.cshtml");
}
