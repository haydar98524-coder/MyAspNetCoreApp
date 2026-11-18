using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Admissions)]
public class DuesController : Controller
{
    private readonly AppDbContext _db;
    private readonly Localizer _l;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;

    public DuesController(AppDbContext db, Localizer l, AuditService audit, NotificationService notifications)
    {
        _db = db;
        _l = l;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task<IActionResult> Index(string? q, DueStatus? status, bool overdue = false, int? studentId = null)
    {
        var query = _db.Dues
            .Include(d => d.Student).ThenInclude(s => s.Grade)
            .Include(d => d.Enrollment).ThenInclude(e => e.Course)
            .AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(d => d.Student.FullNameAr.Contains(term) || d.Student.FullNameEn.Contains(term)
                || d.Student.StudentNo.Contains(term) || d.Description.Contains(term)
                || (d.Student.Parent != null && d.Student.Parent.Phone.Contains(term)));
        }
        if (status.HasValue) query = query.Where(d => d.Status == status.Value);
        if (overdue) query = query.Where(d => d.DueDate < DateTime.Today && d.Status != DueStatus.Paid && d.Status != DueStatus.Waived);
        if (studentId.HasValue) query = query.Where(d => d.StudentId == studentId.Value);

        var rows = await query.OrderBy(d => d.Status == DueStatus.Paid || d.Status == DueStatus.Waived)
            .ThenBy(d => d.DueDate).Take(300).ToListAsync();
        var open = _db.Dues.Where(d => d.Status != DueStatus.Paid && d.Status != DueStatus.Waived);
        ViewBag.Q = q;
        ViewBag.Status = status;
        ViewBag.Overdue = overdue;
        ViewBag.StudentId = studentId;
        ViewBag.OpenCount = await open.CountAsync();
        ViewBag.OpenTotal = await open.SumAsync(d => (long?)(d.Amount - d.PaidAmount)) ?? 0;
        ViewBag.OverdueTotal = await open.Where(d => d.DueDate < DateTime.Today)
            .SumAsync(d => (long?)(d.Amount - d.PaidAmount)) ?? 0;
        ViewBag.CollectedMonth = await _db.Payments
            .Where(p => p.Date.Year == DateTime.Today.Year && p.Date.Month == DateTime.Today.Month)
            .SumAsync(p => (long?)p.Amount) ?? 0;
        return View(rows);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Waive(int id, string? reason)
    {
        var due = await _db.Dues.Include(d => d.Student).Include(d => d.Enrollment).ThenInclude(e => e.Course)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (due is null) return NotFound();
        if (due.PaidAmount > 0)
        {
            TempData["Error"] = _l.T("A partially paid due cannot be waived. Collect or adjust it first.", "لا يمكن إعفاء قسط مدفوع جزئياً. حصّله أو عدّله أولاً.");
            return RedirectToAction(nameof(Index));
        }
        due.Status = DueStatus.Waived;
        if (string.IsNullOrWhiteSpace(due.Description)) due.Description = reason ?? "";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Waive", "Due", id.ToString(), new { due.Amount, Status = DueStatus.Unpaid }, new { due.Amount, Status = DueStatus.Waived, reason });
        await _notifications.NotifyGuardianAsync(due.Student,
            "تم إعفاء قسط", "Due waived",
            $"تم إعفاء قسط بمبلغ {_l.Money(due.Amount)} لدورة {due.Enrollment.Course.NameAr}.",
            $"A due of IQD {due.Amount:N0} for {due.Enrollment.Course.NameEn} was waived.",
            $"/Students/Details/{due.StudentId}", NotificationType.Info);
        TempData["Success"] = _l.T("Due waived.", "تم إعفاء القسط.");
        return RedirectToAction(nameof(Index));
    }
}
