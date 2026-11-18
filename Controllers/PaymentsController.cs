using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Admissions)]
public class PaymentsController : Controller
{
    private readonly AppDbContext _db;
    private readonly Localizer _l;
    private readonly BillingService _billing;
    private readonly CashierShiftService _cashier;
    private readonly AuditService _audit;

    public PaymentsController(AppDbContext db, Localizer l, BillingService billing,
        CashierShiftService cashier, AuditService audit)
    {
        _db = db;
        _l = l;
        _billing = billing;
        _cashier = cashier;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? q, DateTime? from, DateTime? to, PaymentMethod? method, int? studentId)
    {
        var query = _db.Payments
            .Include(p => p.Student).ThenInclude(s => s.Grade)
            .Include(p => p.Enrollment).ThenInclude(e => e!.Course)
            .Include(p => p.Due)
            .AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p => p.ReceiptNo.Contains(term) || p.Student.FullNameAr.Contains(term)
                || p.Student.FullNameEn.Contains(term) || p.Student.StudentNo.Contains(term));
        }
        if (from.HasValue) query = query.Where(p => p.Date >= from.Value.Date);
        if (to.HasValue) query = query.Where(p => p.Date <= to.Value.Date);
        if (method.HasValue) query = query.Where(p => p.Method == method.Value);
        if (studentId.HasValue) query = query.Where(p => p.StudentId == studentId.Value);

        var rows = await query.OrderByDescending(p => p.Date).ThenByDescending(p => p.Id).Take(300).ToListAsync();
        ViewBag.Q = q;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Method = method;
        ViewBag.StudentId = studentId;
        ViewBag.Count = await query.CountAsync();
        ViewBag.Total = await query.SumAsync(p => (long?)p.Amount) ?? 0;
        ViewBag.TodayTotal = await _db.Payments.Where(p => p.Date == DateTime.Today).SumAsync(p => (long?)p.Amount) ?? 0;
        ViewBag.TodayCount = await _db.Payments.CountAsync(p => p.Date == DateTime.Today);
        return View(rows);
    }

    public async Task<IActionResult> Collect(int? studentId, int? dueId)
    {
        await LoadCollectListsAsync();
        var model = new CollectPaymentViewModel { StudentId = studentId ?? 0, DueId = dueId ?? 0 };
        if (studentId.HasValue)
        {
            var dues = await _db.Dues.Where(d => d.StudentId == studentId && d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
                .Include(d => d.Enrollment).ThenInclude(e => e.Course)
                .OrderBy(d => d.DueDate).ToListAsync();
            ViewBag.Dues = dues;
            ViewBag.SelectedStudent = await _db.Students.Include(s => s.Grade).Include(s => s.Parent)
                .FirstOrDefaultAsync(s => s.Id == studentId);
            var due = dues.FirstOrDefault(d => d.Id == dueId) ?? dues.FirstOrDefault();
            if (due is not null)
            {
                model.DueId = due.Id;
                model.Amount = due.Remaining;
            }
        }
        else
        {
            ViewBag.Dues = new List<Due>();
        }
        ViewBag.OpenShift = await _cashier.GetOpenAsync(User.CurrentUserId());
        ViewBag.ActiveShift = await _cashier.GetActiveAsync(User.CurrentUserId());
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Collect(CollectPaymentViewModel model)
    {
        var student = await _db.Students.Include(s => s.Grade).Include(s => s.Parent).FirstOrDefaultAsync(s => s.Id == model.StudentId);
        var due = await _db.Dues.Include(d => d.Enrollment).ThenInclude(e => e.Course).ThenInclude(c => c.Stage)
            .FirstOrDefaultAsync(d => d.Id == model.DueId);
        if (student is null) ModelState.AddModelError(nameof(model.StudentId), _l.T("Select a student.", "اختر طالباً."));
        if (due is null || due.StudentId != model.StudentId)
            ModelState.AddModelError(nameof(model.DueId), _l.T("Select a valid open due for this student.", "اختر قسطاً مفتوحاً صحيحاً لهذا الطالب."));
        else if (model.Amount > due.Remaining)
            ModelState.AddModelError(nameof(model.Amount), _l.T("Amount exceeds the remaining balance.", "المبلغ يتجاوز الرصيد المتبقي."));
        if (due?.Status == DueStatus.Waived) ModelState.AddModelError(nameof(model.DueId), _l.T("This due is waived.", "هذا القسط معفى."));

        var shift = model.Method == PaymentMethod.Cash
            ? await _cashier.GetOpenAsync(User.CurrentUserId())
            : null;
        if (model.Method == PaymentMethod.Cash && shift is null)
            ModelState.AddModelError(nameof(model.Method), _l.T(
                "Open a cashier shift before receiving cash.",
                "افتح وردية الصندوق قبل استلام النقد."));

        if (!ModelState.IsValid)
        {
            await LoadCollectListsAsync();
            ViewBag.Dues = await _db.Dues.Where(d => d.StudentId == model.StudentId && d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
                .Include(d => d.Enrollment).ThenInclude(e => e.Course).OrderBy(d => d.DueDate).ToListAsync();
            ViewBag.SelectedStudent = student;
            ViewBag.OpenShift = await _cashier.GetOpenAsync(User.CurrentUserId());
            ViewBag.ActiveShift = await _cashier.GetActiveAsync(User.CurrentUserId());
            return View(model);
        }

        Payment payment;
        try
        {
            payment = await _billing.ReceiveAsync(student!, model.Amount, model.Method, DateTime.Today, model.Notes,
                User.CurrentUserId(), User.DisplayName(), due, due!.Enrollment, due.Enrollment.Course,
                notifyGuardian: true, shift: shift);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadCollectListsAsync();
            ViewBag.Dues = await _db.Dues.Where(d => d.StudentId == model.StudentId && d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
                .Include(d => d.Enrollment).ThenInclude(e => e.Course).OrderBy(d => d.DueDate).ToListAsync();
            ViewBag.SelectedStudent = student;
            ViewBag.OpenShift = await _cashier.GetOpenAsync(User.CurrentUserId());
            ViewBag.ActiveShift = await _cashier.GetActiveAsync(User.CurrentUserId());
            return View(model);
        }

        await _audit.LogAsync("Collect", "Payment", payment.Id.ToString(), null,
            new { payment.ReceiptNo, payment.StudentId, payment.Amount, payment.Method, DueId = due.Id });
        TempData["Success"] = _l.T($"Receipt {payment.ReceiptNo} issued.", $"تم إصدار السند {payment.ReceiptNo}.");
        return RedirectToAction(nameof(Receipt), new { id = payment.Id });
    }

    public async Task<IActionResult> Receipt(int id)
    {
        var payment = await _db.Payments
            .Include(p => p.Student).ThenInclude(s => s.Grade).ThenInclude(g => g.Stage)
            .Include(p => p.Student).ThenInclude(s => s.Parent)
            .Include(p => p.Enrollment).ThenInclude(e => e!.Course).ThenInclude(c => c.Teacher)
            .Include(p => p.Enrollment).ThenInclude(e => e!.Section)
            .Include(p => p.Due)
            .Include(p => p.CashierShift)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (payment is null) return NotFound();
        ViewBag.Settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1);
        ViewBag.Journal = await _db.JournalEntries.Include(j => j.Lines).ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(j => j.ReferenceType == "Payment" && j.ReferenceId == id.ToString());
        if (!User.IsInRole(Roles.Administrator) && !User.IsInRole(Roles.Manager) && !User.IsInRole(Roles.Accountant)
            && !User.IsInRole(Roles.Registrar) && payment.Student.UserId != User.CurrentUserId()
            && payment.Student.Parent?.UserId != User.CurrentUserId())
        {
            return Forbid();
        }
        return View(payment);
    }

    private async Task LoadCollectListsAsync()
    {
        ViewBag.Students = await _db.Students.Where(s => s.Status == StudentStatus.Active)
            .Include(s => s.Grade).Include(s => s.Parent)
            .OrderBy(s => s.FullNameAr).ToListAsync();
    }
}
