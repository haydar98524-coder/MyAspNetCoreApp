using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Administrator + "," + Roles.Manager + "," + Roles.Registrar)]
public class GradesController : Controller
{
    private readonly AppDbContext _db;
    private readonly Localizer _l;
    private readonly AuditService _audit;

    public GradesController(AppDbContext db, Localizer l, AuditService audit)
    {
        _db = db;
        _l = l;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var stages = await _db.Stages
            .Include(s => s.Grades).ThenInclude(g => g.Students)
            .Include(s => s.Grades).ThenInclude(g => g.Courses)
            .OrderBy(s => s.Order).ToListAsync();
        ViewBag.StudentCount = await _db.Students.CountAsync(s => s.Status == StudentStatus.Active);
        ViewBag.CourseCount = await _db.Courses.CountAsync(c => c.Status == CourseStatus.Active);
        return View(stages);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int stageId, string nameAr, string nameEn, int level, GradeBranch branch)
    {
        if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
        {
            TempData["Error"] = _l.T("Arabic and English grade names are required.", "اسم الصف بالعربية والإنكليزية مطلوب.");
            return RedirectToAction(nameof(Index));
        }
        if (await _db.Grades.AnyAsync(g => g.StageId == stageId && g.Level == level && g.Branch == branch))
        {
            TempData["Error"] = _l.T("This grade and branch already exists in the stage.", "هذا الصف والفرع موجود مسبقاً في المرحلة.");
            return RedirectToAction(nameof(Index));
        }
        var grade = new Grade { StageId = stageId, NameAr = nameAr.Trim(), NameEn = nameEn.Trim(), Level = level, Branch = branch };
        _db.Grades.Add(grade);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Grade", grade.Id.ToString(), null, new { grade.StageId, grade.NameAr, grade.Level, grade.Branch });
        TempData["Success"] = _l.T("Grade added.", "تمت إضافة الصف.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string nameAr, string nameEn, int level, GradeBranch branch)
    {
        var grade = await _db.Grades.FindAsync(id);
        if (grade is null) return NotFound();
        var old = new { grade.NameAr, grade.NameEn, grade.Level, grade.Branch };
        grade.NameAr = nameAr.Trim();
        grade.NameEn = nameEn.Trim();
        grade.Level = level;
        grade.Branch = branch;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Grade", id.ToString(), old, new { grade.NameAr, grade.NameEn, grade.Level, grade.Branch });
        TempData["Success"] = _l.T("Grade updated.", "تم تحديث الصف.");
        return RedirectToAction(nameof(Index));
    }
}
