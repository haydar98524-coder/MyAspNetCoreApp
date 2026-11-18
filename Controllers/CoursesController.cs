using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Administrator + "," + Roles.Manager + "," + Roles.Registrar)]
public class CoursesController : Controller
{
    private readonly AppDbContext _db;
    private readonly Localizer _l;
    private readonly AuditService _audit;

    public CoursesController(AppDbContext db, Localizer l, AuditService audit)
    {
        _db = db;
        _l = l;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? q, int? gradeId, CourseStatus? status)
    {
        var query = _db.Courses
            .Include(c => c.Grade).ThenInclude(g => g.Stage)
            .Include(c => c.Subject)
            .Include(c => c.Teacher)
            .Include(c => c.Sections).ThenInclude(s => s.Enrollments)
            .AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.NameAr.Contains(term) || c.NameEn.Contains(term)
                || c.Code.Contains(term) || c.Teacher.FullNameAr.Contains(term) || c.Subject.NameAr.Contains(term));
        }
        if (gradeId.HasValue) query = query.Where(c => c.GradeId == gradeId.Value);
        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        var rows = await query.OrderByDescending(c => c.Status == CourseStatus.Active).ThenBy(c => c.Grade.Level).ThenBy(c => c.NameAr).ToListAsync();

        ViewBag.Q = q;
        ViewBag.GradeId = gradeId;
        ViewBag.Status = status;
        ViewBag.Grades = await _db.Grades.Include(g => g.Stage).OrderBy(g => g.Stage.Order).ThenBy(g => g.Level).ToListAsync();
        ViewBag.ActiveCount = await _db.Courses.CountAsync(c => c.Status == CourseStatus.Active);
        ViewBag.StudentCount = await _db.Enrollments.Where(e => e.Status == EnrollmentStatus.Active).Select(e => e.StudentId).Distinct().CountAsync();
        ViewBag.Revenue = await _db.Enrollments.Where(e => e.Status == EnrollmentStatus.Active).SumAsync(e => (long?)e.NetFee) ?? 0;
        return View(rows);
    }

    public async Task<IActionResult> Details(int id)
    {
        var course = await _db.Courses
            .Include(c => c.Grade).ThenInclude(g => g.Stage)
            .Include(c => c.Subject)
            .Include(c => c.Teacher)
            .Include(c => c.Sections).ThenInclude(s => s.Teacher)
            .Include(c => c.Sections).ThenInclude(s => s.Schedules).ThenInclude(x => x.Classroom)
            .Include(c => c.Sections).ThenInclude(s => s.Enrollments).ThenInclude(e => e.Student)
            .Include(c => c.Exams)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id);
        if (course is null) return NotFound();
        ViewBag.Classrooms = await _db.Classrooms.Where(c => c.Status == ClassroomStatus.Available).OrderBy(c => c.Number).ToListAsync();
        ViewBag.Teachers = await _db.Teachers.Where(t => t.IsActive).OrderBy(t => t.FullNameAr).ToListAsync();
        ViewBag.Revenue = await _db.Payments.Where(p => p.Enrollment != null && p.Enrollment.CourseId == id).SumAsync(p => (long?)p.Amount) ?? 0;
        ViewBag.TeacherCost = await _db.TeacherEarnings.Where(e => e.CourseId == id).SumAsync(e => (long?)e.Amount) ?? 0;
        ViewBag.AttendanceCount = await _db.StudentAttendances.CountAsync(a => a.CourseId == id);
        return View(course);
    }

    public async Task<IActionResult> Create(int? teacherId, int? gradeId)
    {
        await LoadListsAsync();
        return View("Form", new CourseFormViewModel
        {
            TeacherId = teacherId ?? 0,
            GradeId = gradeId ?? 0,
            TeacherSharePercent = (await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1))?.DefaultTeacherShare ?? 55m
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CourseFormViewModel model)
    {
        var grade = await _db.Grades.Include(g => g.Stage).FirstOrDefaultAsync(g => g.Id == model.GradeId);
        if (grade is null) ModelState.AddModelError(nameof(model.GradeId), _l.T("Select a valid grade.", "اختر صفاً صحيحاً."));
        if (model.EndDate < model.StartDate) ModelState.AddModelError(nameof(model.EndDate), _l.T("End date must be after the start date.", "يجب أن يكون تاريخ الانتهاء بعد تاريخ البدء."));
        if (!ModelState.IsValid)
        {
            await LoadListsAsync();
            return View("Form", model);
        }
        var seq = await _db.Courses.CountAsync() + 1;
        var code = $"C-{seq:D3}";
        while (await _db.Courses.AnyAsync(c => c.Code == code)) { seq++; code = $"C-{seq:D3}"; }
        var course = new Course
        {
            Code = code,
            NameAr = model.NameAr,
            NameEn = model.NameEn,
            StageId = grade!.StageId,
            GradeId = grade.Id,
            SubjectId = model.SubjectId,
            TeacherId = model.TeacherId,
            Type = model.Type,
            Fee = model.Fee,
            TeacherSharePercent = model.TeacherSharePercent,
            StartDate = model.StartDate.Date,
            EndDate = model.EndDate.Date,
            Capacity = model.Capacity,
            Status = model.Status,
            DescriptionAr = model.DescriptionAr,
            DescriptionEn = model.DescriptionEn,
            BranchId = 1
        };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Course", course.Id.ToString(), null, new { course.Code, course.NameAr, course.TeacherId, course.Fee });
        TempData["Success"] = _l.T("Course created. Add sections and schedules next.", "تم إنشاء الدورة. أضف الشعب والجداول الآن.");
        return RedirectToAction(nameof(Details), new { id = course.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var c = await _db.Courses.FindAsync(id);
        if (c is null) return NotFound();
        await LoadListsAsync();
        return View("Form", new CourseFormViewModel
        {
            Id = c.Id,
            NameAr = c.NameAr,
            NameEn = c.NameEn,
            StageId = c.StageId,
            GradeId = c.GradeId,
            SubjectId = c.SubjectId,
            TeacherId = c.TeacherId,
            Type = c.Type,
            Fee = c.Fee,
            TeacherSharePercent = c.TeacherSharePercent,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            Capacity = c.Capacity,
            Status = c.Status,
            DescriptionAr = c.DescriptionAr,
            DescriptionEn = c.DescriptionEn
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CourseFormViewModel model)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course is null) return NotFound();
        var grade = await _db.Grades.FirstOrDefaultAsync(g => g.Id == model.GradeId);
        if (grade is null) ModelState.AddModelError(nameof(model.GradeId), _l.T("Select a valid grade.", "اختر صفاً صحيحاً."));
        if (!ModelState.IsValid)
        {
            await LoadListsAsync();
            return View("Form", model);
        }
        var old = new { course.NameAr, course.TeacherId, course.Fee, course.TeacherSharePercent, course.Status };
        course.NameAr = model.NameAr;
        course.NameEn = model.NameEn;
        course.GradeId = grade!.Id;
        course.StageId = grade.StageId;
        course.SubjectId = model.SubjectId;
        course.TeacherId = model.TeacherId;
        course.Type = model.Type;
        course.Fee = model.Fee;
        course.TeacherSharePercent = model.TeacherSharePercent;
        course.StartDate = model.StartDate.Date;
        course.EndDate = model.EndDate.Date;
        course.Capacity = model.Capacity;
        course.Status = model.Status;
        course.DescriptionAr = model.DescriptionAr;
        course.DescriptionEn = model.DescriptionEn;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Course", id.ToString(), old, new { course.NameAr, course.TeacherId, course.Fee, course.TeacherSharePercent, course.Status });
        TempData["Success"] = _l.T("Course updated.", "تم تحديث الدورة.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSection(int courseId, string name, int capacity, int? teacherId)
    {
        var course = await _db.Courses.FindAsync(courseId);
        if (course is null) return NotFound();
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = _l.T("Section name is required.", "اسم الشعبة مطلوب.");
            return RedirectToAction(nameof(Details), new { id = courseId });
        }
        _db.CourseSections.Add(new CourseSection
        {
            CourseId = courseId,
            Name = name.Trim(),
            Capacity = capacity <= 0 ? course.Capacity : capacity,
            TeacherId = teacherId,
            IsActive = true
        });
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "CourseSection", courseId.ToString(), null, new { name, capacity, teacherId });
        TempData["Success"] = _l.T("Section added.", "تمت إضافة الشعبة.");
        return RedirectToAction(nameof(Details), new { id = courseId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSchedule(int courseId, int sectionId, int classroomId, DayOfWeek dayOfWeek, string startTime, string endTime)
    {
        var section = await _db.CourseSections.Include(s => s.Schedules).FirstOrDefaultAsync(s => s.Id == sectionId && s.CourseId == courseId);
        if (section is null) return NotFound();
        if (TimeSpan.TryParse(startTime, out var start) == false || TimeSpan.TryParse(endTime, out var end) == false || end <= start)
        {
            TempData["Error"] = _l.T("Enter a valid start and end time.", "أدخل وقت بدء وانتهاء صحيحين.");
            return RedirectToAction(nameof(Details), new { id = courseId });
        }
        var conflict = await _db.ClassSchedules.AnyAsync(s => s.ClassroomId == classroomId && s.DayOfWeek == dayOfWeek
            && string.Compare(s.StartTime, endTime) < 0 && string.Compare(s.EndTime, startTime) > 0);
        if (conflict)
        {
            TempData["Error"] = _l.T("This classroom is already booked at that time.", "هذه القاعة محجوزة في هذا الوقت.");
            return RedirectToAction(nameof(Details), new { id = courseId });
        }
        _db.ClassSchedules.Add(new ClassSchedule
        {
            SectionId = sectionId,
            ClassroomId = classroomId,
            DayOfWeek = dayOfWeek,
            StartTime = startTime,
            EndTime = endTime
        });
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "ClassSchedule", sectionId.ToString(), null, new { classroomId, dayOfWeek, startTime, endTime });
        TempData["Success"] = _l.T("Schedule slot added.", "تمت إضافة موعد الحصة.");
        return RedirectToAction(nameof(Details), new { id = courseId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSchedule(int courseId, int id)
    {
        var schedule = await _db.ClassSchedules.FindAsync(id);
        if (schedule is not null)
        {
            _db.ClassSchedules.Remove(schedule);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Delete", "ClassSchedule", id.ToString(), new { schedule.SectionId, schedule.DayOfWeek, schedule.StartTime }, null);
            TempData["Success"] = _l.T("Schedule slot removed.", "تم حذف موعد الحصة.");
        }
        return RedirectToAction(nameof(Details), new { id = courseId });
    }

    private async Task LoadListsAsync()
    {
        ViewBag.Grades = await _db.Grades.Include(g => g.Stage).OrderBy(g => g.Stage.Order).ThenBy(g => g.Level).ToListAsync();
        ViewBag.Subjects = await _db.Subjects.Where(s => s.IsActive).OrderBy(s => s.NameAr).ToListAsync();
        ViewBag.Teachers = await _db.Teachers.Where(t => t.IsActive).OrderBy(t => t.FullNameAr).ToListAsync();
    }
}
