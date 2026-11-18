using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Admissions)]
public class StudentsController : Controller
{
    private readonly AppDbContext _db;
    private readonly Localizer _l;
    private readonly AuditService _audit;
    private readonly FileStorageService _files;
    private readonly StudentExportService _exporter;
    private readonly StudentNumberService _numbers;

    public StudentsController(AppDbContext db, Localizer l, AuditService audit, FileStorageService files,
        StudentExportService exporter, StudentNumberService numbers)
    {
        _db = db;
        _l = l;
        _audit = audit;
        _files = files;
        _exporter = exporter;
        _numbers = numbers;
    }

    public async Task<IActionResult> Index(string? q, int? gradeId, StudentStatus? status, int page = 1)
    {
        const int size = 25;
        var query = FilteredQuery(q, gradeId, status)
            .Include(s => s.Grade).ThenInclude(g => g.Stage)
            .Include(s => s.Parent)
            .Include(s => s.Hall);
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(s => s.RegistrationDate).ThenBy(s => s.Id)
            .Skip((Math.Max(1, page) - 1) * size).Take(size).ToListAsync();

        ViewBag.Q = q;
        ViewBag.GradeId = gradeId;
        ViewBag.Status = status;
        ViewBag.Page = Math.Max(1, page);
        ViewBag.PageCount = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        ViewBag.Total = total;
        ViewBag.Grades = await _db.Grades.Include(g => g.Stage).OrderBy(g => g.Stage.Order).ThenBy(g => g.Level).ToListAsync();
        ViewBag.ActiveCount = await _db.Students.CountAsync(s => s.Status == StudentStatus.Active);
        ViewBag.NewCount = await _db.Students.CountAsync(s => s.RegistrationDate.Year == DateTime.Today.Year && s.RegistrationDate.Month == DateTime.Today.Month);
        ViewBag.DueTotal = await _db.Dues.Where(d => d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
            .SumAsync(d => (long?)d.Amount) ?? 0;
        return View(rows);
    }

    public async Task<IActionResult> Details(int id)
    {
        var student = await _db.Students
            .Include(s => s.Grade).ThenInclude(g => g.Stage)
            .Include(s => s.Hall)
            .Include(s => s.Parent)
            .Include(s => s.Subjects).ThenInclude(x => x.Subject)
            .Include(s => s.Teachers).ThenInclude(x => x.Teacher)
            .Include(s => s.Enrollments).ThenInclude(e => e.Course).ThenInclude(c => c.Subject)
            .Include(s => s.Enrollments).ThenInclude(e => e.Section)
            .Include(s => s.Dues).ThenInclude(d => d.Enrollment).ThenInclude(e => e.Course)
            .Include(s => s.Payments).ThenInclude(p => p.Due)
            .Include(s => s.Attendances)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return NotFound();

        ViewBag.Attendance = await _db.StudentAttendances
            .Where(a => a.StudentId == id)
            .Include(a => a.Course).Include(a => a.Section)
            .OrderByDescending(a => a.Date).Take(15).ToListAsync();
        ViewBag.Results = await _db.ExamResults
            .Where(r => r.StudentId == id)
            .Include(r => r.Exam).ThenInclude(e => e.Course)
            .OrderByDescending(r => r.Exam.Date).Take(10).ToListAsync();
        ViewBag.Sections = await _db.CourseSections.Include(s => s.Course)
            .Where(s => s.Course.GradeId == student.GradeId && s.IsActive).ToListAsync();
        ViewBag.Grades = await _db.Grades.Include(g => g.Stage).OrderBy(g => g.Stage.Order).ThenBy(g => g.Level).ToListAsync();
        return View(student);
    }

    public async Task<IActionResult> Create(int? gradeId, int? courseId)
    {
        await LoadListsAsync(gradeId);
        ViewBag.PrefCourse = courseId;
        return View("Form", new StudentFormViewModel { GradeId = gradeId ?? 0, BranchId = 1 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StudentFormViewModel model, int? courseId)
    {
        model.NationalId = (model.NationalId ?? "").Trim();

        if (!ModelState.IsValid)
        {
            await LoadListsAsync(model.GradeId);
            return View("Form", model);
        }
        if (model.BirthDate > DateTime.Today || model.BirthDate < DateTime.Today.AddYears(-60))
            ModelState.AddModelError(nameof(model.BirthDate), _l.T("Enter a valid birth date.", "أدخل تاريخ ميلاد صحيحاً."));

        // The national ID is the human-facing unique key; the database enforces the same rule
        // with a filtered unique index as a second line of defence.
        if (await _db.Students.AnyAsync(s => s.NationalId == model.NationalId))
            ModelState.AddModelError(nameof(model.NationalId),
                _l.T("This national ID is already registered.", "هذا الرقم الوطني مسجل مسبقاً لطالب آخر."));

        if (model.HallId is > 0 && !await _db.Classrooms.AnyAsync(c => c.Id == model.HallId))
        {
            model.HallId = null;
            ModelState.AddModelError(string.Empty, _l.T("The selected hall no longer exists.", "القاعة المختارة لم تعد موجودة."));
        }

        if (!ModelState.IsValid)
        {
            await LoadListsAsync(model.GradeId);
            return View("Form", model);
        }

        var subjectIds = await ResolveSubjectIdsAsync(model.SubjectIds);
        var teacherIds = await ResolveTeacherIdsAsync(subjectIds, model.TeacherIds);

        Parent? parent = null;
        if (model.ParentId.HasValue)
            parent = await _db.Parents.FindAsync(model.ParentId.Value);
        else if (!string.IsNullOrWhiteSpace(model.GuardianName) && !string.IsNullOrWhiteSpace(model.GuardianPhone))
        {
            parent = new Parent
            {
                FullNameAr = model.GuardianName!,
                FullNameEn = model.GuardianName!,
                Phone = model.GuardianPhone!,
                Email = model.GuardianEmail,
                Area = model.Area,
                Address = model.Address,
                CreatedAt = DateTime.UtcNow
            };
            _db.Parents.Add(parent);
        }

        var photo = await _files.SaveAsync(model.Photo, "students");
        // Permanent electronic student number: allocated atomically from a per-year counter,
        // never taken from the form and never edited afterwards.
        var no = await _numbers.AllocateAsync();

        var student = new Student
        {
            StudentNo = no,
            FullNameAr = model.FullNameAr,
            FullNameEn = model.FullNameEn,
            NationalId = model.NationalId,
            Gender = model.Gender,
            BirthDate = model.BirthDate.Date,
            GradeId = model.GradeId,
            HallId = model.HallId,
            Phone = model.Phone,
            SchoolName = model.SchoolName ?? "",
            Parent = parent,
            Address = model.Address,
            Area = model.Area,
            Status = model.Status,
            RegistrationDate = DateTime.Today,
            Notes = model.Notes,
            BranchId = model.BranchId ?? 1,
            PhotoPath = photo,
            QrToken = Guid.NewGuid().ToString("N")
        };
        _db.Students.Add(student);
        await _db.SaveChangesAsync();

        if (subjectIds.Count > 0)
            _db.StudentSubjects.AddRange(subjectIds.Select(id =>
                new StudentSubject { StudentId = student.Id, SubjectId = id }));
        if (teacherIds.Count > 0)
            _db.StudentTeachers.AddRange(teacherIds.Select(id =>
                new StudentTeacher { StudentId = student.Id, TeacherId = id }));
        if (subjectIds.Count > 0 || teacherIds.Count > 0)
            await _db.SaveChangesAsync();

        if (courseId.HasValue)
        {
            var course = await _db.Courses.FindAsync(courseId.Value);
            if (course is not null && course.GradeId == student.GradeId)
            {
                var enrollment = new Enrollment
                {
                    StudentId = student.Id,
                    CourseId = course.Id,
                    EnrollmentDate = DateTime.Today,
                    Fee = course.Fee,
                    NetFee = course.Fee,
                    Status = EnrollmentStatus.Active
                };
                _db.Enrollments.Add(enrollment);
                await _db.SaveChangesAsync();
                _db.Dues.Add(new Due
                {
                    StudentId = student.Id,
                    EnrollmentId = enrollment.Id,
                    Description = _l.Pick(course.NameAr, course.NameEn),
                    Amount = course.Fee,
                    DueDate = DateTime.Today.AddDays(30),
                    Status = course.Fee == 0 ? DueStatus.Waived : DueStatus.Unpaid
                });
                await _db.SaveChangesAsync();
            }
        }

        await _audit.LogAsync("Create", "Student", student.Id.ToString(), null,
            new { student.StudentNo, student.FullNameAr, student.GradeId, student.HallId, student.ParentId });
        TempData["Success"] = _l.T($"Student {student.FullNameEn} registered with number {no}.",
            $"تم تسجيل الطالب {student.FullNameAr} بالرقم {no}.");
        return RedirectToAction(nameof(Details), new { id = student.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var student = await _db.Students.FindAsync(id);
        if (student is null) return NotFound();
        await LoadListsAsync(student.GradeId);
        return View("Form", new StudentFormViewModel
        {
            Id = student.Id,
            StudentNo = student.StudentNo,
            FullNameAr = student.FullNameAr,
            FullNameEn = student.FullNameEn,
            NationalId = student.NationalId ?? "",
            Gender = student.Gender,
            BirthDate = student.BirthDate,
            GradeId = student.GradeId,
            HallId = student.HallId,
            SubjectIds = await _db.StudentSubjects.Where(x => x.StudentId == id)
                .Select(x => x.SubjectId).ToListAsync(),
            TeacherIds = await _db.StudentTeachers.Where(x => x.StudentId == id)
                .Select(x => x.TeacherId).ToListAsync(),
            Phone = student.Phone,
            SchoolName = student.SchoolName,
            ParentId = student.ParentId,
            Address = student.Address,
            Area = student.Area,
            Status = student.Status,
            Notes = student.Notes,
            BranchId = student.BranchId,
            PhotoPath = student.PhotoPath
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StudentFormViewModel model)
    {
        var student = await _db.Students.FindAsync(id);
        if (student is null) return NotFound();

        model.NationalId = (model.NationalId ?? "").Trim();

        if (await _db.Students.AnyAsync(s => s.NationalId == model.NationalId && s.Id != id))
            ModelState.AddModelError(nameof(model.NationalId),
                _l.T("This national ID is already registered.", "هذا الرقم الوطني مسجل مسبقاً لطالب آخر."));

        if (model.HallId is > 0 && !await _db.Classrooms.AnyAsync(c => c.Id == model.HallId))
        {
            model.HallId = null;
            ModelState.AddModelError(string.Empty, _l.T("The selected hall no longer exists.", "القاعة المختارة لم تعد موجودة."));
        }

        if (!ModelState.IsValid)
        {
            model.PhotoPath = student.PhotoPath;
            model.StudentNo = student.StudentNo;
            await LoadListsAsync(model.GradeId);
            return View("Form", model);
        }

        var subjectIds = await ResolveSubjectIdsAsync(model.SubjectIds);
        var teacherIds = await ResolveTeacherIdsAsync(subjectIds, model.TeacherIds);

        var old = new { student.FullNameAr, student.FullNameEn, student.GradeId, student.Status, student.ParentId, student.HallId };
        var photo = await _files.SaveAsync(model.Photo, "students");
        student.FullNameAr = model.FullNameAr;
        student.FullNameEn = model.FullNameEn;
        student.NationalId = model.NationalId;
        student.Gender = model.Gender;
        student.BirthDate = model.BirthDate.Date;
        student.GradeId = model.GradeId;
        student.HallId = model.HallId;
        student.Phone = model.Phone;
        student.SchoolName = model.SchoolName ?? "";
        student.ParentId = model.ParentId;
        student.Address = model.Address;
        student.Area = model.Area;
        student.Status = model.Status;
        student.Notes = model.Notes;
        student.BranchId = model.BranchId;
        if (photo is not null) student.PhotoPath = photo;

        await SyncAssignmentsAsync(student.Id, subjectIds, teacherIds);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Update", "Student", student.Id.ToString(), old,
            new { student.FullNameAr, student.FullNameEn, student.GradeId, student.Status, student.ParentId, student.HallId });
        TempData["Success"] = _l.T("Student record updated.", "تم تحديث بيانات الطالب.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, StudentStatus status)
    {
        var student = await _db.Students.FindAsync(id);
        if (student is null) return NotFound();
        var old = student.Status;
        student.Status = status;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("ChangeStatus", "Student", id.ToString(), new { Status = old }, new { Status = status });
        TempData["Success"] = _l.T("Student status updated.", "تم تحديث حالة الطالب.");
        return RedirectToAction(nameof(Details), new { id });
    }

    // ── Excel export ────────────────────────────────────────────────────────────
    /// <summary>
    /// Exports every student matching the current registry filter, flattened together with the
    /// linked hall, subjects and teachers.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Export(string? q, int? gradeId, StudentStatus? status)
    {
        var students = await FilteredQuery(q, gradeId, status)
            .Include(s => s.Grade)
            .Include(s => s.Hall)
            .Include(s => s.Parent)
            .Include(s => s.Subjects).ThenInclude(x => x.Subject)
            .Include(s => s.Teachers).ThenInclude(x => x.Teacher)
            .AsSplitQuery()
            .OrderBy(s => s.StudentNo)
            .ToListAsync();

        var bytes = _exporter.Build(students);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"students-{DateTime.Today:yyyy-MM-dd}.xlsx");
    }

    // ── Live teacher lookup (used by the registration form) ─────────────────────
    /// <summary>
    /// Returns active teachers, narrowed to those who teach at least one of the selected subjects.
    /// Called whenever the subject selection changes so the teacher list stays in sync with the database.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TeachersForSubjects(int[]? subjectIds)
    {
        var ids = (subjectIds ?? []).Where(id => id > 0).Distinct().ToArray();

        var query = _db.Teachers.AsNoTracking().Where(t => t.IsActive);
        if (ids.Length > 0)
            query = query.Where(t => t.Subjects.Any(ts => ids.Contains(ts.SubjectId)));

        var rows = await query
            .OrderBy(t => t.FullNameAr)
            .Select(t => new
            {
                id = t.Id,
                nameAr = t.FullNameAr,
                nameEn = t.FullNameEn,
                specialization = t.Specialization,
                subjectIds = t.Subjects.Select(ts => ts.SubjectId).ToList(),
                subjectNames = t.Subjects.Select(ts => new { ts.Subject.NameAr, ts.Subject.NameEn }).ToList()
            })
            .ToListAsync();

        return Json(rows.Select(t => new
        {
            id = t.id,
            name = _l.Pick(t.nameAr, t.nameEn),
            specialization = t.specialization,
            subjectIds = t.subjectIds,
            subjects = string.Join(" · ",
                t.subjectNames.Select(x => _l.Pick(x.NameAr, x.NameEn)).Where(x => x.Length > 0))
        }));
    }

    private IQueryable<Student> FilteredQuery(string? q, int? gradeId, StudentStatus? status)
    {
        var query = _db.Students.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(s => s.FullNameAr.Contains(term) || s.FullNameEn.Contains(term)
                || s.StudentNo.Contains(term) || (s.NationalId != null && s.NationalId.Contains(term))
                || (s.Phone != null && s.Phone.Contains(term))
                || (s.Parent != null && s.Parent.Phone.Contains(term)));
        }
        if (gradeId.HasValue) query = query.Where(s => s.GradeId == gradeId.Value);
        if (status.HasValue) query = query.Where(s => s.Status == status.Value);
        return query;
    }

    private async Task<List<int>> ResolveSubjectIdsAsync(IEnumerable<int> selected)
    {
        var ids = selected.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0) return new();
        return await _db.Subjects
            .Where(s => ids.Contains(s.Id) && s.IsActive)
            .Select(s => s.Id)
            .ToListAsync();
    }

    private async Task<List<int>> ResolveTeacherIdsAsync(List<int> subjectIds, IEnumerable<int> selected)
    {
        var ids = selected.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0) return new();

        var query = _db.Teachers.Where(t => ids.Contains(t.Id) && t.IsActive);
        // A teacher may only be assigned when they teach one of the chosen subjects (or when no
        // subject has been chosen yet, in which case the form offers the full active list).
        if (subjectIds.Count > 0)
            query = query.Where(t => t.Subjects.Any(ts => subjectIds.Contains(ts.SubjectId)));

        return await query.Select(t => t.Id).ToListAsync();
    }

    private async Task SyncAssignmentsAsync(int studentId, List<int> subjectIds, List<int> teacherIds)
    {
        var existingSubjects = await _db.StudentSubjects
            .Where(x => x.StudentId == studentId).ToListAsync();
        _db.StudentSubjects.RemoveRange(existingSubjects.Where(x => !subjectIds.Contains(x.SubjectId)));
        var keptSubjectIds = existingSubjects.Select(x => x.SubjectId).ToHashSet();
        foreach (var id in subjectIds.Where(id => !keptSubjectIds.Contains(id)))
            _db.StudentSubjects.Add(new StudentSubject { StudentId = studentId, SubjectId = id });

        var existingTeachers = await _db.StudentTeachers
            .Where(x => x.StudentId == studentId).ToListAsync();
        _db.StudentTeachers.RemoveRange(existingTeachers.Where(x => !teacherIds.Contains(x.TeacherId)));
        var keptTeacherIds = existingTeachers.Select(x => x.TeacherId).ToHashSet();
        foreach (var id in teacherIds.Where(id => !keptTeacherIds.Contains(id)))
            _db.StudentTeachers.Add(new StudentTeacher { StudentId = studentId, TeacherId = id });
    }

    private async Task LoadListsAsync(int? gradeId)
    {
        ViewBag.Grades = await _db.Grades.Include(g => g.Stage).OrderBy(g => g.Stage.Order).ThenBy(g => g.Level).ToListAsync();
        ViewBag.Parents = await _db.Parents.OrderBy(p => p.FullNameAr).ToListAsync();
        ViewBag.Halls = await _db.Classrooms
            .Where(c => c.Status != ClassroomStatus.Retired)
            .OrderBy(c => c.NameAr).ThenBy(c => c.Number)
            .ToListAsync();
        ViewBag.Subjects = await _db.Subjects
            .Where(s => s.IsActive)
            .OrderBy(s => s.NameAr)
            .ToListAsync();
        ViewBag.Teachers = await _db.Teachers
            .Where(t => t.IsActive)
            .Include(t => t.Subjects).ThenInclude(ts => ts.Subject)
            .OrderBy(t => t.FullNameAr)
            .ToListAsync();
        ViewBag.Courses = await _db.Courses
            .Where(c => c.Status == CourseStatus.Active && (!gradeId.HasValue || gradeId == 0 || c.GradeId == gradeId))
            .Include(c => c.Subject).Include(c => c.Teacher)
            .OrderBy(c => c.Grade.Level).ThenBy(c => c.NameAr).ToListAsync();
    }
}
