using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Administrator + "," + Roles.Manager + "," + Roles.Registrar)]
public class ParentsController : Controller
{
    private readonly AppDbContext _db;
    private readonly Localizer _l;
    private readonly AuditService _audit;

    public ParentsController(AppDbContext db, Localizer l, AuditService audit)
    {
        _db = db;
        _l = l;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var query = _db.Parents.Include(p => p.Children).AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p => p.FullNameAr.Contains(term) || p.FullNameEn.Contains(term)
                || p.Phone.Contains(term) || (p.Email != null && p.Email.Contains(term)));
        }
        var rows = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        ViewBag.Q = q;
        ViewBag.WithBalance = await _db.Dues.Where(d => d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
            .Select(d => d.Student.ParentId).Distinct().CountAsync();
        return View(rows);
    }

    public async Task<IActionResult> Details(int id)
    {
        var parent = await _db.Parents
            .Include(p => p.Children).ThenInclude(s => s.Grade).ThenInclude(g => g.Stage)
            .Include(p => p.LessonRequests).ThenInclude(r => r.Subject)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (parent is null) return NotFound();
        ViewBag.Children = await _db.Students
            .Where(s => s.ParentId == id)
            .Include(s => s.Grade)
            .Include(s => s.Enrollments).ThenInclude(e => e.Course)
            .Include(s => s.Dues)
            .Include(s => s.Payments)
            .ToListAsync();
        return View(parent);
    }

    public IActionResult Create(string? returnUrl) => View("Form", new ParentFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ParentFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Form", model);
        if (await _db.Parents.AnyAsync(p => p.Phone == model.Phone))
        {
            ModelState.AddModelError(nameof(model.Phone), _l.T("A guardian with this phone already exists.", "يوجد ولي أمر مسجل بهذا الهاتف."));
            return View("Form", model);
        }
        var parent = new Parent
        {
            FullNameAr = model.FullNameAr,
            FullNameEn = string.IsNullOrWhiteSpace(model.FullNameEn) ? model.FullNameAr : model.FullNameEn,
            Phone = model.Phone,
            Email = model.Email,
            Address = model.Address,
            Area = model.Area,
            Occupation = model.Occupation,
            Notes = model.Notes,
            CreatedAt = DateTime.UtcNow
        };
        _db.Parents.Add(parent);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Parent", parent.Id.ToString(), null, new { parent.FullNameAr, parent.Phone });
        TempData["Success"] = _l.T("Guardian record created.", "تم إنشاء سجل ولي الأمر.");
        return RedirectToAction(nameof(Details), new { id = parent.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var p = await _db.Parents.FindAsync(id);
        if (p is null) return NotFound();
        return View("Form", new ParentFormViewModel
        {
            Id = p.Id,
            FullNameAr = p.FullNameAr,
            FullNameEn = p.FullNameEn,
            Phone = p.Phone,
            Email = p.Email,
            Address = p.Address,
            Area = p.Area,
            Occupation = p.Occupation,
            Notes = p.Notes
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ParentFormViewModel model)
    {
        var parent = await _db.Parents.FindAsync(id);
        if (parent is null) return NotFound();
        if (await _db.Parents.AnyAsync(p => p.Phone == model.Phone && p.Id != id))
            ModelState.AddModelError(nameof(model.Phone), _l.T("A guardian with this phone already exists.", "يوجد ولي أمر مسجل بهذا الهاتف."));
        if (!ModelState.IsValid) return View("Form", model);
        var old = new { parent.FullNameAr, parent.Phone, parent.Email, parent.Area };
        parent.FullNameAr = model.FullNameAr;
        parent.FullNameEn = string.IsNullOrWhiteSpace(model.FullNameEn) ? model.FullNameAr : model.FullNameEn;
        parent.Phone = model.Phone;
        parent.Email = model.Email;
        parent.Address = model.Address;
        parent.Area = model.Area;
        parent.Occupation = model.Occupation;
        parent.Notes = model.Notes;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Parent", parent.Id.ToString(), old, new { parent.FullNameAr, parent.Phone, parent.Email, parent.Area });
        TempData["Success"] = _l.T("Guardian record updated.", "تم تحديث بيانات ولي الأمر.");
        return RedirectToAction(nameof(Details), new { id });
    }
}
