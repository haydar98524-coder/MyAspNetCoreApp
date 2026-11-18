using System.Globalization;
using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Staff)]
public class ReportsController : Controller
{
    private readonly AppDbContext _db;
    private readonly DashboardService _dashboard;
    private readonly Localizer _l;

    public ReportsController(AppDbContext db, DashboardService dashboard, Localizer localizer)
    {
        _db = db;
        _dashboard = dashboard;
        _l = localizer;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        var start = new DateTime(today.Year, today.Month, 1);
        var vm = await _dashboard.BuildAsync(start, today, _l.Month(today), User.CurrentUserId(), _l.IsArabic);

        ViewBag.AttendanceRate = await AttendanceRateAsync(start, today);
        ViewBag.NewEnrollments = await _db.Enrollments.CountAsync(e => e.EnrollmentDate >= start);
        ViewBag.OutstandingDues = await _db.Dues
            .Where(d => d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
            .SumAsync(d => d.Amount - d.PaidAmount);
        ViewBag.PayrollMonth = await _db.Payrolls
            .Where(p => p.Month == start)
            .SumAsync(p => p.Net);
        return View(vm);
    }

    public async Task<IActionResult> Profit(string? groupBy, DateTime? from, DateTime? to)
    {
        var (start, end) = NormalizeRange(from, to, 5);
        var group = string.IsNullOrWhiteSpace(groupBy) ? "course" : groupBy.ToLowerInvariant();
        if (group is not ("course" or "teacher" or "subject" or "room")) group = "course";

        var endExclusive = end.AddDays(1);
        var courses = await _db.Courses.AsNoTracking()
            .Include(c => c.Teacher)
            .Include(c => c.Subject)
            .Include(c => c.Grade)
            .Include(c => c.Sections).ThenInclude(s => s.Schedules).ThenInclude(s => s.Classroom)
            .ToListAsync();
        var courseMap = courses.ToDictionary(c => c.Id);

        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.Date >= start && p.Date <= end)
            .Include(p => p.Enrollment)
            .ToListAsync();
        var earnings = await _db.TeacherEarnings.AsNoTracking()
            .Where(e => e.Kind == TeacherEarningKind.CourseShare && e.Date >= start && e.Date <= end)
            .ToListAsync();
        var expenses = await _db.Expenses.AsNoTracking()
            .Where(e => e.Status == ExpenseStatus.Approved && e.Date >= start && e.Date <= end)
            .ToListAsync();

        var lines = new Dictionary<string, ProfitLine>();

        ProfitLine GetLine(string key, string name, string meta)
        {
            if (!lines.TryGetValue(key, out var line))
            {
                line = new ProfitLine { Name = name, Meta = meta };
                lines[key] = line;
            }
            return line;
        }

        if (group == "room")
        {
            var minutesByCourse = courses.ToDictionary(c => c.Id, c =>
                c.Sections.SelectMany(s => s.Schedules)
                    .GroupBy(s => s.ClassroomId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => Math.Max(1, Minutes(x.StartTime, x.EndTime)))));
            var roomNames = courses.SelectMany(c => c.Sections).SelectMany(s => s.Schedules)
                .GroupBy(s => s.ClassroomId)
                .ToDictionary(g => g.Key, g => _l.Pick(g.First().Classroom.NameAr, g.First().Classroom.NameEn));

            void Allocate(int? courseId, long revenue, long cost, long expense)
            {
                if (!courseId.HasValue || !minutesByCourse.TryGetValue(courseId.Value, out var rooms) || rooms.Count == 0)
                {
                    var fallback = GetLine("unassigned", _l.T("Unassigned", "غير محدد"), _l.T("No classroom schedule", "بدون جدول قاعة"));
                    fallback.Revenue += revenue; fallback.TeacherCost += cost; fallback.Expenses += expense;
                    return;
                }
                var totalMinutes = rooms.Values.Sum();
                foreach (var room in rooms)
                {
                    var share = (decimal)room.Value / totalMinutes;
                    var name = roomNames.TryGetValue(room.Key, out var n) ? n : _l.T("Classroom", "قاعة");
                    var line = GetLine($"room-{room.Key}", name, _l.T("Allocated by scheduled hours", "موزعة حسب ساعات الجدول"));
                    line.Revenue += (long)Math.Round(revenue * share, MidpointRounding.AwayFromZero);
                    line.TeacherCost += (long)Math.Round(cost * share, MidpointRounding.AwayFromZero);
                    line.Expenses += (long)Math.Round(expense * share, MidpointRounding.AwayFromZero);
                }
            }

            foreach (var payment in payments)
                Allocate(payment.Enrollment?.CourseId, payment.Amount, 0, 0);
            foreach (var earning in earnings)
                Allocate(earning.CourseId, 0, earning.Amount, 0);
            foreach (var expense in expenses)
                Allocate(expense.CourseId, 0, 0, expense.Amount);
        }
        else
        {
            foreach (var payment in payments)
            {
                var courseId = payment.Enrollment?.CourseId;
                courseMap.TryGetValue(courseId ?? 0, out var course);
                var (key, name, meta) = GroupKey(group, course);
                var line = GetLine(key, name, meta);
                line.Revenue += payment.Amount;
            }
            foreach (var earning in earnings)
            {
                courseMap.TryGetValue(earning.CourseId ?? 0, out var course);
                var (key, name, meta) = GroupKey(group, course);
                var line = GetLine(key, name, meta);
                line.TeacherCost += earning.Amount;
            }
            foreach (var expense in expenses)
            {
                courseMap.TryGetValue(expense.CourseId ?? 0, out var course);
                var (key, name, meta) = GroupKey(group, course);
                var line = GetLine(key, name, meta);
                line.Expenses += expense.Amount;
            }
        }

        var vm = new ProfitReportViewModel
        {
            From = start,
            To = end,
            GroupBy = group,
            Lines = lines.Values.OrderByDescending(x => x.Profit).ThenByDescending(x => x.Revenue).ToList()
        };
        vm.TotalRevenue = vm.Lines.Sum(x => x.Revenue);
        vm.TotalTeacherCost = vm.Lines.Sum(x => x.TeacherCost);
        vm.TotalExpenses = vm.Lines.Sum(x => x.Expenses);
        return View(vm);
    }

    public async Task<IActionResult> Attendance(DateTime? from, DateTime? to)
    {
        var (start, end) = NormalizeRange(from, to, 0);
        var endExclusive = end.AddDays(1);
        var records = await _db.StudentAttendances.AsNoTracking()
            .Where(a => a.Date >= start && a.Date <= end)
            .Include(a => a.Section).ThenInclude(s => s.Course).ThenInclude(c => c.Teacher)
            .ToListAsync();
        var enrolled = await _db.Enrollments.AsNoTracking()
            .Where(e => e.Status != EnrollmentStatus.Cancelled && e.SectionId != null)
            .GroupBy(e => e.SectionId!.Value)
            .Select(g => new { SectionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SectionId, x => x.Count);

        var rows = records.GroupBy(a => a.SectionId)
            .Select(g =>
            {
                var section = g.First().Section;
                return new AttendanceReportRow
                {
                    Name = $"{_l.Pick(section.Course.NameAr, section.Course.NameEn)} — {section.Name}",
                    Meta = _l.Pick(section.Course.Teacher.FullNameAr, section.Course.Teacher.FullNameEn),
                    Enrolled = enrolled.TryGetValue(g.Key, out var count) ? count : 0,
                    Sessions = g.Count(),
                    Present = g.Count(x => x.Status == AttendanceStatus.Present),
                    Absent = g.Count(x => x.Status == AttendanceStatus.Absent),
                    Late = g.Count(x => x.Status == AttendanceStatus.Late),
                    Excused = g.Count(x => x.Status == AttendanceStatus.Excused)
                };
            })
            .OrderBy(r => r.Rate).ThenByDescending(r => r.Sessions)
            .ToList();

        var vm = new AttendanceReportViewModel
        {
            From = start,
            To = end,
            Sessions = rows.Sum(r => r.Sessions),
            Present = rows.Sum(r => r.Present),
            Absent = rows.Sum(r => r.Absent),
            Late = rows.Sum(r => r.Late),
            Excused = rows.Sum(r => r.Excused),
            Rows = rows
        };
        return View(vm);
    }

    public async Task<IActionResult> Dues()
    {
        var today = DateTime.Today;
        var dues = await _db.Dues.AsNoTracking()
            .Where(d => d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
            .Include(d => d.Student).ThenInclude(s => s.Grade)
            .Include(d => d.Student).ThenInclude(s => s.Parent)
            .ToListAsync();

        var rows = dues.GroupBy(d => d.StudentId)
            .Select(g =>
            {
                var student = g.First().Student;
                var remaining = g.Sum(x => Math.Max(0, x.Amount - x.PaidAmount));
                if (remaining <= 0) return null;
                var dueDate = g.Min(x => x.DueDate);
                var days = (today - dueDate).Days;
                return new DuesReportRow
                {
                    Student = student,
                    ParentName = student.Parent is null ? "" : _l.Pick(student.Parent.FullNameAr, student.Parent.FullNameEn),
                    GradeName = _l.Pick(student.Grade.NameAr, student.Grade.NameEn),
                    Amount = g.Sum(x => x.Amount),
                    Paid = g.Sum(x => x.PaidAmount),
                    Remaining = remaining,
                    DueDate = dueDate,
                    DaysOverdue = days > 0 ? days : 0
                };
            })
            .Where(r => r is not null)
            .Select(r => r!)
            .OrderByDescending(r => r.DaysOverdue).ThenByDescending(r => r.Remaining)
            .ToList();

        var vm = new DuesReportViewModel
        {
            AsOf = today,
            Total = rows.Sum(r => r.Remaining),
            Overdue = rows.Where(r => r.DaysOverdue > 0).Sum(r => r.Remaining),
            Students = rows.Count,
            Rows = rows
        };
        return View(vm);
    }

    public async Task<IActionResult> Payroll(string? month)
    {
        var normalized = NormalizeMonth(month);
        var rows = await _db.Payrolls.AsNoTracking()
            .Where(p => p.Month == normalized)
            .Include(p => p.Employee)
            .OrderBy(p => p.Employee.Department).ThenBy(p => p.Employee.FullNameAr)
            .Select(p => new PayrollReportRow { Payroll = p, Employee = p.Employee })
            .ToListAsync();
        var vm = new PayrollReportViewModel { Month = normalized, Rows = rows };
        ViewBag.MonthValue = normalized.ToString("yyyy-MM");
        return View(vm);
    }

    public async Task<IActionResult> Enrollments(DateTime? from, DateTime? to)
    {
        var (start, end) = NormalizeRange(from, to, 0);
        var newEnrollments = await _db.Enrollments.AsNoTracking()
            .Where(e => e.EnrollmentDate >= start && e.EnrollmentDate <= end)
            .Include(e => e.Course)
            .ToListAsync();
        var activeCounts = await _db.Enrollments.AsNoTracking()
            .Where(e => e.Status == EnrollmentStatus.Active)
            .GroupBy(e => e.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Count);
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.Date >= start && p.Date <= end && p.Enrollment != null)
            .Select(p => new { CourseId = p.Enrollment!.CourseId, p.Amount })
            .ToListAsync();
        var dues = await _db.Dues.AsNoTracking()
            .Where(d => d.Enrollment != null && d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
            .Select(d => new { CourseId = d.Enrollment!.CourseId, Remaining = d.Amount - d.PaidAmount })
            .ToListAsync();
        var courses = await _db.Courses.AsNoTracking()
            .Include(c => c.Teacher).Include(c => c.Grade)
            .ToListAsync();

        var rows = newEnrollments.GroupBy(e => e.CourseId)
            .Select(g =>
            {
                var course = courses.FirstOrDefault(c => c.Id == g.Key);
                if (course is null) return null;
                return new EnrollmentReportRow
                {
                    Course = course,
                    TeacherName = _l.Pick(course.Teacher.FullNameAr, course.Teacher.FullNameEn),
                    NewEnrollments = g.Count(),
                    ActiveEnrollments = activeCounts.TryGetValue(g.Key, out var count) ? count : 0,
                    Fees = g.Sum(x => x.NetFee),
                    Collected = payments.Where(p => p.CourseId == g.Key).Sum(p => p.Amount),
                    Outstanding = dues.Where(d => d.CourseId == g.Key).Sum(d => Math.Max(0, d.Remaining))
                };
            })
            .Where(r => r is not null)
            .Select(r => r!)
            .OrderByDescending(r => r.NewEnrollments).ThenByDescending(r => r.Collected)
            .ToList();

        var vm = new EnrollmentReportViewModel
        {
            From = start,
            To = end,
            Rows = rows,
            Enrollments = rows.Sum(r => r.NewEnrollments),
            Fees = rows.Sum(r => r.Fees),
            Collected = rows.Sum(r => r.Collected),
            Outstanding = rows.Sum(r => r.Outstanding)
        };
        return View(vm);
    }

    private (string Key, string Name, string Meta) GroupKey(string group, Course? course)
    {
        if (course is null)
            return ("unassigned", _l.T("Unassigned / other income", "غير محدد / إيرادات أخرى"), _l.T("No course attached", "غير مرتبط بدورة"));

        return group switch
        {
            "teacher" => ($"teacher-{course.TeacherId}",
                _l.Pick(course.Teacher.FullNameAr, course.Teacher.FullNameEn),
                _l.T("Teacher share of course revenue", "حصة المدرّس من إيراد الدورات")),
            "subject" => ($"subject-{course.SubjectId}",
                _l.Pick(course.Subject.NameAr, course.Subject.NameEn),
                _l.T("All courses of this subject", "كل دورات هذه المادة")),
            _ => ($"course-{course.Id}",
                _l.Pick(course.NameAr, course.NameEn),
                $"{_l.Pick(course.Teacher.FullNameAr, course.Teacher.FullNameEn)} · {_l.Pick(course.Grade.NameAr, course.Grade.NameEn)}")
        };
    }

    private async Task<decimal> AttendanceRateAsync(DateTime start, DateTime end)
    {
        var rows = await _db.StudentAttendances.AsNoTracking()
            .Where(a => a.Date >= start && a.Date <= end)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        var total = rows.Sum(r => r.Count);
        if (total == 0) return 0;
        var present = rows.Where(r => r.Status is AttendanceStatus.Present or AttendanceStatus.Late).Sum(r => r.Count);
        return Math.Round((decimal)present / total * 100, 1);
    }

    private static (DateTime Start, DateTime End) NormalizeRange(DateTime? from, DateTime? to, int monthsBack)
    {
        var today = DateTime.Today;
        var start = (from ?? new DateTime(today.Year, today.Month, 1).AddMonths(-monthsBack)).Date;
        var end = (to ?? today).Date;
        if (end < start) (start, end) = (end, start);
        if ((end - start).TotalDays > 731) start = end.AddDays(-731);
        return (start, end);
    }

    private static int Minutes(string start, string end)
    {
        if (!TimeSpan.TryParse(start, CultureInfo.InvariantCulture, out var a) ||
            !TimeSpan.TryParse(end, CultureInfo.InvariantCulture, out var b)) return 60;
        return b > a ? (int)(b - a).TotalMinutes : 60;
    }

    private static DateTime NormalizeMonth(string? month)
    {
        if (!string.IsNullOrWhiteSpace(month) &&
            DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return new DateTime(parsed.Year, parsed.Month, 1);
        if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse(month, out parsed))
            return new DateTime(parsed.Year, parsed.Month, 1);
        return new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    }
}
