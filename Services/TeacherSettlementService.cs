using KirkukInstitute.Data;
using KirkukInstitute.Models;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Services;

/// <summary>
/// Builds and settles teacher payables. Percentage contracts settle the shares already accrued
/// on each receipt (Dr Teacher share / Cr Teacher payable); fixed contracts add a monthly
/// retainer that is accrued when the statement is approved. Nothing is paid without an approved
/// statement, and payment is posted against the selected fund.
/// </summary>
public class TeacherSettlementService
{
    private readonly AppDbContext _db;
    private readonly AccountingService _accounting;
    private readonly DocumentNumberService _numbers;
    private readonly NotificationService _notifications;
    private readonly Localizer _l;

    public TeacherSettlementService(AppDbContext db, AccountingService accounting,
        DocumentNumberService numbers, NotificationService notifications, Localizer l)
    {
        _db = db;
        _accounting = accounting;
        _numbers = numbers;
        _notifications = notifications;
        _l = l;
    }

    public async Task<TeacherContract?> GetActiveContractAsync(int teacherId, DateTime onDate) =>
        await _db.TeacherContracts
            .Where(c => c.TeacherId == teacherId && c.IsActive && c.StartDate <= onDate
                && (c.EndDate == null || c.EndDate >= onDate))
            .OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync();

    public async Task<TeacherSettlement> GenerateAsync(int teacherId, DateTime periodStart,
        DateTime periodEnd, string? notes, string userId, string userName)
    {
        var teacher = await _db.Teachers.FindAsync(teacherId)
            ?? throw new InvalidOperationException(_l.T("Teacher not found.", "المدرّس غير موجود."));

        if (periodEnd < periodStart) (periodStart, periodEnd) = (periodEnd, periodStart);
        periodStart = periodStart.Date;
        periodEnd = periodEnd.Date;
        if ((periodEnd - periodStart).TotalDays > 366)
            throw new InvalidOperationException(_l.T("A settlement period cannot exceed one year.", "لا يمكن أن تتجاوز فترة التسوية سنة واحدة."));

        var contract = await GetActiveContractAsync(teacherId, periodEnd);

        var settledEarningIds = _db.TeacherSettlementLines
            .Where(l => l.TeacherEarningId != null)
            .Select(l => l.TeacherEarningId!.Value);

        var earnings = await _db.TeacherEarnings
            .Include(e => e.Course)
            .Where(e => e.TeacherId == teacherId && !e.IsPaid
                && e.Date >= periodStart && e.Date <= periodEnd
                && !settledEarningIds.Contains(e.Id))
            .OrderBy(e => e.Date).ThenBy(e => e.Id)
            .ToListAsync();

        var settlement = new TeacherSettlement
        {
            SettlementNo = await _numbers.AllocateAsync(DocumentNumberService.TeacherSettlement),
            TeacherId = teacherId,
            ContractId = contract?.Id,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = TeacherSettlementStatus.Draft,
            Notes = notes,
            GeneratedByUserId = userId,
            GeneratedByName = userName,
            Method = PaymentMethod.Cash
        };

        foreach (var earning in earnings)
        {
            settlement.Lines.Add(new TeacherSettlementLine
            {
                TeacherEarningId = earning.Id,
                Kind = TeacherSettlementLineKind.Earning,
                Description = _l.T(
                    $"{earning.Course?.NameEn ?? "Course share"} · {earning.Date:yyyy/MM/dd} · receipt share",
                    $"{earning.Course?.NameAr ?? "حصة دورة"} · {earning.Date:yyyy/MM/dd} · حصة إيصال"),
                Amount = earning.Amount
            });
        }

        // A fixed retainer is included only for a full calendar month, once per month, so
        // splitting a month into several statements cannot duplicate the retainer.
        var fullMonth = periodStart.Day == 1
            && periodEnd == periodStart.AddMonths(1).AddDays(-1);
        if (contract is { CompensationType: TeacherCompensationType.FixedMonthly, FixedMonthlyAmount: > 0 }
            && fullMonth)
        {
            var alreadyRetained = await _db.TeacherSettlementLines.AnyAsync(l =>
                l.Kind == TeacherSettlementLineKind.FixedRetainer
                && l.Settlement.TeacherId == teacherId
                && l.Settlement.PeriodStart.Year == periodStart.Year
                && l.Settlement.PeriodStart.Month == periodStart.Month);
            if (!alreadyRetained)
            {
                settlement.Lines.Add(new TeacherSettlementLine
                {
                    Kind = TeacherSettlementLineKind.FixedRetainer,
                    Description = _l.T(
                        $"Fixed monthly retainer · {periodStart:yyyy/MM}",
                        $"مخصص شهري ثابت · {periodStart:yyyy/MM}"),
                    Amount = contract.FixedMonthlyAmount
                });
            }
        }

        if (settlement.Lines.Count == 0)
            throw new InvalidOperationException(_l.T(
                "No unsettled earnings were found for this teacher and period.",
                "لا توجد مستحقات غير مسواة لهذا المدرّس في هذه الفترة."));

        settlement.Gross = settlement.Lines.Sum(l => l.Amount);
        settlement.Net = settlement.Gross;
        _db.TeacherSettlements.Add(settlement);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(_l.T(
                "This period was already settled. Open the existing statement instead.",
                "تمت تسوية هذه الفترة مسبقاً. افتح الكشف الموجود."));
        }
        return settlement;
    }

    public async Task<TeacherSettlement> UpdateAsync(int id, long adjustments, long deductions, string? notes)
    {
        var settlement = await _db.TeacherSettlements.Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new InvalidOperationException(_l.T("Statement not found.", "الكشف غير موجود."));
        if (settlement.Status != TeacherSettlementStatus.Draft)
            throw new InvalidOperationException(_l.T("Only draft statements can be edited.", "لا يمكن تعديل إلا الكشوف المسودة."));
        if (adjustments < 0 || deductions < 0)
            throw new InvalidOperationException(_l.T("Adjustments and deductions cannot be negative.", "التعديلات والاستقطاعات لا يمكن أن تكون سالبة."));
        if (settlement.Gross + adjustments - deductions < 0)
            throw new InvalidOperationException(_l.T("Deductions exceed the statement total.", "الاستقطاعات تتجاوز إجمالي الكشف."));

        settlement.Adjustments = adjustments;
        settlement.Deductions = deductions;
        settlement.Net = settlement.Gross + adjustments - deductions;
        settlement.Notes = notes;
        await _db.SaveChangesAsync();
        return settlement;
    }

    public async Task<TeacherSettlement> ApproveAsync(int id, bool confirm, string? approverName, string? userId)
    {
        var settlement = await _db.TeacherSettlements.Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new InvalidOperationException(_l.T("Statement not found.", "الكشف غير موجود."));
        if (!confirm)
            throw new InvalidOperationException(_l.T("Approval must be confirmed.", "يجب تأكيد الاعتماد."));
        if (settlement.Status != TeacherSettlementStatus.Draft)
            throw new InvalidOperationException(_l.T("Only draft statements can be approved.", "لا يمكن اعتماد إلا الكشوف المسودة."));

        var retainer = settlement.Lines.Where(l => l.Kind == TeacherSettlementLineKind.FixedRetainer).Sum(l => l.Amount);
        var increases = retainer + settlement.Adjustments;
        var decreases = settlement.Deductions;

        if (increases > 0 || decreases > 0)
        {
            var drafts = new List<JournalDraft>();
            if (increases > 0)
            {
                drafts.Add(new JournalDraft(CoA.TeacherShare, increases, 0));
                drafts.Add(new JournalDraft(CoA.TeacherPayable, 0, increases));
            }
            if (decreases > 0)
            {
                drafts.Add(new JournalDraft(CoA.TeacherPayable, decreases, 0));
                drafts.Add(new JournalDraft(CoA.TeacherShare, 0, decreases));
            }
            await _accounting.PostAsync(DateTime.Today,
                _l.IsArabic
                    ? $"اعتماد كشف مستحقات المدرّس {settlement.SettlementNo}"
                    : $"Teacher settlement approved {settlement.SettlementNo}",
                "TeacherSettlement", settlement.Id.ToString(), drafts,
                approverName ?? "", userId ?? "");
        }

        settlement.Status = TeacherSettlementStatus.Approved;
        settlement.ApprovedByName = approverName;
        settlement.ApprovedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return settlement;
    }

    public async Task<TeacherSettlement> PayAsync(int id, int? fundId, PaymentMethod method,
        string userName, string userId)
    {
        var settlement = await _db.TeacherSettlements
            .Include(s => s.Teacher)
            .Include(s => s.Lines).ThenInclude(l => l.TeacherEarning)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new InvalidOperationException(_l.T("Statement not found.", "الكشف غير موجود."));
        if (settlement.Status != TeacherSettlementStatus.Approved)
            throw new InvalidOperationException(_l.T("Only an approved statement can be paid.", "لا يمكن صرف إلا كشف معتمد."));

        if (settlement.Net > 0)
        {
            var fund = await _accounting.GetFundAsync(method, fundId);
            if (fund.CurrentBalance < settlement.Net)
                throw new InvalidOperationException(_l.T("The selected fund balance is not sufficient.", "رصيد الصندوق المحدد غير كافٍ."));

            _accounting.ApplyOutflow(fund, settlement.Net);
            await _accounting.PostAsync(DateTime.Today,
                _l.IsArabic
                    ? $"صرف مستحقات المدرّس {settlement.Teacher.FullNameAr} — {settlement.SettlementNo}"
                    : $"Teacher payout {settlement.SettlementNo} — {settlement.Teacher.FullNameEn}",
                "TeacherSettlement", settlement.Id.ToString(),
                new[]
                {
                    new JournalDraft(CoA.TeacherPayable, settlement.Net, 0),
                    new JournalDraft(fund.Type == FundType.Cash ? CoA.Cash : CoA.Bank, 0, settlement.Net)
                },
                userName, userId);

            settlement.FundId = fund.Id;
            settlement.Method = method;
        }

        var paidAt = DateTime.UtcNow;
        foreach (var line in settlement.Lines.Where(l => l.TeacherEarning is not null))
        {
            line.TeacherEarning!.IsPaid = true;
            line.TeacherEarning.PaidAt = paidAt;
            line.TeacherEarning.PaidReference = settlement.SettlementNo;
        }

        settlement.Status = TeacherSettlementStatus.Paid;
        settlement.PaidAt = DateTime.Now;
        settlement.PaidByName = userName;
        await _db.SaveChangesAsync();

        await _notifications.NotifyUserAsync(settlement.Teacher.UserId,
            "تم صرف مستحقاتك", "Your settlement was paid",
            $"تم صرف كشف المستحقات {settlement.SettlementNo} بمبلغ {settlement.Net:N0} د.ع.",
            $"Settlement {settlement.SettlementNo} was paid: IQD {settlement.Net:N0}.",
            "/Teacher/Earnings", NotificationType.Success);

        return settlement;
    }
}
