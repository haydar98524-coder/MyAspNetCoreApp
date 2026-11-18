using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

/// <summary>
/// Teacher compensation contracts, monthly statements, approvals and payout vouchers.
/// Statements are generated from unsettled earnings (and a fixed retainer when the contract
/// calls for one), adjusted while in draft, approved by leadership, then paid from a fund.
/// </summary>
[Authorize(Roles = Roles.Finance)]
public class TeacherSettlementsController : Controller
{
    private readonly AppDbContext _db;
    private readonly TeacherSettlementService _settlements;
    private readonly DocumentNumberService _numbers;
    private readonly AuditService _audit;
    private readonly Localizer _l;

    public TeacherSettlementsController(AppDbContext db, TeacherSettlementService settlements,
        DocumentNumberService numbers, AuditService audit, Localizer l)
    {
        _db = db;
        _settlements = settlements;
        _numbers = numbers;
        _audit = audit;
        _l = l;
    }

    public async Task<IActionResult> Index(string? q, TeacherSettlementStatus? status, DateTime? from, DateTime? to)
    {
        var query = _db.TeacherSettlements.Include(s => s.Teacher).AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(s => s.SettlementNo.Contains(term)
                || s.Teacher.FullNameAr.Contains(term) || s.Teacher.FullNameEn.Contains(term)
                || s.Teacher.Code.Contains(term));
        }
        if (status.HasValue) query = query.Where(s => s.Status == status.Value);
        if (from.HasValue) query = query.Where(s => s.PeriodEnd >= from.Value.Date);
        if (to.HasValue) query = query.Where(s => s.PeriodStart <= to.Value.Date);

        var rows = await query.OrderByDescending(s => s.PeriodEnd).ThenByDescending(s => s.Id)
            .Take(300).ToListAsync();

        ViewBag.Q = q;
        ViewBag.Status = status;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.DraftCount = await _db.TeacherSettlements.CountAsync(s => s.Status == TeacherSettlementStatus.Draft);
        ViewBag.ApprovedTotal = await _db.TeacherSettlements
            .Where(s => s.Status == TeacherSettlementStatus.Approved)
            .SumAsync(s => (long?)s.Net) ?? 0;
        ViewBag.PaidTotal = await _db.TeacherSettlements
            .Where(s => s.Status == TeacherSettlementStatus.Paid)
            .SumAsync(s => (long?)s.Net) ?? 0;
        ViewBag.ActiveContracts = await _db.TeacherContracts.CountAsync(c => c.IsActive);
        return View(rows);
    }

    public async Task<IActionResult> Contracts()
    {
        ViewBag.Contracts = await _db.TeacherContracts
            .Include(c => c.Teacher)
            .OrderByDescending(c => c.IsActive).ThenByDescending(c => c.StartDate)
            .Take(300).ToListAsync();
        ViewBag.Teachers = await _db.Teachers.Where(t => t.IsActive)
            .OrderBy(t => t.FullNameAr).ToListAsync();
        ViewBag.DefaultShare = (await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1))?.DefaultTeacherShare ?? 55m;
        return View(new TeacherContractFormViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateContract(TeacherContractFormViewModel model)
    {
        var teacher = await _db.Teachers.FindAsync(model.TeacherId);
        if (teacher is null) TempData["Error"] = _l.T("Select a valid teacher.", "اختر مدرّساً صحيحاً.");
        else if (model.CompensationType == TeacherCompensationType.PercentageCollected && (model.SharePercent < 0 || model.SharePercent > 100))
            TempData["Error"] = _l.T("Share percent must be between 0 and 100.", "نسبة الحصة يجب أن تكون بين 0 و100.");
        else if (model.CompensationType == TeacherCompensationType.FixedMonthly && model.FixedMonthlyAmount <= 0)
            TempData["Error"] = _l.T("Enter a positive fixed monthly amount.", "أدخل مخصصاً شهرياً أكبر من صفر.");
        else if (model.EndDate.HasValue && model.EndDate.Value.Date < model.StartDate.Date)
            TempData["Error"] = _l.T("End date cannot precede the start date.", "تاريخ الانتهاء لا يسبق تاريخ البدء.");
        else if (await _db.TeacherContracts.AnyAsync(c => c.TeacherId == model.TeacherId && c.IsActive))
            TempData["Error"] = _l.T("This teacher already has an active contract. Deactivate it first.",
                "لدى المدرّس عقد نشط بالفعل. أوقفه أولاً.");
        else
        {
            var contract = new TeacherContract
            {
                ContractNo = await _numbers.AllocateAsync(DocumentNumberService.TeacherContract),
                TeacherId = model.TeacherId,
                CompensationType = model.CompensationType,
                SharePercent = model.SharePercent,
                FixedMonthlyAmount = model.FixedMonthlyAmount,
                StartDate = model.StartDate.Date,
                EndDate = model.EndDate?.Date,
                IsActive = model.IsActive,
                Notes = model.Notes,
                CreatedByUserId = User.CurrentUserId(),
                CreatedByName = User.DisplayName()
            };
            _db.TeacherContracts.Add(contract);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Create", "TeacherContract", contract.Id.ToString(), null,
                new { contract.ContractNo, contract.TeacherId, contract.CompensationType, contract.SharePercent, contract.FixedMonthlyAmount });
            TempData["Success"] = _l.T($"Contract {contract.ContractNo} created.", $"تم إنشاء العقد {contract.ContractNo}.");
        }
        return RedirectToAction(nameof(Contracts));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleContract(int id)
    {
        var contract = await _db.TeacherContracts.FindAsync(id);
        if (contract is null) return NotFound();
        contract.IsActive = !contract.IsActive;
        if (contract.IsActive)
        {
            var others = await _db.TeacherContracts
                .Where(c => c.TeacherId == contract.TeacherId && c.Id != contract.Id && c.IsActive)
                .ToListAsync();
            foreach (var other in others) other.IsActive = false;
        }
        await _db.SaveChangesAsync();
        await _audit.LogAsync(contract.IsActive ? "Activate" : "Deactivate", "TeacherContract", id.ToString(), null,
            new { contract.ContractNo, contract.IsActive });
        TempData["Success"] = contract.IsActive
            ? _l.T("Contract activated.", "تم تنشيط العقد.")
            : _l.T("Contract deactivated.", "تم إيقاف العقد.");
        return RedirectToAction(nameof(Contracts));
    }

    public async Task<IActionResult> Generate(int? teacherId)
    {
        var model = new GenerateSettlementViewModel();
        if (teacherId.HasValue) model.TeacherId = teacherId.Value;
        await LoadGenerateListsAsync(model.TeacherId);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(GenerateSettlementViewModel model)
    {
        try
        {
            var settlement = await _settlements.GenerateAsync(model.TeacherId,
                model.PeriodStart, model.PeriodEnd, model.Notes,
                User.CurrentUserId(), User.DisplayName());
            await _audit.LogAsync("Generate", "TeacherSettlement", settlement.Id.ToString(), null,
                new { settlement.SettlementNo, settlement.TeacherId, settlement.Gross, settlement.PeriodStart, settlement.PeriodEnd });
            TempData["Success"] = _l.T($"Statement {settlement.SettlementNo} generated as a draft.",
                $"تم إنشاء الكشف {settlement.SettlementNo} كمسودة.");
            return RedirectToAction(nameof(Details), new { id = settlement.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Generate), new { teacherId = model.TeacherId });
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        var settlement = await _db.TeacherSettlements
            .Include(s => s.Teacher)
            .Include(s => s.Contract)
            .Include(s => s.Fund)
            .Include(s => s.Lines).ThenInclude(l => l.TeacherEarning).ThenInclude(e => e!.Course)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (settlement is null) return NotFound();

        ViewBag.Funds = await _db.Funds.Where(f => f.IsActive).OrderBy(f => f.Type).ToListAsync();
        ViewBag.CanApprove = User.IsInRole(Roles.Administrator) || User.IsInRole(Roles.Manager);
        ViewBag.Journal = await _db.JournalEntries.Include(j => j.Lines).ThenInclude(l => l.Account)
            .Where(j => j.ReferenceType == "TeacherSettlement" && j.ReferenceId == id.ToString())
            .OrderBy(j => j.Id).ToListAsync();
        return View(settlement);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateSettlementViewModel model)
    {
        try
        {
            var settlement = await _settlements.UpdateAsync(model.Id, model.Adjustments, model.Deductions, model.Notes);
            await _audit.LogAsync("Update", "TeacherSettlement", settlement.Id.ToString(), null,
                new { settlement.Adjustments, settlement.Deductions, settlement.Net });
            TempData["Success"] = _l.T("Statement updated.", "تم تحديث الكشف.");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Approve(int id)
    {
        try
        {
            var settlement = await _settlements.ApproveAsync(id, true, User.DisplayName(), User.CurrentUserId());
            await _audit.LogAsync("Approve", "TeacherSettlement", id.ToString(), null,
                new { settlement.SettlementNo, settlement.Net, settlement.Adjustments, settlement.Deductions });
            TempData["Success"] = _l.T("Statement approved. It can now be paid.",
                "تم اعتماد الكشف. يمكن صرفه الآن.");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(PaySettlementViewModel model)
    {
        try
        {
            var settlement = await _settlements.PayAsync(model.Id, model.FundId, model.Method,
                User.DisplayName(), User.CurrentUserId());
            await _audit.LogAsync("Pay", "TeacherSettlement", settlement.Id.ToString(), null,
                new { settlement.SettlementNo, settlement.Net, settlement.FundId, settlement.Method });
            TempData["Success"] = _l.T($"Statement {settlement.SettlementNo} paid.",
                $"تم صرف الكشف {settlement.SettlementNo}.");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    public async Task<IActionResult> Voucher(int id)
    {
        var settlement = await _db.TeacherSettlements
            .Include(s => s.Teacher)
            .Include(s => s.Contract)
            .Include(s => s.Fund)
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (settlement is null) return NotFound();
        if (settlement.Status == TeacherSettlementStatus.Draft)
        {
            TempData["Error"] = _l.T("Approve the statement before printing its voucher.",
                "اعتمد الكشف قبل طباعة سند الصرف.");
            return RedirectToAction(nameof(Details), new { id });
        }
        ViewBag.Settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1);
        return View(settlement);
    }

    private async Task LoadGenerateListsAsync(int? teacherId)
    {
        ViewBag.Teachers = await _db.Teachers.Where(t => t.IsActive).OrderBy(t => t.FullNameAr).ToListAsync();
        ViewBag.SelectedTeacher = teacherId.HasValue ? await _db.Teachers.FindAsync(teacherId.Value) : null;
        ViewBag.Contract = teacherId.HasValue
            ? await _settlements.GetActiveContractAsync(teacherId.Value, DateTime.Today)
            : null;

        if (!teacherId.HasValue)
        {
            ViewBag.UnsettledCount = 0;
            ViewBag.UnsettledAmount = 0L;
            return;
        }

        var used = _db.TeacherSettlementLines.Where(l => l.TeacherEarningId != null)
            .Select(l => l.TeacherEarningId!.Value);
        var open = _db.TeacherEarnings.Where(e => e.TeacherId == teacherId.Value && !e.IsPaid && !used.Contains(e.Id));
        ViewBag.UnsettledCount = await open.CountAsync();
        ViewBag.UnsettledAmount = await open.SumAsync(e => (long?)e.Amount) ?? 0;
    }
}
