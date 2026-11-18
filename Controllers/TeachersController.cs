using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

public class TeachersController : Controller
{
    private readonly AppDbContext _db;
    private readonly Localizer _l;
    private readonly AuditService _audit;
    private readonly FileStorageService _files;

    public TeachersController(AppDbContext db, Localizer l, AuditService audit, FileStorageService files)
    {
        _db = db;
        _l = l;
        _audit = audit;
        _files = files;
    }

    [Authorize(Roles = Roles.Administrator + "," + Roles.Manager + "," + Roles.Hr + "," + Roles.Registrar)]
    public async Task<IActionResult> Index(string? q, bool? active)
    {
        var query = _db.Teachers
            .Include(t => t.Subjects).ThenInclude(s => s.Subject)
            .Include(t => t.Grades).ThenInclude(g => g.Grade)
            .Include(t => t.Courses)
            .AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(t => t.FullNameAr.Contains(term) || t.FullNameEn.Contains(term)
                || t.Code.Contains(term) || t.Phone.Contains(term) || t.Specialization.Contains(term));
        }
        if (active.HasValue) query = query.Where(t => t.IsActive == active.Value);
        var rows = await query.OrderByDescending(t => t.IsActive).ThenBy(t => t.FullNameAr).ToListAsync();
        ViewBag.Q = q;
        ViewBag.Active = active;
        ViewBag.TeacherCount = await _db.Teachers.CountAsync(t => t.IsActive);
        ViewBag.PublicCount = await _db.Teachers.CountAsync(t => t.IsActive && t.IsPublic);
        ViewBag.Rating = await _db.Teachers.Where(t => t.RatingCount > 0).AverageAsync(t => (double?)t.RatingAvg) ?? 0;
        return View(rows);
    }

    [Authorize(Roles = Roles.Administrator + "," + Roles.Manager + "," + Roles.Hr + "," + Roles.Registrar)]
    public async Task<IActionResult> Details(int id)
    {
        var teacher = await _db.Teachers
            .Include(t => t.Subjects).ThenInclude(s => s.Subject)
            .Include(t => t.Grades).ThenInclude(g => g.Grade).ThenInclude(g => g.Stage)
            .Include(t => t.Certificates)
            .Include(t => t.Availabilities)
            .Include(t => t.Courses).ThenInclude(c => c.Grade)
            .Include(t => t.Courses).ThenInclude(c => c.Subject)
            .Include(t => t.Ratings)
            .Include(t => t.RentContracts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id);
        if (teacher is null) return NotFound();
        ViewBag.Earnings = await _db.TeacherEarnings.Where(e => e.TeacherId == id)
            .OrderByDescending(e => e.Date).Take(10).ToListAsync();
        ViewBag.Earned = await _db.TeacherEarnings.Where(e => e.TeacherId == id).SumAsync(e => (long?)e.Amount) ?? 0;
        ViewBag.Unpaid = await _db.TeacherEarnings.Where(e => e.TeacherId == id && !e.IsPaid).SumAsync(e => (long?)e.Amount) ?? 0;
        ViewBag.StudentCount = await _db.Enrollments
            .Where(e => e.Course.TeacherId == id && e.Status == EnrollmentStatus.Active)
            .Select(e => e.StudentId).Distinct().CountAsync();
        return View(teacher);
    }

    [Authorize(Roles = Roles.Administrator + "," + Roles.Manager + "," + Roles.Hr + "," + Roles.Registrar)]
    public async Task<IActionResult> Create()
    {
        await LoadListsAsync();
        return View("Form", new TeacherFormViewModel());
    }

    [Authorize(Roles = Roles.Administrator + "," + Roles.Manager + "," + Roles.Hr + "," + Roles.Registrar)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TeacherFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LoadListsAsync();
            return View("Form", model);
        }
        var seq = await _db.Teachers.CountAsync() + 1;
        var code = $"T-{seq:D3}";
        while (await _db.Teachers.AnyAsync(t => t.Code == code))
        {
            seq++;
            code = $"T-{seq:D3}";
        }
        var teacher = new Teacher
        {
            Code = code,
            FullNameAr = model.FullNameAr,
            FullNameEn = model.FullNameEn,
            Gender = model.Gender,
            Phone = model.Phone,
            Email = model.Email,
            Specialization = model.Specialization,
            Certificate = model.Certificate,
            University = model.University,
            GraduationYear = model.GraduationYear,
            ExperienceYears = model.ExperienceYears,
            TeachingMode = model.TeachingMode,
            Areas = model.Areas,
            BioAr = model.BioAr,
            BioEn = model.BioEn,
            IsPublic = model.IsPublic,
            IsActive = model.IsActive,
            FixedMonthlySalary = model.FixedMonthlySalary,
            HireDate = model.HireDate.Date,
            PhotoPath = await _files.SaveAsync(model.Photo, "teachers"),
            CreatedAt = DateTime.UtcNow,
            Subjects = model.SubjectIds.Distinct().Select(id => new TeacherSubject { SubjectId = id }).ToList(),
            Grades = model.GradeIds.Distinct().Select(id => new TeacherGrade { GradeId = id }).ToList()
        };
        _db.Teachers.Add(teacher);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Teacher", teacher.Id.ToString(), null, new { teacher.Code, teacher.FullNameAr, model.SubjectIds, model.GradeIds });
        TempData["Success"] = _l.T($"Teacher {teacher.FullNameEn} added.", $"تمت إضافة المدرّس {teacher.FullNameAr}.");
        return RedirectToAction(nameof(Details), new { id = teacher.Id });
    }

    [Authorize(Roles = Roles.Administrator + "," + Roles.Manager + "," + Roles.Hr + "," + Roles.Registrar)]
    public async Task<IActionResult> Edit(int id)
    {
        var t = await _db.Teachers.Include(x => x.Subjects).Include(x => x.Grades).FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        await LoadListsAsync();
        return View("Form", new TeacherFormViewModel
        {
            Id = t.Id,
            FullNameAr = t.FullNameAr,
            FullNameEn = t.FullNameEn,
            Gender = t.Gender,
            Phone = t.Phone,
            Email = t.Email,
            Specialization = t.Specialization,
            Certificate = t.Certificate,
            University = t.University,
            GraduationYear = t.GraduationYear,
            ExperienceYears = t.ExperienceYears,
            TeachingMode = t.TeachingMode,
            Areas = t.Areas,
            BioAr = t.BioAr,
            BioEn = t.BioEn,
            IsPublic = t.IsPublic,
            IsActive = t.IsActive,
            FixedMonthlySalary = t.FixedMonthlySalary,
            HireDate = t.HireDate,
            PhotoPath = t.PhotoPath,
            SubjectIds = t.Subjects.Select(s => s.SubjectId).ToList(),
            GradeIds = t.Grades.Select(g => g.GradeId).ToList()
        });
    }

    [Authorize(Roles = Roles.Administrator + "," + Roles.Manager + "," + Roles.Hr + "," + Roles.Registrar)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TeacherFormViewModel model)
    {
        var teacher = await _db.Teachers.Include(t => t.Subjects).Include(t => t.Grades).FirstOrDefaultAsync(t => t.Id == id);
        if (teacher is null) return NotFound();
        if (!ModelState.IsValid)
        {
            model.PhotoPath = teacher.PhotoPath;
            await LoadListsAsync();
            return View("Form", model);
        }
        var old = new { teacher.FullNameAr, teacher.FullNameEn, teacher.TeachingMode, model.SubjectIds, model.GradeIds, teacher.IsActive };
        var photo = await _files.SaveAsync(model.Photo, "teachers");
        teacher.FullNameAr = model.FullNameAr;
        teacher.FullNameEn = model.FullNameEn;
        teacher.Gender = model.Gender;
        teacher.Phone = model.Phone;
        teacher.Email = model.Email;
        teacher.Specialization = model.Specialization;
        teacher.Certificate = model.Certificate;
        teacher.University = model.University;
        teacher.GraduationYear = model.GraduationYear;
        teacher.ExperienceYears = model.ExperienceYears;
        teacher.TeachingMode = model.TeachingMode;
        teacher.Areas = model.Areas;
        teacher.BioAr = model.BioAr;
        teacher.BioEn = model.BioEn;
        teacher.IsPublic = model.IsPublic;
        teacher.IsActive = model.IsActive;
        teacher.FixedMonthlySalary = model.FixedMonthlySalary;
        teacher.HireDate = model.HireDate.Date;
        if (photo is not null) teacher.PhotoPath = photo;

        _db.TeacherSubjects.RemoveRange(teacher.Subjects);
        _db.TeacherGrades.RemoveRange(teacher.Grades);
        teacher.Subjects = model.SubjectIds.Distinct().Select(x => new TeacherSubject { TeacherId = id, SubjectId = x }).ToList();
        teacher.Grades = model.GradeIds.Distinct().Select(x => new TeacherGrade { TeacherId = id, GradeId = x }).ToList();
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Teacher", id.ToString(), old,
            new { teacher.FullNameAr, teacher.FullNameEn, teacher.TeachingMode, model.SubjectIds, model.GradeIds, teacher.IsActive });
        TempData["Success"] = _l.T("Teacher record updated.", "تم تحديث بيانات المدرّس.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [AllowAnonymous]
    public async Task<IActionResult> Directory(string? q, int? subjectId, int? gradeId, TeachingMode? mode, string? area)
    {
        var query = _db.Teachers
            .Include(t => t.Subjects).ThenInclude(s => s.Subject)
            .Include(t => t.Grades).ThenInclude(g => g.Grade)
            .Include(t => t.Availabilities)
            .Where(t => t.IsActive && t.IsPublic)
            .AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(t => t.FullNameAr.Contains(q) || t.FullNameEn.Contains(q) || t.Specialization.Contains(q) || t.Areas.Contains(q));
        if (subjectId.HasValue) query = query.Where(t => t.Subjects.Any(s => s.SubjectId == subjectId));
        if (gradeId.HasValue) query = query.Where(t => t.Grades.Any(g => g.GradeId == gradeId));
        if (mode.HasValue)
            query = query.Where(t => mode == TeachingMode.Institute
                ? t.TeachingMode == TeachingMode.Institute || t.TeachingMode == TeachingMode.Both
                : t.TeachingMode == TeachingMode.Home || t.TeachingMode == TeachingMode.Both);
        if (!string.IsNullOrWhiteSpace(area)) query = query.Where(t => t.Areas.Contains(area));

        var rows = await query.OrderByDescending(t => t.RatingAvg).ThenByDescending(t => t.ExperienceYears).ToListAsync();
        ViewBag.Q = q;
        ViewBag.SubjectId = subjectId;
        ViewBag.GradeId = gradeId;
        ViewBag.Mode = mode;
        ViewBag.Area = area;
        ViewBag.Subjects = await _db.Subjects.Where(s => s.IsActive).OrderBy(s => s.NameAr).ToListAsync();
        ViewBag.Grades = await _db.Grades.Include(g => g.Stage).OrderBy(g => g.Stage.Order).ThenBy(g => g.Level).ToListAsync();
        return View(rows);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Profile(int id)
    {
        var teacher = await _db.Teachers
            .Include(t => t.Subjects).ThenInclude(s => s.Subject)
            .Include(t => t.Grades).ThenInclude(g => g.Grade).ThenInclude(g => g.Stage)
            .Include(t => t.Certificates)
            .Include(t => t.Availabilities)
            .Include(t => t.Courses).ThenInclude(c => c.Subject)
            .Include(t => t.Courses).ThenInclude(c => c.Grade)
            .Include(t => t.Ratings.Where(r => r.IsVisible))
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id && t.IsActive && t.IsPublic);
        if (teacher is null) return NotFound();
        ViewBag.OtherTeachers = await _db.Teachers.Where(t => t.IsActive && t.IsPublic && t.Id != id)
            .OrderByDescending(t => t.RatingAvg).Take(3).ToListAsync();
        return View(teacher);
    }

    private async Task LoadListsAsync()
    {
        ViewBag.Subjects = await _db.Subjects.Where(s => s.IsActive).OrderBy(s => s.NameAr).ToListAsync();
        ViewBag.Grades = await _db.Grades.Include(g => g.Stage).OrderBy(g => g.Stage.Order).ThenBy(g => g.Level).ToListAsync();
    }
}
