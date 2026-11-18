using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

/// <summary>
/// Read-focused register of signed tuition agreements. Contracts are created with an enrollment
/// (where the installment plan is agreed) and cancelled through the enrollment, so their
/// financial history is never edited here.
/// </summary>
[Authorize(Roles = Roles.Admissions)]
public class TuitionContractsController : Controller
{
    private readonly AppDbContext _db;
    private readonly Localizer _l;

    public TuitionContractsController(AppDbContext db, Localizer l)
    {
        _db = db;
        _l = l;
    }

    public async Task<IActionResult> Index(string? q, TuitionContractStatus? status, DateTime? from, DateTime? to)
    {
        var query = _db.TuitionContracts
            .Include(c => c.Student).ThenInclude(s => s.Grade)
            .Include(c => c.Course)
            .Include(c => c.Installments)
            .AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.ContractNo.Contains(term)
                || c.Student.FullNameAr.Contains(term) || c.Student.FullNameEn.Contains(term)
                || c.Student.StudentNo.Contains(term) || c.Course.NameAr.Contains(term));
        }
        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        if (from.HasValue) query = query.Where(c => c.StartDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(c => c.StartDate <= to.Value.Date);

        var rows = await query.OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id)
            .Take(300).ToListAsync();

        ViewBag.Q = q;
        ViewBag.Status = status;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Active = await _db.TuitionContracts.CountAsync(c => c.Status == TuitionContractStatus.Active);
        ViewBag.NetTotal = await _db.TuitionContracts
            .Where(c => c.Status != TuitionContractStatus.Cancelled)
            .SumAsync(c => (long?)c.NetFee) ?? 0;
        ViewBag.Outstanding = await _db.Dues
            .Where(d => d.ContractId != null && d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
            .SumAsync(d => (long?)(d.Amount - d.PaidAmount)) ?? 0;
        ViewBag.InstallmentCount = await _db.Dues.CountAsync(d => d.ContractId != null);

        return View(rows);
    }

    public async Task<IActionResult> Details(int id)
    {
        var contract = await _db.TuitionContracts
            .Include(c => c.Student).ThenInclude(s => s.Grade).ThenInclude(g => g.Stage)
            .Include(c => c.Student).ThenInclude(s => s.Parent)
            .Include(c => c.Course).ThenInclude(c => c.Teacher)
            .Include(c => c.Course).ThenInclude(c => c.Subject)
            .Include(c => c.Enrollment).ThenInclude(e => e.Section)
            .Include(c => c.Installments).ThenInclude(i => i.Payments)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (contract is null) return NotFound();

        ViewBag.Payments = await _db.Payments
            .Include(p => p.CashierShift)
            .Where(p => p.EnrollmentId == contract.EnrollmentId)
            .OrderByDescending(p => p.Date).ThenByDescending(p => p.Id)
            .ToListAsync();

        return View(contract);
    }
}
