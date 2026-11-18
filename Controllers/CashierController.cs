using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

/// <summary>
/// Cashier drawer control: open a shift, take cash receipts, close with a blind denomination
/// count, and let leadership approve any variance before the cashier may open a new drawer.
/// </summary>
[Authorize(Roles = Roles.Cashiers)]
public class CashierController : Controller
{
    private readonly AppDbContext _db;
    private readonly CashierShiftService _shifts;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;
    private readonly Localizer _l;

    public CashierController(AppDbContext db, CashierShiftService shifts,
        AuditService audit, NotificationService notifications, Localizer l)
    {
        _db = db;
        _shifts = shifts;
        _audit = audit;
        _notifications = notifications;
        _l = l;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.CurrentUserId();
        var active = await _shifts.GetActiveAsync(userId);
        var recent = await _db.CashierShifts.AsNoTracking()
            .OrderByDescending(s => s.OpenedAt).Take(40).ToListAsync();
        var today = DateTime.Today;

        ViewBag.Active = active;
        ViewBag.Recent = recent;
        ViewBag.TodayCash = await _db.Payments
            .Where(p => p.Method == PaymentMethod.Cash && p.Date == today)
            .SumAsync(p => (long?)p.Amount) ?? 0;
        ViewBag.TodayReceipts = await _db.Payments
            .CountAsync(p => p.Method == PaymentMethod.Cash && p.Date == today);
        ViewBag.Pending = await _db.CashierShifts.CountAsync(s => s.Status == CashierShiftStatus.PendingApproval);
        ViewBag.MyShifts = recent.Count(s => s.CashierUserId == userId);
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(OpenCashierShiftViewModel model)
    {
        try
        {
            var shift = await _shifts.OpenAsync(model.OpeningFloat, User.CurrentUserId(), User.DisplayName());
            await _audit.LogAsync("Open shift", "CashierShift", shift.Id.ToString(), null,
                new { shift.ShiftNo, shift.OpeningFloat });
            TempData["Success"] = _l.T($"Shift {shift.ShiftNo} opened.", $"تم فتح وردية {shift.ShiftNo}.");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Close(int id)
    {
        var shift = await _db.CashierShifts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (shift is null) return NotFound();
        if (shift.CashierUserId != User.CurrentUserId()) return Forbid();
        if (shift.Status != CashierShiftStatus.Open)
        {
            TempData["Error"] = _l.T("This shift is already closed.", "هذه الوردية مغلقة بالفعل.");
            return RedirectToAction(nameof(Details), new { id });
        }

        ViewBag.Shift = shift;
        ViewBag.ReceiptCount = await _db.Payments
            .CountAsync(p => p.CashierShiftId == id && p.Method == PaymentMethod.Cash);
        var model = new CloseCashierShiftViewModel
        {
            ShiftId = id,
            Counts = CashierShiftService.Denominations
                .Select(d => new DenominationCountInput { Denomination = d }).ToList()
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(CloseCashierShiftViewModel model)
    {
        var counts = new Dictionary<int, int>();
        foreach (var row in model.Counts ?? new List<DenominationCountInput>())
        {
            if (!CashierShiftService.Denominations.Contains(row.Denomination)) continue;
            counts.TryGetValue(row.Denomination, out var existing);
            counts[row.Denomination] = existing + Math.Max(0, row.Count);
        }

        try
        {
            var shift = await _shifts.CloseAsync(model.ShiftId, counts, model.Notes, User.CurrentUserId());
            await _audit.LogAsync("Close shift", "CashierShift", shift.Id.ToString(), null,
                new { shift.ShiftNo, shift.ExpectedCash, shift.CountedCash, shift.Variance, shift.Status });

            if (shift.Status == CashierShiftStatus.PendingApproval)
            {
                foreach (var role in new[] { Roles.Manager, Roles.Administrator })
                {
                    await _notifications.NotifyRoleAsync(role,
                        "فرق في إغلاق الصندوق", "Cashier variance awaiting approval",
                        $"الوردية {shift.ShiftNo} بها فرق {shift.Variance:N0} د.ع ويجب اعتمادها.",
                        $"Shift {shift.ShiftNo} closed with a variance of IQD {shift.Variance:N0} and needs approval.",
                        $"/Cashier/Details/{shift.Id}", NotificationType.Warning,
                        exceptUserId: User.CurrentUserId());
                }
                TempData["Warning"] = _l.T("Count submitted. A variance was found and needs manager approval.",
                    "تم إرسال العد. وُجد فرق يحتاج اعتماد المدير.");
            }
            else
            {
                TempData["Success"] = _l.T("Drawer balanced and closed.", "تم إغلاق الصندوق ومطابقته.");
            }
            return RedirectToAction(nameof(Details), new { id = shift.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Close), new { id = model.ShiftId });
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        var shift = await _db.CashierShifts
            .Include(s => s.Denominations)
            .Include(s => s.Payments).ThenInclude(p => p.Student)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
        if (shift is null) return NotFound();
        if (!CanView(shift)) return Forbid();
        ViewBag.CanApprove = shift.Status == CashierShiftStatus.PendingApproval
            && (User.IsInRole(Roles.Manager) || User.IsInRole(Roles.Administrator));
        return View(shift);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Approve(int id, string? note)
    {
        try
        {
            var shift = await _shifts.ApproveAsync(id, note, User.DisplayName());
            await _audit.LogAsync("Approve shift variance", "CashierShift", shift.Id.ToString(), null,
                new { shift.ShiftNo, shift.Variance, note });
            await _notifications.NotifyUserAsync(shift.CashierUserId,
                "تم اعتماد إغلاق الصندوق", "Drawer variance approved",
                $"تم اعتماد فرق الوردية {shift.ShiftNo}. يمكنك فتح وردية جديدة.",
                $"The variance on shift {shift.ShiftNo} was approved. You can open a new shift.",
                "/Cashier", NotificationType.Success);
            TempData["Success"] = _l.T("Variance approved and shift closed.", "تم اعتماد الفرق وإغلاق الوردية.");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    private bool CanView(CashierShift shift) =>
        shift.CashierUserId == User.CurrentUserId()
        || User.IsInRole(Roles.Administrator) || User.IsInRole(Roles.Manager) || User.IsInRole(Roles.Accountant);
}
