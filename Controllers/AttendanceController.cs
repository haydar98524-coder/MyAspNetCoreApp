using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize]
public class AttendanceController : Controller
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;

    public AttendanceController(AppDbContext db, AuditService audit, NotificationService notifications)
    {
        _db = db;
        _audit = audit;
        _notifications = notifications;
    }

    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Index(DateTime? date, string? filter)
    {
        var day = (date ?? DateTime.Today).Date;
        if (day > DateTime.Today) day = DateTime.Today;

        var sections = await _db.CourseSections
            .AsNoTracking()
            .Include(s => s.Course).ThenInclude(c => c.Teacher)
            .Include(s => s.Schedules).ThenInclude(x => x.Classroom)
            .Include(s => s.Enrollments)
            .Where(s => s.IsActive && s.Course.Status != CourseStatus.Cancelled)
            .ToListAsync();

        var records = await _db.StudentAttendances
            .AsNoTracking()
            .Where(a => a.Date == day)
            .Select(a => new { a.SectionId, a.Status })
            .ToListAsync();

        var rows = sections.Select(s =>
        {
            var schedule = s.Schedules.FirstOrDefault(x => x.DayOfWeek == day.DayOfWeek);
            var mine = records.Where(r => r.SectionId == s.Id).ToList();
            var enrolled = s.Enrollments.Count(e => e.Status != EnrollmentStatus.Cancelled);
            return new AttendanceSectionRow
            {
                Section = s,
                CourseName = s.Course.NameAr,
                TeacherName = s.Course.Teacher.FullNameAr,
                Room = schedule is null ? "" : schedule.Classroom.NameAr,
                Time = schedule is null ? "" : $"{schedule.StartTime} - {schedule.EndTime}",
                Enrolled = enrolled,
                Recorded = mine.Count,
                Present = mine.Count(x => x.Status is AttendanceStatus.Present or AttendanceStatus.Late),
                Absent = mine.Count(x => x.Status == AttendanceStatus.Absent),
                IsScheduled = schedule is not null
            };
        })
        .Where(r => filter != "unscheduled" || r.IsScheduled)
        .OrderByDescending(r => r.IsScheduled)
        .ThenByDescending(r => r.Time)
        .ThenBy(r => r.CourseName)
        .ToList();

        var dayRecords = records.ToList();
        var vm = new AttendanceIndexViewModel
        {
            Date = day,
            Present = dayRecords.Count(r => r.Status is AttendanceStatus.Present or AttendanceStatus.Late),
            Absent = dayRecords.Count(r => r.Status == AttendanceStatus.Absent),
            Late = dayRecords.Count(r => r.Status == AttendanceStatus.Late),
            Excused = dayRecords.Count(r => r.Status == AttendanceStatus.Excused),
            PendingRequests = await _db.AttendanceChangeRequests.CountAsync(r => r.Status == ChangeRequestStatus.Pending),
            Sections = rows
        };
        ViewBag.Filter = filter;
        return View(vm);
    }

    [Authorize(Roles = Roles.TeachingStaff)]
    public async Task<IActionResult> Take(int sectionId, DateTime? date)
    {
        var section = await LoadSectionAsync(sectionId);
        if (section is null) return NotFound();

        var day = (date ?? DateTime.Today).Date;
        if (day > DateTime.Today)
        {
            TempData["Error"] = "لا يمكن تسجيل الحضور بتاريخ مستقبلي.";
            return RedirectToAction(nameof(Index));
        }

        var roster = await _db.Enrollments
            .Include(e => e.Student)
            .Where(e => e.SectionId == sectionId && e.Status != EnrollmentStatus.Cancelled)
            .OrderBy(e => e.Student.FullNameAr)
            .ToListAsync();

        var existing = await _db.StudentAttendances
            .Where(a => a.SectionId == sectionId && a.Date == day)
            .ToDictionaryAsync(a => a.StudentId);

        var vm = new TakeAttendanceViewModel
        {
            SectionId = section.Id,
            SectionName = section.Name,
            CourseName = section.Course.NameAr,
            TeacherName = section.Course.Teacher.FullNameAr,
            Date = day,
            AlreadyRecorded = existing.Count > 0,
            Students = roster.Select(e => new AttendanceMark
            {
                StudentId = e.StudentId,
                StudentName = e.Student.FullNameAr,
                AttendanceId = existing.TryGetValue(e.StudentId, out var a) ? (int?)a.Id : null,
                Status = existing.TryGetValue(e.StudentId, out var rec) ? rec.Status : AttendanceStatus.Present,
                ExistingNote = existing.TryGetValue(e.StudentId, out var r2) ? r2.Notes : null
            }).ToList()
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.TeachingStaff)]
    public async Task<IActionResult> Save(TakeAttendanceViewModel model)
    {
        var section = await LoadSectionAsync(model.SectionId);
        if (section is null) return NotFound();
        var day = model.Date.Date;
        if (day > DateTime.Today)
        {
            TempData["Error"] = "لا يمكن تسجيل الحضور بتاريخ مستقبلي.";
            return RedirectToAction(nameof(Index));
        }

        var already = await _db.StudentAttendances.AnyAsync(a => a.SectionId == model.SectionId && a.Date == day);
        if (already)
        {
            TempData["Warning"] = "تم تسجيل حضور هذه الشعبة مسبقاً. تعديل السجل يتطلب طلب تعديل معتمد من الإدارة.";
            return RedirectToAction(nameof(Take), new { sectionId = model.SectionId, date = day.ToString("yyyy-MM-dd") });
        }

        if (model.Students.Count == 0)
        {
            TempData["Error"] = "لا يوجد طلاب مسجلون في هذه الشعبة.";
            return RedirectToAction(nameof(Index));
        }

        var userId = User.CurrentUserId();
        var userName = User.DisplayName();
        var now = DateTime.UtcNow;
        var absentStudents = new List<Student>();
        foreach (var mark in model.Students)
        {
            var record = new StudentAttendance
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
            };
            _db.StudentAttendances.Add(record);
            if (mark.Status == AttendanceStatus.Absent)
            {
                var student = await _db.Students.FindAsync(mark.StudentId);
                if (student is not null) absentStudents.Add(student);
            }
        }
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Attendance recorded", nameof(StudentAttendance),
            section.Id.ToString(), null, new { Section = section.Name, Date = day, Count = model.Students.Count });

        foreach (var student in absentStudents)
        {
            await _notifications.NotifyGuardianAsync(student,
                "غياب عن حصة",
                "Absence recorded",
                $"تم تسجيل غياب الطالب عن حصة {section.Course.NameAr} بتاريخ {day:yyyy/MM/dd}.",
                $"The student was marked absent in {section.Course.NameEn} on {day:yyyy/MM/dd}.",
                "/Parent/Attendance", NotificationType.Warning);
        }

        TempData["Success"] = $"تم حفظ حضور {model.Students.Count} طالباً.";
        return RedirectToAction(nameof(Index), new { date = day.ToString("yyyy-MM-dd") });
    }

    [Authorize(Roles = Roles.TeachingStaff)]
    public async Task<IActionResult> Requests(string? status)
    {
        ChangeRequestStatus? parsed = Enum.TryParse<ChangeRequestStatus>(status, out var s) ? s : null;
        var query = _db.AttendanceChangeRequests
            .Include(r => r.Attendance).ThenInclude(a => a.Student)
            .Include(r => r.Attendance).ThenInclude(a => a.Section).ThenInclude(x => x.Course)
            .AsNoTracking()
            .AsQueryable();
        if (parsed.HasValue) query = query.Where(r => r.Status == parsed.Value);
        var list = await query.OrderBy(r => r.Status == ChangeRequestStatus.Pending ? 0 : 1)
            .ThenByDescending(r => r.CreatedAt)
            .Take(200).ToListAsync();

        ViewBag.Status = parsed;
        ViewBag.Pending = await _db.AttendanceChangeRequests.CountAsync(r => r.Status == ChangeRequestStatus.Pending);
        ViewBag.Approved = await _db.AttendanceChangeRequests.CountAsync(r => r.Status == ChangeRequestStatus.Approved);
        ViewBag.Rejected = await _db.AttendanceChangeRequests.CountAsync(r => r.Status == ChangeRequestStatus.Rejected);
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Approve(int id, string? note)
    {
        var request = await _db.AttendanceChangeRequests
            .Include(r => r.Attendance).ThenInclude(a => a.Student)
            .Include(r => r.Attendance).ThenInclude(a => a.Section).ThenInclude(x => x.Course)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request is null) return NotFound();
        if (request.Status != ChangeRequestStatus.Pending)
        {
            TempData["Warning"] = "تمت مراجعة هذا الطلب مسبقاً.";
            return RedirectToAction(nameof(Requests));
        }

        var oldStatus = request.Attendance.Status;
        request.Status = ChangeRequestStatus.Approved;
        request.ReviewedByName = User.DisplayName();
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNote = note;
        request.Attendance.Status = request.NewStatus;
        request.Attendance.IsEdited = true;
        request.Attendance.LastEditedByName = User.DisplayName();
        request.Attendance.LastEditedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Attendance change approved", nameof(AttendanceChangeRequest), id.ToString(),
            new { Status = oldStatus }, new { Status = request.NewStatus });
        await _notifications.NotifyUserAsync(request.RequestedByUserId,
            "تمت الموافقة على طلب تعديل الحضور",
            "Attendance change approved",
            $"تم اعتماد تعديل حضور الطالب {request.Attendance.Student.FullNameAr} في {request.Attendance.Section.Course.NameAr}.",
            $"The attendance change for {request.Attendance.Student.FullNameEn} in {request.Attendance.Section.Course.NameEn} was approved.",
            "/Attendance/Requests", NotificationType.Success);

        TempData["Success"] = "تم اعتماد تعديل الحضور.";
        return RedirectToAction(nameof(Requests));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Reject(int id, string? note)
    {
        var request = await _db.AttendanceChangeRequests
            .Include(r => r.Attendance).ThenInclude(a => a.Student)
            .Include(r => r.Attendance).ThenInclude(a => a.Section).ThenInclude(x => x.Course)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request is null) return NotFound();
        if (request.Status != ChangeRequestStatus.Pending)
        {
            TempData["Warning"] = "تمت مراجعة هذا الطلب مسبقاً.";
            return RedirectToAction(nameof(Requests));
        }

        request.Status = ChangeRequestStatus.Rejected;
        request.ReviewedByName = User.DisplayName();
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNote = note;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Attendance change rejected", nameof(AttendanceChangeRequest), id.ToString());
        await _notifications.NotifyUserAsync(request.RequestedByUserId,
            "تم رفض طلب تعديل الحضور",
            "Attendance change rejected",
            $"تم رفض طلب تعديل حضور الطالب {request.Attendance.Student.FullNameAr}. {note}",
            $"The attendance change request for {request.Attendance.Student.FullNameEn} was rejected. {note}",
            "/Attendance/Requests", NotificationType.Warning);

        TempData["Success"] = "تم رفض طلب التعديل.";
        return RedirectToAction(nameof(Requests));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.TeachingStaff + "," + Roles.Teacher)]
    public async Task<IActionResult> RequestChange(long attendanceId, AttendanceStatus newStatus, string reason, string? returnUrl)
    {
        var attendance = await _db.StudentAttendances
            .Include(a => a.Student)
            .Include(a => a.Section).ThenInclude(s => s.Course)
            .FirstOrDefaultAsync(a => a.Id == attendanceId);
        if (attendance is null) return NotFound();
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
        {
            TempData["Error"] = "يرجى كتابة سبب التعديل (5 أحرف على الأقل).";
            return Redirect(returnUrl ?? Url.Action(nameof(Index))!);
        }
        if (attendance.Status == newStatus)
        {
            TempData["Warning"] = "الحالة الجديدة مطابقة للحالة الحالية.";
            return Redirect(returnUrl ?? Url.Action(nameof(Index))!);
        }
        if (await _db.AttendanceChangeRequests.AnyAsync(r => r.AttendanceId == attendanceId && r.Status == ChangeRequestStatus.Pending))
        {
            TempData["Warning"] = "يوجد طلب تعديل قيد المراجعة لهذا السجل.";
            return Redirect(returnUrl ?? Url.Action(nameof(Index))!);
        }

        var request = new AttendanceChangeRequest
        {
            AttendanceId = attendance.Id,
            RequestedByUserId = User.CurrentUserId(),
            RequestedByName = User.DisplayName(),
            NewStatus = newStatus,
            Reason = reason.Trim(),
            Status = ChangeRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _db.AttendanceChangeRequests.Add(request);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Attendance change requested", nameof(AttendanceChangeRequest), request.Id.ToString(),
            new { Status = attendance.Status }, new { request.NewStatus, request.Reason });
        await _notifications.NotifyRoleAsync(Roles.Manager,
            "طلب تعديل حضور جديد",
            "New attendance change request",
            $"قدّم {User.DisplayName()} طلباً لتعديل حضور الطالب {attendance.Student.FullNameAr} في {attendance.Section.Course.NameAr}.",
            $"{User.DisplayName()} requested an attendance change for {attendance.Student.FullNameEn} in {attendance.Section.Course.NameEn}.",
            "/Attendance/Requests", NotificationType.Warning);

        TempData["Success"] = "تم إرسال طلب التعديل إلى الإدارة للموافقة.";
        return Redirect(returnUrl ?? Url.Action(nameof(Index))!);
    }

    private async Task<CourseSection?> LoadSectionAsync(int id) =>
        await _db.CourseSections
            .Include(s => s.Course).ThenInclude(c => c.Teacher)
            .Include(s => s.Schedules).ThenInclude(x => x.Classroom)
            .FirstOrDefaultAsync(s => s.Id == id);
}
