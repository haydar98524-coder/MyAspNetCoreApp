using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Administrator + "," + Roles.Manager + "," + Roles.Registrar)]
public class SubjectsController : Controller
{
    private readonly AppDbContext _db;
    private readonly Localizer _l;
    private readonly AuditService _audit;

    public SubjectsController(AppDbContext db, Localizer l, AuditService audit)
    {
        _db = db;
        _l = l;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var subjects = await _db.Subjects
            .Include(s => s.Courses).ThenInclude(c => c.Teacher)
            .OrderByDescending(s => s.IsActive).ThenBy(s => s.NameAr)
            .ToListAsync();
        ViewBag.Teachers = await _db.TeacherSubjects.Include(ts => ts.Teacher)
            .GroupBy(ts => ts.SubjectId)
            .Select(g => new { SubjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SubjectId, x => x.Count);
        return View(subjects);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string nameAr, string nameEn, string code, string color)
    {
        if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
        {
            TempData["Error"] = _l.T("Arabic and English subject names are required.", "اسم المادة بالعربية والإنكليزية مطلوب.");
            return RedirectToAction(nameof(Index));
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            var n = await _db.Subjects.CountAsync() + 1;
            code = $"SUB-{n:D2}";
        }
        if (await _db.Subjects.AnyAsync(s => s.Code == code))
        {
            TempData["Error"] = _l.T("Subject code already exists.", "رمز المادة مستخدم مسبقاً.");
            return RedirectToAction(nameof(Index));
        }
        var subject = new Subject
        {
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            Code = code.Trim(),
            Color = string.IsNullOrWhiteSpace(color) ? "#0f766e" : color,
            IsActive = true
        };
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Subject", subject.Id.ToString(), null, new { subject.Code, subject.NameAr });
        TempData["Success"] = _l.T("Subject created.", "تمت إضافة المادة.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string nameAr, string nameEn, string code, string color, bool isActive)
    {
        var subject = await _db.Subjects.FindAsync(id);
        if (subject is null) return NotFound();
        if (await _db.Subjects.AnyAsync(s => s.Code == code && s.Id != id))
        {
            TempData["Error"] = _l.T("Subject code already exists.", "رمز المادة مستخدم مسبقاً.");
            return RedirectToAction(nameof(Index));
        }
        var old = new { subject.NameAr, subject.NameEn, subject.Code, subject.IsActive };
        subject.NameAr = nameAr.Trim();
        subject.NameEn = nameEn.Trim();
        subject.Code = code.Trim();
        subject.Color = string.IsNullOrWhiteSpace(color) ? subject.Color : color;
        subject.IsActive = isActive;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Subject", id.ToString(), old, new { subject.NameAr, subject.NameEn, subject.Code, subject.IsActive });
        TempData["Success"] = _l.T("Subject updated.", "تم تحديث المادة.");
        return RedirectToAction(nameof(Index));
    }
}
