namespace KirkukInstitute.Models;

/// <summary>
/// One row per document family and calendar year. DocumentNumberService advances LastNumber
/// under a serializable range lock so receipts, journals, contracts, shifts and settlement
/// statements can never share a number, even under concurrent requests.
/// </summary>
public class DocumentNumberCounter
{
    public string Prefix { get; set; } = "";
    public int Year { get; set; }
    public int LastNumber { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A signed tuition agreement. The installments themselves remain ordinary <see cref="Due"/>
/// rows, so collection, waivers, receipts and the ledger keep working unchanged while the
/// contract records the terms that produced them.
/// </summary>
public class TuitionContract
{
    public int Id { get; set; }
    public string ContractNo { get; set; } = "";
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public long ListFee { get; set; }
    public long Discount { get; set; }
    public long NetFee { get; set; }
    public int InstallmentCount { get; set; } = 1;
    public DateTime StartDate { get; set; }
    public DateTime FirstDueDate { get; set; }
    public TuitionContractStatus Status { get; set; } = TuitionContractStatus.Active;
    public string? Notes { get; set; }
    public string CreatedByUserId { get; set; } = "";
    public string CreatedByName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Due> Installments { get; set; } = new List<Due>();

    public long Paid => Installments.Sum(i => i.PaidAmount);
    public long Outstanding => Installments
        .Where(i => i.Status != DueStatus.Paid && i.Status != DueStatus.Waived)
        .Sum(i => i.Remaining);
}

/// <summary>
/// A cashier's working session. Cash receipts are attached to a shift; the drawer is closed
/// with a blind denomination count and any variance must be approved by leadership.
/// </summary>
public class CashierShift
{
    public int Id { get; set; }
    public string ShiftNo { get; set; } = "";
    public string CashierUserId { get; set; } = "";
    public string CashierName { get; set; } = "";
    public DateTime OpenedAt { get; set; } = DateTime.Now;
    public long OpeningFloat { get; set; }
    public DateTime? ClosedAt { get; set; }
    public long ExpectedCash { get; set; }
    public long CountedCash { get; set; }
    public long Variance { get; set; }
    public CashierShiftStatus Status { get; set; } = CashierShiftStatus.Open;
    public string? Notes { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public ICollection<CashierDenomination> Denominations { get; set; } = new List<CashierDenomination>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public long CashTaken => Payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
    public long CountedTotal => Denominations.Sum(d => d.LineTotal);
}

public class CashierDenomination
{
    public int Id { get; set; }
    public int CashierShiftId { get; set; }
    public CashierShift Shift { get; set; } = null!;
    public int Denomination { get; set; }
    public int Count { get; set; }
    public long LineTotal => (long)Denomination * Count;
}

/// <summary>
/// The compensation terms that govern a teacher's settlements. Percentage contracts settle the
/// shares accrued on each receipt; fixed contracts add one monthly retainer line.
/// </summary>
public class TeacherContract
{
    public int Id { get; set; }
    public string ContractNo { get; set; } = "";
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public TeacherCompensationType CompensationType { get; set; } = TeacherCompensationType.PercentageCollected;
    public decimal SharePercent { get; set; } = 55m;
    public long FixedMonthlyAmount { get; set; }
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public string CreatedByUserId { get; set; } = "";
    public string CreatedByName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<TeacherSettlement> Settlements { get; set; } = new List<TeacherSettlement>();
}

/// <summary>
/// A monthly (or arbitrary-period) statement of a teacher's earnings. Draft statements can be
/// adjusted, leadership approves them, and only then can they be paid from a fund.
/// </summary>
public class TeacherSettlement
{
    public int Id { get; set; }
    public string SettlementNo { get; set; } = "";
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public int? ContractId { get; set; }
    public TeacherContract? Contract { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public long Gross { get; set; }
    public long Adjustments { get; set; }
    public long Deductions { get; set; }
    public long Net { get; set; }
    public TeacherSettlementStatus Status { get; set; } = TeacherSettlementStatus.Draft;
    public string? Notes { get; set; }
    public string GeneratedByUserId { get; set; } = "";
    public string GeneratedByName { get; set; } = "";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? PaidByName { get; set; }
    public DateTime? PaidAt { get; set; }
    public int? FundId { get; set; }
    public Fund? Fund { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public ICollection<TeacherSettlementLine> Lines { get; set; } = new List<TeacherSettlementLine>();
}

public class TeacherSettlementLine
{
    public int Id { get; set; }
    public int TeacherSettlementId { get; set; }
    public TeacherSettlement Settlement { get; set; } = null!;
    public int? TeacherEarningId { get; set; }
    public TeacherEarning? TeacherEarning { get; set; }
    public TeacherSettlementLineKind Kind { get; set; } = TeacherSettlementLineKind.Earning;
    public string Description { get; set; } = "";
    public long Amount { get; set; }
}
