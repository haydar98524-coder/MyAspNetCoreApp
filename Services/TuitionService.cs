using KirkukInstitute.Data;
using KirkukInstitute.Models;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Services;

/// <summary>
/// Creates structured tuition agreements. The contract stores the agreed fee, discount and
/// installment plan; the installments are ordinary dues, so existing receipt, waiver, aging and
/// ledger behaviour applies unchanged. Rounding remainders go to the final installment so the
/// installments always sum exactly to the net fee.
/// </summary>
public class TuitionService
{
    public const int MaxInstallments = 36;

    private readonly AppDbContext _db;
    private readonly DocumentNumberService _numbers;
    private readonly Localizer _l;

    public TuitionService(AppDbContext db, DocumentNumberService numbers, Localizer l)
    {
        _db = db;
        _numbers = numbers;
        _l = l;
    }

    public async Task<TuitionContract> CreateAsync(Enrollment enrollment, long discount,
        int installmentCount, DateTime firstDueDate, string? notes, string userId, string userName)
    {
        if (discount < 0 || discount > enrollment.Fee)
            throw new InvalidOperationException(_l.T("Discount cannot exceed the course fee.", "الخصم لا يمكن أن يتجاوز رسوم الدورة."));
        installmentCount = Math.Clamp(installmentCount, 1, MaxInstallments);

        var net = enrollment.Fee - discount;
        var contractNo = await _numbers.AllocateAsync(DocumentNumberService.TuitionContract, DateTime.Today.Year);
        var first = firstDueDate.Date;

        var contract = new TuitionContract
        {
            ContractNo = contractNo,
            StudentId = enrollment.StudentId,
            EnrollmentId = enrollment.Id,
            CourseId = enrollment.CourseId,
            ListFee = enrollment.Fee,
            Discount = discount,
            NetFee = net,
            InstallmentCount = installmentCount,
            StartDate = enrollment.EnrollmentDate.Date,
            FirstDueDate = first,
            Status = TuitionContractStatus.Active,
            Notes = notes,
            CreatedByUserId = userId,
            CreatedByName = userName
        };

        var baseAmount = net / installmentCount;
        var remainder = net - baseAmount * installmentCount;
        for (var i = 1; i <= installmentCount; i++)
        {
            var amount = baseAmount + (i == installmentCount ? remainder : 0);
            contract.Installments.Add(new Due
            {
                StudentId = enrollment.StudentId,
                EnrollmentId = enrollment.Id,
                InstallmentNo = i,
                Description = _l.T(
                    $"{enrollment.Course.NameEn} — installment {i} of {installmentCount}",
                    $"{enrollment.Course.NameAr} — القسط {i} من {installmentCount}"),
                Amount = amount,
                DueDate = first.AddMonths(i - 1),
                Status = amount == 0 ? DueStatus.Waived : DueStatus.Unpaid
            });
        }

        _db.TuitionContracts.Add(contract);
        await _db.SaveChangesAsync();
        return contract;
    }

    public async Task CancelAsync(TuitionContract contract)
    {
        contract.Status = TuitionContractStatus.Cancelled;
        foreach (var due in contract.Installments
            .Where(d => d.Status == DueStatus.Unpaid || d.Status == DueStatus.Partial))
            due.Status = DueStatus.Waived;
        await _db.SaveChangesAsync();
    }

    public Task<TuitionContract?> FindByEnrollmentAsync(int enrollmentId) =>
        _db.TuitionContracts.Include(c => c.Installments)
            .FirstOrDefaultAsync(c => c.EnrollmentId == enrollmentId);

    /// <summary>Marks a contract completed when no installment remains open.</summary>
    public async Task RefreshStatusAsync(int contractId)
    {
        var contract = await _db.TuitionContracts.Include(c => c.Installments)
            .FirstOrDefaultAsync(c => c.Id == contractId);
        if (contract is null || contract.Status != TuitionContractStatus.Active) return;
        if (contract.Installments.All(i => i.Status is DueStatus.Paid or DueStatus.Waived))
            contract.Status = TuitionContractStatus.Completed;
        await _db.SaveChangesAsync();
    }
}
