using KirkukInstitute.Data;
using KirkukInstitute.Models;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Services;

public static class CoA
{
    public const string Cash = "1100";
    public const string Bank = "1110";
    public const string StudentReceivable = "1200";
    public const string FixedAssets = "1500";
    public const string TeacherPayable = "2100";
    public const string SalaryPayable = "2200";
    public const string Capital = "3100";
    public const string TuitionPrimary = "4110";
    public const string TuitionIntermediate = "4120";
    public const string TuitionPreparatory = "4130";
    public const string TuitionLanguage = "4140";
    public const string TuitionOther = "4150";
    public const string PrepTeacherRent = "4210";
    public const string PrivateCommission = "4220";
    public const string HomeLessons = "4230";
    public const string HallRent = "4300";
    public const string CafeteriaRent = "4400";
    public const string OtherRevenue = "4500";
    public const string TeacherShare = "5100";
    public const string Salaries = "5200";
    public const string OtherExpense = "6300";

    public static string TuitionFor(StageType type) => type switch
    {
        StageType.Primary => TuitionPrimary,
        StageType.Intermediate => TuitionIntermediate,
        StageType.Preparatory => TuitionPreparatory,
        _ => TuitionOther
    };
}

public record JournalDraft(string AccountCode, long Debit, long Credit, string? Description = null);

public class AccountingService
{
    private readonly AppDbContext _db;
    private readonly DocumentNumberService _numbers;

    public AccountingService(AppDbContext db, DocumentNumberService numbers)
    {
        _db = db;
        _numbers = numbers;
    }

    public async Task<JournalEntry> PostAsync(
        DateTime date, string description, string? referenceType, string? referenceId,
        IEnumerable<JournalDraft> drafts, string userName, string userId)
    {
        var lines = drafts.Where(x => x.Debit != 0 || x.Credit != 0).ToList();
        if (lines.Count == 0) throw new InvalidOperationException("Journal must contain at least one line.");
        var debit = lines.Sum(x => x.Debit);
        var credit = lines.Sum(x => x.Credit);
        if (debit != credit) throw new InvalidOperationException("Journal entry is not balanced.");

        var codes = lines.Select(x => x.AccountCode).Distinct().ToList();
        var accounts = await _db.Accounts.Where(a => codes.Contains(a.Code)).ToDictionaryAsync(a => a.Code);
        if (accounts.Count != codes.Count) throw new InvalidOperationException("One or more ledger accounts do not exist.");

        var entryNo = await _numbers.AllocateAsync(DocumentNumberService.Journal, date.Year);
        var entry = new JournalEntry
        {
            EntryNo = entryNo,
            Date = date.Date,
            Description = description,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            CreatedByUserId = userId,
            CreatedByName = userName,
            CreatedAt = DateTime.UtcNow
        };
        foreach (var line in lines)
        {
            entry.Lines.Add(new JournalLine
            {
                AccountId = accounts[line.AccountCode].Id,
                Debit = line.Debit,
                Credit = line.Credit,
                Description = line.Description
            });
        }
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task<Fund> GetFundAsync(PaymentMethod method, int? preferredFundId = null)
    {
        if (preferredFundId.HasValue)
        {
            var preferred = await _db.Funds.FindAsync(preferredFundId.Value);
            if (preferred is not null && preferred.IsActive) return preferred;
        }
        var type = method == PaymentMethod.Cash ? FundType.Cash : FundType.Bank;
        var fund = await _db.Funds.Where(f => f.IsActive && f.Type == type).OrderBy(f => f.Id).FirstOrDefaultAsync();
        if (fund is null) throw new InvalidOperationException("No active fund is configured.");
        return fund;
    }

    public void ApplyInflow(Fund fund, long amount) => fund.CurrentBalance += amount;
    public void ApplyOutflow(Fund fund, long amount) => fund.CurrentBalance -= amount;
}
