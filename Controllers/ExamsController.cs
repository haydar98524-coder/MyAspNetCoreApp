using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.TeachingStaff + "," + Roles.Teacher)]
public class ExamsController : Controller
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;
    private readonly Localizer _l;

    public ExamsController(AppDbContext db, AuditService audit, NotificationService notifications, Localizer localizer)
    {
        _db = db;
        _audit = audit;
        _notifications = notifications;
        _l = localizer;
    }

    public async Task<IActionResult> Index(string? q, int? courseId, string? type)
    {
        var today = DateTime.Today;
        ExamType? parsedType = Enum.TryParse<ExamType>(type, out var t) ? t : null;
        var teacherId = TeacherScope();

        var query = _db.Exams
            .AsNoTracking()
            .Include(e => e.Course).ThenInclude(c => c.Teacher)
            .Include(e => e.Course).ThenInclude(c => c.Grade).ThenInclude(g => g.Stage)
            .Include(e => e.Results)
            .Include(e => e.Course).ThenInclude(c => c.Enrollments)
            .AsQueryable();

        if (teacherId.HasValue) query = query.Where(e => e.Course.TeacherId == teacherId.Value);
        if (courseId.HasValue) query = query.Where(e => e.CourseId == courseId.Value);
        if (parsedType.HasValue) query = query.Where(e => e.Type == parsedType.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(e => e.NameAr.Contains(term) || e.NameEn.Contains(term) ||
                                     e.Course.NameAr.Contains(term) || e.Course.NameEn.Contains(term));
        }

        var exams = await query.OrderBy(e => e.Date).ThenBy(e => e.Course.NameAr).ToListAsync();
        var rows = exams.Select(e =>
        {
            var enrolled = e.Course.Enrollments.Count(x => x.Status != EnrollmentStatus.Cancelled);
            var recorded = e.Results.Count;
            var passed = e.Results.Count(r => r.Mark >= e.PassMark);
            return new ExamIndexRow
            {
                Exam = e,
                CourseName = _l.Pick(e.Course.NameAr, e.Course.NameEn),
                TeacherName = _l.Pick(e.Course.Teacher.FullNameAr, e.Course.Teacher.FullNameEn),
                GradeName = _l.Pick(e.Course.Grade.NameAr, e.Course.Grade.NameEn),
                Enrolled = enrolled,
                Recorded = recorded,
                Passed = passed,
                Average = recorded == 0 ? 0 : Math.Round(e.Results.Average(r => r.Mark), 1)
            };
        }).ToList();

        var vm = new ExamsIndexViewModel
        {
            Total = rows.Count,
            Upcoming = rows.Count(r => r.Exam.Date >= today),
            Recorded = rows.Where(r => r.Recorded > 0).Sum(r => r.Recorded),
            AveragePassRate = rows.Where(r => r.Recorded > 0) is var withResults && withResults.Any()
                ? Math.Round(withResults.Sum(r => (decimal)r.Passed) / withResults.Sum(r => (decimal)r.Recorded) * 100, 1)
                : 0,
            Rows = rows
        };
        ViewBag.Q = q;
        ViewBag.CourseId = courseId;
        ViewBag.Type = parsedType;
        ViewBag.Courses = await CourseOptionsAsync(teacherId);
        return View(vm);
    }

    [Authorize(Roles = Roles.TeachingStaff + "," + Roles.Teacher)]
    public async Task<IActionResult> Create(int? courseId)
    {
        var vm = new ExamFormViewModel
        {
            CourseId = courseId ?? 0,
            Date = DateTime.Today.AddDays(7)
        };
        await LoadListsAsync(vm);
        return View("Form", vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.TeachingStaff + "," + Roles.Teacher)]
    public async Task<IActionResult> Create(ExamFormViewModel model)
    {
        await ValidateCourseAsync(model);
        if (!ModelState.IsValid)
        {
            await LoadListsAsync(model);
            return View("Form", model);
        }

        var exam = new Exam
        {
            CourseId = model.CourseId,
            NameAr = model.NameAr.Trim(),
            NameEn = model.NameEn.Trim(),
            Type = model.Type,
            Date = model.Date.Date,
            MaxMark = model.MaxMark,
            PassMark = model.PassMark
        };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Exam created", nameof(Exam), exam.Id.ToString(), null,
            new { exam.NameAr, exam.Type, exam.Date, exam.MaxMark, exam.PassMark });
        TempData["Success"] = _l.T("The exam was created. You can now enter marks.",
            "تم إنشاء الامتحان. يمكنك الآن إدخال الدرجات.");
        return RedirectToAction(nameof(Results), new { id = exam.Id });
    }

    [Authorize(Roles = Roles.TeachingStaff + "," + Roles.Teacher)]
    public async Task<IActionResult> Edit(int id)
    {
        var exam = await ExamQuery().FirstOrDefaultAsync(e => e.Id == id);
        if (exam is null) return NotFound();
        if (!await CanAccessCourseAsync(exam.CourseId)) return Forbid();

        var vm = new ExamFormViewModel
        {
            Id = exam.Id,
            CourseId = exam.CourseId,
            NameAr = exam.NameAr,
            NameEn = exam.NameEn,
            Type = exam.Type,
            Date = exam.Date,
            MaxMark = exam.MaxMark,
            PassMark = exam.PassMark
        };
        await LoadListsAsync(vm);
        return View("Form", vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.TeachingStaff + "," + Roles.Teacher)]
    public async Task<IActionResult> Edit(int id, ExamFormViewModel model)
    {
        var exam = await ExamQuery().FirstOrDefaultAsync(e => e.Id == id);
        if (exam is null) return NotFound();
        if (!await CanAccessCourseAsync(exam.CourseId)) return Forbid();
        await ValidateCourseAsync(model);

        if (!ModelState.IsValid)
        {
            model.Id = id;
            await LoadListsAsync(model);
            return View("Form", model);
        }

        var old = new { exam.NameAr, exam.Type, exam.Date, exam.MaxMark, exam.PassMark };
        exam.CourseId = model.CourseId;
        exam.NameAr = model.NameAr.Trim();
        exam.NameEn = model.NameEn.Trim();
        exam.Type = model.Type;
        exam.Date = model.Date.Date;
        exam.MaxMark = model.MaxMark;
        exam.PassMark = model.PassMark;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Exam updated", nameof(Exam), exam.Id.ToString(), old,
            new { exam.NameAr, exam.Type, exam.Date, exam.MaxMark, exam.PassMark });
        TempData["Success"] = _l.T("Exam details updated.", "تم تحديث بيانات الامتحان.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Delete(int id)
    {
        var exam = await _db.Exams.Include(e => e.Results).FirstOrDefaultAsync(e => e.Id == id);
        if (exam is null) return NotFound();
        if (exam.Results.Count > 0)
        {
            TempData["Error"] = _l.T("This exam has recorded marks. Remove the marks before deleting it.",
                "هذا الامتحان يحتوي على درجات مسجلة. احذف الدرجات قبل حذف الامتحان.");
            return RedirectToAction(nameof(Index));
        }
        _db.Exams.Remove(exam);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Exam deleted", nameof(Exam), id.ToString(), new { exam.NameAr });
        TempData["Success"] = _l.T("Exam deleted.", "تم حذف الامتحان.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>An exam's page is its results sheet; /Exams/Details/{id} is kept as an alias.</summary>
    public IActionResult Details(int id) => RedirectToAction(nameof(Results), new { id });

    [Authorize(Roles = Roles.TeachingStaff + "," + Roles.Teacher)]
    public async Task<IActionResult> Results(int id)
    {
        var exam = await _db.Exams
            .AsNoTracking()
            .Include(e => e.Course).ThenInclude(c => c.Teacher)
            .Include(e => e.Course).ThenInclude(c => c.Grade)
            .Include(e => e.Results).ThenInclude(r => r.Student)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (exam is null) return NotFound();
        if (!await CanAccessCourseAsync(exam.CourseId)) return Forbid();

        var rosterStudentIds = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == exam.CourseId && e.Status != EnrollmentStatus.Cancelled)
            .Select(e => e.StudentId)
            .Distinct()
            .ToListAsync();
        var roster = await _db.Students
            .AsNoTracking()
            .Where(s => rosterStudentIds.Contains(s.Id))
            .OrderBy(s => s.FullNameAr)
            .ToListAsync();

        var marks = exam.Results.ToDictionary(r => r.StudentId);
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1);
        var vm = new ExamResultsViewModel
        {
            ExamId = exam.Id,
            ExamName = _l.Pick(exam.NameAr, exam.NameEn),
            CourseName = _l.Pick(exam.Course.NameAr, exam.Course.NameEn),
            MaxMark = exam.MaxMark,
            PassMark = exam.PassMark,
            Rows = roster.Select(s => new ExamResultRow
            {
                StudentId = s.Id,
                StudentName = _l.Pick(s.FullNameAr, s.FullNameEn),
                StudentNo = s.StudentNo,
                Mark = marks.TryGetValue(s.Id, out var r) ? r.Mark : null
            }).ToList()
        };
        ViewData["ExamDate"] = exam.Date;
        ViewData["ExamType"] = exam.Type;
        ViewData["TeacherName"] = _l.Pick(exam.Course.Teacher.FullNameAr, exam.Course.Teacher.FullNameEn);
        ViewData["GradeName"] = _l.Pick(exam.Course.Grade.NameAr, exam.Course.Grade.NameEn);
        ViewData["DefaultPassMark"] = settings?.PassMark ?? 50;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.TeachingStaff + "," + Roles.Teacher)]
    public async Task<IActionResult> SaveResults(ExamResultsViewModel model)
    {
        var exam = await _db.Exams
            .Include(e => e.Course).ThenInclude(c => c.Teacher)
            .Include(e => e.Results)
            .FirstOrDefaultAsync(e => e.Id == model.ExamId);
        if (exam is null) return NotFound();
        if (!await CanAccessCourseAsync(exam.CourseId)) return Forbid();

        var enrolledIds = await _db.Enrollments
            .Where(e => e.CourseId == exam.CourseId && e.Status != EnrollmentStatus.Cancelled)
            .Select(e => e.StudentId)
            .Distinct()
            .ToListAsync();
        var enrolledSet = enrolledIds.ToHashSet();

        foreach (var row in model.Rows.Where(r => enrolledSet.Contains(r.StudentId)))
        {
            var existing = exam.Results.FirstOrDefault(r => r.StudentId == row.StudentId);
            if (!row.Mark.HasValue)
            {
                if (existing is not null) _db.ExamResults.Remove(existing);
                continue;
            }

            var mark = Math.Clamp(row.Mark.Value, 0, exam.MaxMark);
            if (existing is null)
            {
                _db.ExamResults.Add(new ExamResult { ExamId = exam.Id, StudentId = row.StudentId, Mark = mark });
            }
            else
            {
                existing.Mark = mark;
            }
        }
        await _db.SaveChangesAsync();

        var recorded = await _db.ExamResults.CountAsync(r => r.ExamId == exam.Id);
        var passed = await _db.ExamResults.CountAsync(r => r.ExamId == exam.Id && r.Mark >= exam.PassMark);
        await _audit.LogAsync("Exam results saved", nameof(Exam), exam.Id.ToString(), null,
            new { exam.NameAr, Recorded = recorded, Passed = passed });

        if (recorded > 0)
        {
            var failed = await _db.ExamResults
                .Where(r => r.ExamId == exam.Id && r.Mark < exam.PassMark)
                .Include(r => r.Student)
                .Take(50)
                .ToListAsync();
            foreach (var result in failed)
            {
                await _notifications.NotifyGuardianAsync(result.Student,
                    "نتيجة امتحان",
                    "Exam result",
                    $"حصل الطالب على {result.Mark:0.#} من {exam.MaxMark} في {exam.NameAr} — {exam.Course.NameAr}، دون درجة النجاح.",
                    $"The student scored {result.Mark:0.#}/{exam.MaxMark} in {exam.NameEn} — {exam.Course.NameEn}, below the pass mark.",
                    $"/Students/Details/{result.StudentId}", NotificationType.Warning);
            }
        }

        TempData["Success"] = _l.T($"Marks saved: {recorded} results, {passed} passed.",
            $"تم حفظ الدرجات: {recorded} نتيجة، {passed} ناجح.");
        return RedirectToAction(nameof(Results), new { id = exam.Id });
    }

    private int? TeacherScope() =>
        User.IsInRole(Roles.Teacher) && !User.IsStaff() ? User.TeacherId() : null;

    private IQueryable<Exam> ExamQuery() => _db.Exams
        .Include(e => e.Course).ThenInclude(c => c.Teacher)
        .Include(e => e.Course).ThenInclude(c => c.Grade)
        .Include(e => e.Results);

    private async Task<bool> CanAccessCourseAsync(int courseId)
    {
        var teacherId = TeacherScope();
        if (!teacherId.HasValue) return true;
        return await _db.Courses.AnyAsync(c => c.Id == courseId && c.TeacherId == teacherId.Value);
    }

    private async Task ValidateCourseAsync(ExamFormViewModel model)
    {
        if (model.PassMark > model.MaxMark)
            ModelState.AddModelError(nameof(model.PassMark),
                _l.T("The pass mark cannot exceed the maximum mark.", "درجة النجاح لا يمكن أن تتجاوز الدرجة العظمى."));
        if (!await CanAccessCourseAsync(model.CourseId))
            ModelState.AddModelError(nameof(model.CourseId),
                _l.T("You can only manage exams for your own courses.", "يمكنك إدارة امتحانات دوراتك فقط."));
        var courseExists = await _db.Courses.AnyAsync(c => c.Id == model.CourseId);
        if (!courseExists)
            ModelState.AddModelError(nameof(model.CourseId),
                _l.T("Please choose a valid course.", "يرجى اختيار دورة صحيحة."));
    }

    private async Task LoadListsAsync(ExamFormViewModel model)
    {
        ViewBag.Courses = await CourseOptionsAsync(TeacherScope());
        ViewBag.SelectedCourse = model.CourseId;
    }

    private async Task<List<Course>> CourseOptionsAsync(int? teacherId)
    {
        var query = _db.Courses.AsNoTracking()
            .Include(c => c.Grade).ThenInclude(g => g.Stage)
            .Include(c => c.Subject)
            .Include(c => c.Teacher)
            .AsQueryable();
        if (teacherId.HasValue) query = query.Where(c => c.TeacherId == teacherId.Value);
        return await query
            .OrderBy(c => c.Grade.Level).ThenBy(c => c.Subject.NameAr)
            .ToListAsync();
    }
}
