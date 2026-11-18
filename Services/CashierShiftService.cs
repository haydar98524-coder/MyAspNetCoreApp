using KirkukInstitute.Data;
using KirkukInstitute.Models;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Services;

/// <summary>
/// Controls the cash drawer. A cashier works in exactly one open shift; cash receipts attach to
/// that shift automatically. At close, the cashier enters a blind denomination count. The
/// expected amount is computed server-side from the opening float plus cash receipts; a
/// non-zero variance moves the shift to PendingApproval and blocks a new shift until leadership
/// approves it. This is the core control against revenue leakage.
/// </summary>
public class CashierShiftService
{
    public static readonly int[] Denominations = [250, 500, 1000, 5000, 10000, 25000, 50000];

    private readonly AppDbContext _db;
    private readonly DocumentNumberService _numbers;
    private readonly Localizer _l;

    public CashierShiftService(AppDbContext db, DocumentNumberService numbers, Localizer l)
    {
        _db = db;
        _numbers = numbers;
        _l = l;
    }

    /// <summary>The shift that is Open or awaiting variance approval, if any.</summary>
    public Task<CashierShift?> GetActiveAsync(string userId) =>
        _db.CashierShifts
            .Where(s => s.CashierUserId == userId
                && (s.Status == CashierShiftStatus.Open || s.Status == CashierShiftStatus.PendingApproval))
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync();

    public async Task<CashierShift?> GetOpenAsync(string userId)
    {
        var shift = await GetActiveAsync(userId);
        return shift?.Status == CashierShiftStatus.Open ? shift : null;
    }

    public async Task<CashierShift> OpenAsync(long openingFloat, string userId, string userName)
    {
        if (openingFloat < 0)
            throw new InvalidOperationException(_l.T("Opening float cannot be negative.", "الرصيد الافتتاحي لا يمكن أن يكون سالباً."));

        var active = await GetActiveAsync(userId);
        if (active is not null)
        {
            throw new InvalidOperationException(active.Status == CashierShiftStatus.PendingApproval
                ? _l.T("Your previous shift is awaiting variance approval. A manager must approve it before you open a new drawer.",
                    "ورديتك السابقة بانتظار اعتماد الفرق. يجب أن يعتمدها المدير قبل فتح صندوق جديد.")
                : _l.T("You already have an open cashier shift.", "لديك وردية صندوق مفتوحة بالفعل."));
        }

        var shift = new CashierShift
        {
            ShiftNo = await _numbers.AllocateAsync(DocumentNumberService.CashierShift),
            CashierUserId = userId,
            CashierName = userName,
            OpenedAt = DateTime.Now,
            OpeningFloat = openingFloat,
            ExpectedCash = openingFloat,
            Status = CashierShiftStatus.Open
        };
        _db.CashierShifts.Add(shift);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(_l.T("A cashier shift is already open for this user.", "توجد وردية صندوق مفتوحة لهذا المستخدم."));
        }
        return shift;
    }

    public async Task<CashierShift> CloseAsync(int shiftId, IReadOnlyDictionary<int, int> counts,
        string? notes, string userId)
    {
        var shift = await _db.CashierShifts
            .Include(s => s.Denominations)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == shiftId)
            ?? throw new InvalidOperationException(_l.T("Cashier shift not found.", "وردية الصندوق غير موجودة."));

        if (!string.Equals(shift.CashierUserId, userId, StringComparison.Ordinal))
            throw new InvalidOperationException(_l.T("Only the cashier who opened the shift can close it.", "فقط أمين الصندوق الذي فتح الوردية يمكنه إغلاقها."));
        if (shift.Status != CashierShiftStatus.Open)
            throw new InvalidOperationException(_l.T("This shift is already closed.", "هذه الوردية مغلقة بالفعل."));

        shift.Denominations.Clear();
        foreach (var value in Denominations)
        {
            counts.TryGetValue(value, out var count);
            if (count < 0)
                throw new InvalidOperationException(_l.T("Denomination counts cannot be negative.", "عدد الفئات لا يمكن أن يكون سالباً."));
            if (count == 0) continue;
            shift.Denominations.Add(new CashierDenomination
            {
                CashierShiftId = shift.Id,
                Denomination = value,
                Count = count
            });
        }

        var counted = shift.Denominations.Sum(d => d.LineTotal);
        var cashTaken = shift.Payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        var expected = shift.OpeningFloat + cashTaken;

        shift.ExpectedCash = expected;
        shift.CountedCash = counted;
        shift.Variance = counted - expected;
        shift.ClosedAt = DateTime.Now;
        shift.Notes = notes;
        shift.Status = shift.Variance == 0 ? CashierShiftStatus.Closed : CashierShiftStatus.PendingApproval;

        await _db.SaveChangesAsync();
        return shift;
    }

    public async Task<CashierShift> ApproveAsync(int shiftId, string? note, string approverName)
    {
        var shift = await _db.CashierShifts.FindAsync(shiftId)
            ?? throw new InvalidOperationException(_l.T("Cashier shift not found.", "وردية الصندوق غير موجودة."));
        if (shift.Status != CashierShiftStatus.PendingApproval)
            throw new InvalidOperationException(_l.T("Only a shift with a variance can be approved.", "لا يمكن اعتماد إلا وردية بها فرق."));

        shift.Status = CashierShiftStatus.Closed;
        shift.ApprovedByName = approverName;
        shift.ApprovedAt = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(note))
            shift.Notes = string.IsNullOrWhiteSpace(shift.Notes) ? note : $"{shift.Notes} · {note}";

        await _db.SaveChangesAsync();
        return shift;
    }
}
