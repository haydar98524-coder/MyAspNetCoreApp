namespace KirkukInstitute.Models;

public class Account
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public AccountType Type { get; set; }
    public int? ParentId { get; set; }
    public Account? Parent { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystem { get; set; }
    public ICollection<Account> Children { get; set; } = new List<Account>();
    public ICollection<JournalLine> JournalLines { get; set; } = new List<JournalLine>();
}

public class JournalEntry
{
    public long Id { get; set; }
    public string EntryNo { get; set; } = "";
    public DateTime Date { get; set; }
    public string Description { get; set; } = "";
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public string CreatedByUserId { get; set; } = "";
    public string CreatedByName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();
}

public class JournalLine
{
    public long Id { get; set; }
    public long JournalEntryId { get; set; }
    public JournalEntry JournalEntry { get; set; } = null!;
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public string? Description { get; set; }
    public long Debit { get; set; }
    public long Credit { get; set; }
}

public class Fund
{
    public int Id { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public FundType Type { get; set; } = FundType.Cash;
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public long OpeningBalance { get; set; }
    public long CurrentBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Due
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
    public int? ContractId { get; set; }
    public TuitionContract? Contract { get; set; }
    public int? InstallmentNo { get; set; }
    public string Description { get; set; } = "";
    public long Amount { get; set; }
    public long PaidAmount { get; set; }
    public DateTime DueDate { get; set; }
    public DueStatus Status { get; set; } = DueStatus.Unpaid;
    public long Remaining => Math.Max(0, Amount - PaidAmount);
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class Payment
{
    public int Id { get; set; }
    public string ReceiptNo { get; set; } = "";
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int? EnrollmentId { get; set; }
    public Enrollment? Enrollment { get; set; }
    public int? DueId { get; set; }
    public Due? Due { get; set; }
    public int? CashierShiftId { get; set; }
    public CashierShift? CashierShift { get; set; }
    public long Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public DateTime Date { get; set; } = DateTime.Today;
    public string? Notes { get; set; }
    public string ReceivedByUserId { get; set; } = "";
    public string ReceivedByName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<TeacherEarning> TeacherEarnings { get; set; } = new List<TeacherEarning>();
}

public class Expense
{
    public int Id { get; set; }
    public string ExpenseNo { get; set; } = "";
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public int? FundId { get; set; }
    public Fund? Fund { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public long Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public string Vendor { get; set; } = "";
    public string Description { get; set; } = "";
    public string? AttachmentPath { get; set; }
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Approved;
    public int? CourseId { get; set; }
    public Course? Course { get; set; }
    public string CreatedByUserId { get; set; } = "";
    public string CreatedByName { get; set; } = "";
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Income
{
    public int Id { get; set; }
    public string IncomeNo { get; set; } = "";
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public int? FundId { get; set; }
    public Fund? Fund { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public long Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public string Source { get; set; } = "";
    public string Description { get; set; } = "";
    public string CreatedByUserId { get; set; } = "";
    public string CreatedByName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Asset
{
    public int Id { get; set; }
    public string AssetNo { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Category { get; set; } = "";
    public DateTime PurchaseDate { get; set; }
    public long Cost { get; set; }
    public long SalvageValue { get; set; }
    public int UsefulLifeYears { get; set; } = 5;
    public string Location { get; set; } = "";
    public string Responsible { get; set; } = "";
    public AssetStatus Status { get; set; } = AssetStatus.InUse;
    public string? Notes { get; set; }
    public long AnnualDepreciation => UsefulLifeYears > 0 ? Math.Max(0, Cost - SalvageValue) / UsefulLifeYears : 0;
}
