using KirkukInstitute.Data;
using KirkukInstitute.Models;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Services;

/// <summary>
/// Centralises student billing so receipts, dues updates, fund balances,
/// teacher shares, ledger postings and guardian notifications stay consistent.
///
/// Phase 1 adds two controls: every cash receipt must belong to the cashier's open drawer,
/// and a cashier can never receive cash without an open shift. Receipt numbers come from the
/// atomic <see cref="DocumentNumberService"/> instead of a row count.
/// </summary>
public class BillingService
{
    private readonly AppDbContext _db;
    private readonly AccountingService _accounting;
    private readonly NotificationService _notifications;
    private readonly DocumentNumberService _numbers;
    private readonly Localizer _l;

    public BillingService(AppDbContext db, AccountingService accounting,
        NotificationService notifications, DocumentNumberService numbers, Localizer l)
    {
        _db = db;
        _accounting = accounting;
        _notifications = notifications;
        _numbers = numbers;
        _l = l;
    }

    public async Task<Due> CreateDueAsync(Enrollment enrollment, string? description, DateTime dueDate,
        int? contractId = null, int? installmentNo = null)
    {
        var due = new Due
        {
            StudentId = enrollment.StudentId,
            EnrollmentId = enrollment.Id,
            ContractId = contractId,
            InstallmentNo = installmentNo,
            Description = description ?? $"{enrollment.Course.NameEn} / {enrollment.Course.NameAr}",
            Amount = enrollment.NetFee,
            DueDate = dueDate.Date,
            Status = enrollment.NetFee == 0 ? DueStatus.Waived : DueStatus.Unpaid
        };
        _db.Dues.Add(due);
        await _db.SaveChangesAsync();
        return due;
    }

    public async Task<Payment> ReceiveAsync(Student student, long amount, PaymentMethod method,
        DateTime date, string? notes, string userId, string userName,
        Due? due = null, Enrollment? enrollment = null, Course? course = null,
        bool notifyGuardian = true, CashierShift? shift = null)
    {
        if (amount <= 0) throw new InvalidOperationException(_l.T("Amount must be greater than zero.", "المبلغ يجب أن يكون أكبر من صفر."));
        if (due is not null && amount > due.Remaining)
            throw new InvalidOperationException(_l.T("Amount exceeds the remaining balance on this due.", "المبلغ يتجاوز الرصيد المتبقي على القسط."));

        if (method == PaymentMethod.Cash)
        {
            if (shift is null)
                throw new InvalidOperationException(_l.T(
                    "Open a cashier shift before receiving cash.",
                    "افتح وردية الصندوق قبل استلام النقد."));
            if (shift.Status != CashierShiftStatus.Open)
                throw new InvalidOperationException(_l.T(
                    "This cashier shift is closed or awaiting approval.",
                    "وردية الصندوق مغلقة أو بانتظار الاعتماد."));
            if (!string.Equals(shift.CashierUserId, userId, StringComparison.Ordinal))
                throw new InvalidOperationException(_l.T(
                    "Cash must be received in your own open shift.",
                    "يجب استلام النقد في وردية الصندوق الخاصة بك."));
        }

        enrollment ??= due?.Enrollment;
        if (enrollment is not null)
        {
            await _db.Entry(enrollment).Reference(e => e.Course).Query()
                .Include(c => c.Stage).LoadAsync();
        }
        course ??= enrollment?.Course;
        var fund = await _accounting.GetFundAsync(method);
        var receiptNo = await _numbers.AllocateAsync(DocumentNumberService.Receipt, date.Year);

        var payment = new Payment
        {
            ReceiptNo = receiptNo,
            StudentId = student.Id,
            EnrollmentId = enrollment?.Id,
            DueId = due?.Id,
            CashierShiftId = shift?.Id,
            Amount = amount,
            Method = method,
            Date = date.Date,
            Notes = notes,
            ReceivedByUserId = userId,
            ReceivedByName = userName
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        if (due is not null)
        {
            due.PaidAmount += amount;
            due.Status = due.PaidAmount >= due.Amount ? DueStatus.Paid
                : due.PaidAmount > 0 ? DueStatus.Partial : DueStatus.Unpaid;
        }

        _accounting.ApplyInflow(fund, amount);

        // A fixed-monthly teacher is paid through a settlement retainer, never per receipt.
        var teacherContract = course is null ? null : await _db.TeacherContracts
            .Where(c => c.TeacherId == course.TeacherId && c.IsActive && c.StartDate <= date
                && (c.EndDate == null || c.EndDate >= date))
            .OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync();

        long share = 0;
        if (course is not null && teacherContract?.CompensationType != TeacherCompensationType.FixedMonthly)
        {
            var sharePercent = teacherContract?.CompensationType == TeacherCompensationType.PercentageCollected
                ? teacherContract.SharePercent
                : course.TeacherSharePercent;
            share = (long)Math.Round(amount * sharePercent / 100m, MidpointRounding.AwayFromZero);
            _db.TeacherEarnings.Add(new TeacherEarning
            {
                TeacherId = course.TeacherId,
                CourseId = course.Id,
                EnrollmentId = enrollment?.Id,
                PaymentId = payment.Id,
                TeacherContractId = teacherContract?.Id,
                Kind = TeacherEarningKind.CourseShare,
                BaseAmount = amount,
                SharePercent = sharePercent,
                Amount = share,
                Date = date.Date
            });
        }

        if (due?.ContractId is not null)
            await RefreshContractStatusAsync(due.ContractId.Value);

        await _db.SaveChangesAsync();

        var revenueCode = course is not null ? CoA.TuitionFor(course.Stage.Type) : CoA.OtherRevenue;
        var drafts = new List<JournalDraft>
        {
            new(method == PaymentMethod.Cash ? CoA.Cash : CoA.Bank, amount, 0),
            new(revenueCode, 0, amount)
        };
        if (share > 0)
        {
            drafts.Add(new JournalDraft(CoA.TeacherShare, share, 0));
            drafts.Add(new JournalDraft(CoA.TeacherPayable, 0, share));
        }
        await _accounting.PostAsync(date,
            _l.IsArabic
                ? $"قبض رسوم الطالب {student.FullNameAr}" + (course is null ? "" : $" - {course.NameAr}")
                : $"Tuition receipt {receiptNo} - {student.FullNameEn}",
            "Payment", payment.Id.ToString(), drafts, userName, userId);

        if (notifyGuardian)
        {
            var bodyAr = $"تم قبض {_l.Money(amount)}. رقم الإيصال {receiptNo}.";
            var bodyEn = $"We received IQD {amount:N0}. Receipt {receiptNo}.";
            await _notifications.NotifyGuardianAsync(student,
                "تم تسجيل دفعة جديدة", "New payment received",
                bodyAr, bodyEn, $"/Payments/Receipt/{payment.Id}", NotificationType.Success);
        }

        return payment;
    }

    private async Task RefreshContractStatusAsync(int contractId)
    {
        var contract = await _db.TuitionContracts
            .Include(c => c.Installments)
            .FirstOrDefaultAsync(c => c.Id == contractId);
        if (contract is null || contract.Status != TuitionContractStatus.Active) return;

        var stillOpen = contract.Installments
            .Any(i => i.Status != DueStatus.Paid && i.Status != DueStatus.Waived);
        if (!stillOpen) contract.Status = TuitionContractStatus.Completed;
    }
}
