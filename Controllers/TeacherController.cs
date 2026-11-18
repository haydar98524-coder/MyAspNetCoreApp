using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Teacher)]
public class TeacherController : Controller
{
    private readonly AppDbContext _db;
    private readonly Localizer _l;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;

    public TeacherController(AppDbContext db, Localizer l, AuditService audit, NotificationService notifications)
    {
        _db = db;
        _l = l;
        _audit = audit;
        _notifications = notifications;
    }

    // ───────────────────────── Dashboard ─────────────────────────

    public async Task<IActionResult> Index()
    {
        var teacher = await CurrentTeacherAsync();
        if (teacher is null) return PortalNotLinked();

        var sections = await SectionsFor(teacher.Id).AsNoTracking().ToListAsync();
        var sectionIds = sections.Select(s => s.Id).ToList();
        var schedules = sections.SelectMany(s => s.Schedules).ToList();
        var courses = sections.Select(s => s.Course).GroupBy(c => c.Id).Select(g => g.First())
            .OrderBy(c => c.NameAr).ToList();
        var today = DateTime.Today;
        var since = today.AddDays(-30);

        var earnings = _db.TeacherEarnings.Where(e => e.TeacherId == teacher.Id);
        var attendanceTotal = sectionIds.Count == 0 ? 0
            : await _db.StudentAttendances.CountAsync(a => sectionIds.Contains(a.SectionId) && a.Date >= since);
        var attendancePresent = sectionIds.Count == 0 ? 0
            : await _db.StudentAttendances.CountAsync(a => sectionIds.Contains(a.SectionId) && a.Date >= since
                && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late));

        var vm = new TeacherPortalViewModel
        {
            Teacher = teacher,
            Courses = courses,
            Today = schedules.Where(x => x.DayOfWeek == today.DayOfWeek)
                .OrderBy(x => x.StartTime).ToList(),
            Week = schedules.OrderBy(x => ((int)x.DayOfWeek + 6) % 7).ThenBy(x => x.StartTime).ToList(),
            RecentEarnings = await earnings
                .Include(e => e.Course)
                .Include(e => e.PrivateLesson).ThenInclude(l => l!.Subject)
                .OrderByDescending(e => e.Date).Take(8).ToListAsync(),
            UpcomingLessons = await _db.PrivateLessons
                .Include(l => l.Subject).Include(l => l.Student).Include(l => l.Grade)
                .Where(l => l.TeacherId == teacher.Id && l.Status == PrivateLessonStatus.Scheduled && l.Date >= today)
                .OrderBy(l => l.Date).ThenBy(l => l.StartTime).Take(6).ToListAsync(),
            PendingRequests = await AssignedRequests(teacher.Id)
                .Where(r => r.Status == RequestStatus.Pending || r.Status == RequestStatus.Matched)
                .OrderByDescending(r => r.CreatedAt).Take(6).ToListAsync(),
            Unpaid = await earnings.Where(e => !e.IsPaid).SumAsync(e => (long?)e.Amount) ?? 0,
            Paid = await earnings.Where(e => e.IsPaid).SumAsync(e => (long?)e.Amount) ?? 0,
            Students = await _db.Enrollments
                .Where(e => e.SectionId.HasValue && sectionIds.Contains(e.SectionId.Value)
                    && e.Status != EnrollmentStatus.Cancelled)
                .Select(e => e.StudentId).Distinct().CountAsync(),
            AttendanceRate = attendanceTotal == 0
                ? 0
                : Math.Round((decimal)attendancePresent * 100 / attendanceTotal, 1),
            AverageRating = (decimal)teacher.RatingAvg,
            RatingCount = teacher.RatingCount
        };
        return View(vm);
    }

    // ───────────────────────── Weekly schedule ─────────────────────────

    public async Task<IActionResult> Schedule()
    {
        var teacher = await CurrentTeacherAsync();
        if (teacher is null) return PortalNotLinked();

        var sections = await SectionsFor(teacher.Id).AsNoTracking().ToListAsync();
        var schedules = sections.SelectMany(s => s.Schedules)
            .OrderBy(x => ((int)x.DayOfWeek + 6) % 7).ThenBy(x => x.StartTime).ToList();

        ViewBag.Teacher = teacher;
        ViewBag.Lessons = await _db.PrivateLessons
            .Include(l => l.Subject).Include(l => l.Student).Include(l => l.Grade)
            .Where(l => l.TeacherId == teacher.Id && l.Status == PrivateLessonStatus.Scheduled
                && l.Date >= DateTime.Today)
            .OrderBy(l => l.Date).ThenBy(l => l.StartTime).Take(12).ToListAsync();
        return View(schedules);
    }

    // ───────────────────────── Attendance ─────────────────────────

    public async Task<IActionResult> Attendance(DateTime? date)
    {
        var teacher = await CurrentTeacherAsync();
        if (teacher is null) return PortalNotLinked();

        var day = (date ?? DateTime.Today).Date;
        if (day > DateTime.Today) day = DateTime.Today;

        var sections = await SectionsFor(teacher.Id).AsNoTracking().ToListAsync();
        var sectionIds = sections.Select(s => s.Id).ToList();
        var records = await _db.StudentAttendances.AsNoTracking()
            .Where(a => a.Date == day && sectionIds.Contains(a.SectionId))
            .Select(a => new { a.SectionId, a.Status })
            .ToListAsync();

        var rows = sections.Select(s =>
        {
            var schedule = s.Schedules.FirstOrDefault(x => x.DayOfWeek == day.DayOfWeek);
            var mine = records.Where(r => r.SectionId == s.Id).ToList();
            return new AttendanceSectionRow
            {
                Section = s,
                CourseName = s.Course.NameAr,
                TeacherName = s.Course.Teacher.FullNameAr,
                Room = schedule?.Classroom.NameAr ?? "",
                Time = schedule is null ? "" : $"{schedule.StartTime} - {schedule.EndTime}",
                Enrolled = s.Enrollments.Count(e => e.Status != EnrollmentStatus.Cancelled),
                Recorded = mine.Count,
                Present = mine.Count(x => x.Status is AttendanceStatus.Present or AttendanceStatus.Late),
                Absent = mine.Count(x => x.Status == AttendanceStatus.Absent),
                IsScheduled = schedule is not null
            };
        })
        .OrderByDescending(r => r.IsScheduled)
        .ThenByDescending(r => r.Time)
        .ThenBy(r => r.CourseName)
        .ToList();

        ViewBag.Date = day;
        ViewBag.Teacher = teacher;
        ViewBag.PendingRequests = await _db.AttendanceChangeRequests
            .CountAsync(r => r.RequestedByUserId == User.CurrentUserId() && r.Status == ChangeRequestStatus.Pending);
        return View(rows);
    }

    public async Task<IActionResult> Take(int sectionId, DateTime? date)
    {
        var teacher = await CurrentTeacherAsync();
        if (teacher is null) return PortalNotLinked();
        var section = await SectionsFor(teacher.Id).FirstOrDefaultAsync(s => s.Id == sectionId);
        if (section is null) return Forbid();

        var day = (date ?? DateTime.Today).Date;
        if (day > DateTime.Today)
        {
            TempData["Error"] = _l.T("Attendance cannot be recorded for a future date.",
                "لا يمكن تسجيل الحضور بتاريخ مستقبلي.");
            return RedirectToAction(nameof(Attendance));
        }

        var vm = await BuildTakeViewModelAsync(section, day);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(TakeAttendanceViewModel model)
    {
        var teacher = await CurrentTeacherAsync();
        if (teacher is null) return PortalNotLinked();
        var section = await SectionsFor(teacher.Id).FirstOrDefaultAsync(s => s.Id == model.SectionId);
        if (section is null) return Forbid();

        var day = model.Date.Date;
        if (day > DateTime.Today)
        {
            TempData["Error"] = _l.T("Attendance cannot be recorded for a future date.",
                "لا يمكن تسجيل الحضور بتاريخ مستقبلي.");
            return RedirectToAction(nameof(Attendance));
        }
        if (await _db.StudentAttendances.AnyAsync(a => a.SectionId == section.Id && a.Date == day))
        {
            TempData["Warning"] = _l.T(
                "Attendance was already recorded for this section. Corrections require an approved change request.",
                "تم تسجيل حضور هذه الشعبة مسبقاً. تعديل السجل يتطلب طلب تعديل معتمد من الإدارة.");
            return RedirectToAction(nameof(Take), new { sectionId = section.Id, date = day.ToString("yyyy-MM-dd") });
        }

        var rosterIds = await _db.Enrollments
            .Where(e => e.SectionId == section.Id && e.Status != EnrollmentStatus.Cancelled)
            .Select(e => e.StudentId).ToListAsync();
        var marks = model.Students.Where(m => rosterIds.Contains(m.StudentId)).ToList();
        if (marks.Count == 0)
        {
            TempData["Error"] = _l.T("No students are enrolled in this section.",
                "لا يوجد طلاب مسجلون في هذه الشعبة.");
            return RedirectToAction(nameof(Attendance), new { date = day.ToString("yyyy-MM-dd") });
        }

        var userId = User.CurrentUserId();
        var userName = User.DisplayName();
        var now = DateTime.UtcNow;
        var absent = new List<Student>();
        foreach (var mark in marks)
        {
            _db.StudentAttendances.Add(new StudentAttendance
            {
                StudentId = mark.StudentId,
                CourseId = section.CourseId,
                SectionId = section.Id,
                Date = day,
                Status = mark.Status,
                RecordedByUserId = userId,
                RecordedByName = userName,
                RecordedAt = now,
                Notes = string.IsNullOrWhiteSpace(mark.ExistingNote) ? null : mark.ExistingNote.Trim()
            });
            if (mark.Status == AttendanceStatus.Absent)
            {
                var student = await _db.Students.FindAsync(mark.StudentId);
                if (student is not null) absent.Add(student);
            }
        }
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Attendance recorded", nameof(StudentAttendance), section.Id.ToString(), null,
            new { Section = section.Name, Course = section.Course.NameEn, Date = day, Count = marks.Count });

        foreach (var student in absent)
        {
            await _notifications.NotifyGuardianAsync(student,
                "غياب عن حصة",
                "Absence recorded",
                $"تم تسجيل غياب الطالب عن حصة {section.Course.NameAr} بتاريخ {day:yyyy/MM/dd}.",
                $"The student was marked absent in {section.Course.NameEn} on {day:yyyy/MM/dd}.",
                "/Parent/Attendance", NotificationType.Warning);
        }

        TempData["Success"] = _l.T($"Attendance saved for {marks.Count} students.",
            $"تم حفظ حضور {marks.Count} طالباً.");
        return RedirectToAction(nameof(Attendance), new { date = day.ToString("yyyy-MM-dd") });
    }

    // ───────────────────────── Earnings ─────────────────────────

    public async Task<IActionResult> Earnings()
    {
        var teacher = await CurrentTeacherAsync();
        if (teacher is null) return PortalNotLinked();

        var earnings = await _db.TeacherEarnings
            .Include(e => e.Course).ThenInclude(c => c!.Subject)
            .Include(e => e.PrivateLesson).ThenInclude(l => l!.Subject)
            .Where(e => e.TeacherId == teacher.Id)
            .OrderByDescending(e => e.Date).Take(300).ToListAsync();

        var vm = new TeacherEarningsViewModel
        {
            Earnings = earnings,
            Total = await _db.TeacherEarnings.Where(e => e.TeacherId == teacher.Id).SumAsync(e => (long?)e.Amount) ?? 0,
            Paid = await _db.TeacherEarnings.Where(e => e.TeacherId == teacher.Id && e.IsPaid)
                .SumAsync(e => (long?)e.Amount) ?? 0,
            Unpaid = await _db.TeacherEarnings.Where(e => e.TeacherId == teacher.Id && !e.IsPaid)
                .SumAsync(e => (long?)e.Amount) ?? 0,
            Months = earnings.GroupBy(e => new { e.Date.Year, e.Date.Month })
                .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month).Take(6)
                .Select(g => new MonthlyEarningRow(
                    _l.Month(new DateTime(g.Key.Year, g.Key.Month, 1)),
                    g.Sum(x => x.Amount), g.Count()))
                .ToList()
        };
        ViewBag.Teacher = teacher;
        return View(vm);
    }

    // ───────────────────────── Private lessons ─────────────────────────

    public async Task<IActionResult> Requests()
    {
        var teacher = await CurrentTeacherAsync();
        if (teacher is null) return PortalNotLinked();

        var requests = await AssignedRequests(teacher.Id)
            .OrderBy(r => r.Status == RequestStatus.Pending || r.Status == RequestStatus.Matched ? 0 : 1)
            .ThenByDescending(r => r.CreatedAt).Take(100).ToListAsync();
        var lessons = await _db.PrivateLessons
            .Include(l => l.Subject).Include(l => l.Student).Include(l => l.Parent).Include(l => l.Grade)
            .Include(l => l.Request)
            .Where(l => l.TeacherId == teacher.Id)
            .OrderByDescending(l => l.Date).ThenByDescending(l => l.StartTime)
            .Take(100).ToListAsync();

        var today = DateTime.Today;
        var vm = new TeacherRequestsViewModel
        {
            Requests = requests,
            Lessons = lessons,
            OpenRequests = requests.Count(r => r.Status is RequestStatus.Pending or RequestStatus.Matched
                or RequestStatus.Accepted),
            UpcomingLessons = lessons.Count(l => l.Status == PrivateLessonStatus.Scheduled && l.Date >= today),
            CompletedLessons = lessons.Count(l => l.Status == PrivateLessonStatus.Completed)
        };
        ViewBag.Teacher = teacher;
        return View(vm);
    }

    // ───────────────────────── Helpers ─────────────────────────

    private async Task<Teacher?> CurrentTeacherAsync()
    {
        var teacherId = User.TeacherId();
        if (teacherId.HasValue)
        {
            var byId = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == teacherId.Value);
            if (byId is not null) return byId;
        }
        var userId = User.CurrentUserId();
        return await _db.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);
    }

    private IQueryable<CourseSection> SectionsFor(int teacherId) =>
        _db.CourseSections
            .Include(s => s.Course).ThenInclude(c => c.Teacher)
            .Include(s => s.Course).ThenInclude(c => c.Subject)
            .Include(s => s.Course).ThenInclude(c => c.Grade).ThenInclude(g => g.Stage)
            .Include(s => s.Schedules).ThenInclude(x => x.Classroom)
            .Include(s => s.Enrollments)
            .Where(s => s.Course.TeacherId == teacherId || s.TeacherId == teacherId);

    private IQueryable<PrivateLessonRequest> AssignedRequests(int teacherId) =>
        _db.PrivateLessonRequests
            .Include(r => r.Subject).Include(r => r.Grade).ThenInclude(g => g.Stage)
            .Include(r => r.Student).Include(r => r.Parent)
            .Where(r => r.MatchedTeacherId == teacherId || r.PreferredTeacherId == teacherId);

    private async Task<TakeAttendanceViewModel> BuildTakeViewModelAsync(CourseSection section, DateTime day)
    {
        var roster = await _db.Enrollments
            .Include(e => e.Student)
            .Where(e => e.SectionId == section.Id && e.Status != EnrollmentStatus.Cancelled)
            .OrderBy(e => e.Student.FullNameAr)
            .ToListAsync();
        var existing = await _db.StudentAttendances
            .Where(a => a.SectionId == section.Id && a.Date == day)
            .ToDictionaryAsync(a => a.StudentId);

        return new TakeAttendanceViewModel
        {
            SectionId = section.Id,
            SectionName = section.Name,
            CourseName = _l.Pick(section.Course.NameAr, section.Course.NameEn),
            TeacherName = _l.Pick(section.Course.Teacher.FullNameAr, section.Course.Teacher.FullNameEn),
            Date = day,
            AlreadyRecorded = existing.Count > 0,
            Students = roster.Select(e => new AttendanceMark
            {
                StudentId = e.StudentId,
                StudentName = _l.Pick(e.Student.FullNameAr, e.Student.FullNameEn),
                AttendanceId = existing.TryGetValue(e.StudentId, out var a) ? (int?)a.Id : null,
                Status = existing.TryGetValue(e.StudentId, out var rec) ? rec.Status : AttendanceStatus.Present,
                ExistingNote = existing.TryGetValue(e.StudentId, out var r2) ? r2.Notes : null
            }).ToList()
        };
    }

    private IActionResult PortalNotLinked() => View("~/Views/Shared/PortalNotLinked.cshtml");
}
