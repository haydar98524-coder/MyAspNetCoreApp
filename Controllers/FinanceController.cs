using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Finance)]
public class FinanceController : Controller
{
    private readonly AppDbContext _db;
    private readonly AccountingService _accounting;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;
    private readonly FileStorageService _files;
    private readonly TeacherSettlementService _settlements;
    private readonly Localizer _l;

    public FinanceController(AppDbContext db, AccountingService accounting, AuditService audit,
        NotificationService notifications, FileStorageService files,
        TeacherSettlementService settlements, Localizer localizer)
    {
        _db = db;
        _accounting = accounting;
        _audit = audit;
        _notifications = notifications;
        _files = files;
        _settlements = settlements;
        _l = localizer;
    }

    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var start = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var end = (to ?? today).Date;
        if (end < start) (start, end) = (end, start);
        var endExclusive = end.AddDays(1);

        var trendMonths = Enumerable.Range(0, 6)
            .Select(i => new DateTime(today.Year, today.Month, 1).AddMonths(-5 + i)).ToList();
        var fetchStart = trendMonths[0];

        var payments = await _db.Payments.Where(x => x.Date >= fetchStart && x.Date < endExclusive)
            .Select(x => new DataRow(x.Date, x.Amount, null, null)).ToListAsync();
        var incomes = await _db.Incomes.Where(x => x.Date >= fetchStart && x.Date < endExclusive)
            .Select(x => new DataRow(x.Date, x.Amount, null, null)).ToListAsync();
        var halls = await _db.ClassroomBookings.Where(x => x.PaymentStatus == BookingPaymentStatus.Paid && x.Date >= fetchStart && x.Date < endExclusive)
            .Select(x => new DataRow(x.Date, x.TotalAmount, null, null)).ToListAsync();
        var cafeterias = await _db.CafeteriaPayments.Where(x => x.IsPaid && x.PaidDate >= fetchStart && x.PaidDate < endExclusive)
            .Select(x => new DataRow(x.PaidDate!.Value, x.Amount, null, null)).ToListAsync();
        var teacherRents = await _db.TeacherRentPayments.Where(x => x.IsPaid && x.PaidDate >= fetchStart && x.PaidDate < endExclusive)
            .Select(x => new DataRow(x.PaidDate!.Value, x.Amount, null, null)).ToListAsync();
        var privates = await _db.PrivateLessons.Where(x => x.IsPaid && x.PaidDate >= fetchStart && x.PaidDate < endExclusive)
            .Select(x => new DataRow(x.PaidDate!.Value, x.Fee, null, null)).ToListAsync();
        var expenses = await _db.Expenses.Include(x => x.Account)
            .Where(x => x.Status == ExpenseStatus.Approved && x.Date >= fetchStart && x.Date < endExclusive)
            .Select(x => new DataRow(x.Date, x.Amount, x.Account.NameAr, x.Account.NameEn)).ToListAsync();
        var earnings = await _db.TeacherEarnings.Where(x => x.Date >= fetchStart && x.Date < endExclusive)
            .Select(x => new DataRow(x.Date, x.Amount, null, null)).ToListAsync();
        var payrolls = await _db.Payrolls.Where(x => x.Status == PayrollStatus.Paid && x.PaidAt >= fetchStart && x.PaidAt < endExclusive)
            .Select(x => new DataRow(x.PaidAt!.Value, x.Net, null, null)).ToListAsync();

        long In(IEnumerable<DataRow> rows, DateTime a, DateTime b) => rows.Where(x => x.Date >= a && x.Date < b).Sum(x => x.Amount);

        var funds = await _db.Funds.Include(f => f.Account).OrderBy(f => f.Type).ToListAsync();
        var dues = await _db.Dues.Where(d => d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
            .Select(d => new { d.Amount, d.PaidAmount }).ToListAsync();

        var expenseGroups = expenses.Where(x => x.Date >= start && x.Date < endExclusive)
            .GroupBy(x => new { x.NameAr, x.NameEn })
            .Select(g => new NamedAmount(_l.Pick(g.Key.NameAr, g.Key.NameEn), g.Sum(x => x.Amount), "#f97316"))
            .OrderByDescending(x => x.Amount).Take(6).ToList();
        var palette = new[] { "#0f766e", "#c9a227", "#3b82f6", "#f97316", "#8b5cf6", "#64748b" };
        for (var i = 0; i < expenseGroups.Count; i++)
            expenseGroups[i] = expenseGroups[i] with { Color = palette[i % palette.Length] };

        var vm = new FinanceOverviewViewModel
        {
            From = start,
            To = end,
            TuitionRevenue = In(payments, start, endExclusive),
            OtherRevenue = In(incomes, start, endExclusive),
            HallRevenue = In(halls, start, endExclusive),
            CafeteriaRevenue = In(cafeterias, start, endExclusive),
            TeacherRentRevenue = In(teacherRents, start, endExclusive),
            PrivateRevenue = In(privates, start, endExclusive),
            TeacherCost = In(earnings, start, endExclusive),
            PayrollCost = In(payrolls, start, endExclusive),
            DirectExpenses = In(expenses, start, endExclusive),
            DuesOutstanding = dues.Sum(d => Math.Max(0, d.Amount - d.PaidAmount)),
            CashBalance = funds.Where(f => f.Type == FundType.Cash).Sum(f => f.CurrentBalance),
            BankBalance = funds.Where(f => f.Type == FundType.Bank).Sum(f => f.CurrentBalance),
            Funds = funds,
            ExpenseBreakdown = expenseGroups,
            RecentEntries = await _db.JournalEntries.Include(j => j.Lines).ThenInclude(l => l.Account)
                .OrderByDescending(j => j.Date).ThenByDescending(j => j.Id).Take(6).ToListAsync()
        };

        vm.RevenueSources = new List<NamedAmount>
        {
            new(_l.T("Tuition fees", "الرسوم الدراسية"), vm.TuitionRevenue, "#0f766e"),
            new(_l.T("Private lessons", "الدروس الخصوصية"), vm.PrivateRevenue, "#c9a227"),
            new(_l.T("Hall rentals", "إيجار القاعات"), vm.HallRevenue, "#3b82f6"),
            new(_l.T("Cafeteria rent", "إيجار الكافيتيريا"), vm.CafeteriaRevenue, "#f97316"),
            new(_l.T("Teacher room rent", "إيجار مدرسي السادس"), vm.TeacherRentRevenue, "#8b5cf6"),
            new(_l.T("Other income", "إيرادات أخرى"), vm.OtherRevenue, "#64748b")
        }.Where(x => x.Amount > 0).ToList();

        for (var i = 0; i < trendMonths.Count; i++)
        {
            var month = trendMonths[i];
            var next = month.AddMonths(1);
            vm.Trend.Add(new ChartPoint(
                _l.MonthShort(month.Month),
                In(payments, month, next) + In(incomes, month, next) + In(halls, month, next) +
                In(cafeterias, month, next) + In(teacherRents, month, next) + In(privates, month, next),
                In(expenses, month, next) + In(earnings, month, next) + In(payrolls, month, next)));
        }
        vm.Accounts = await GetAccountBalancesAsync(endExclusive);
        return View(vm);
    }

    public async Task<IActionResult> Journal(DateTime? from, DateTime? to, string? q, int? accountId, int page = 1)
    {
        var today = DateTime.Today;
        var start = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var end = (to ?? today).Date;
        if (end < start) (start, end) = (end, start);
        var endExclusive = end.AddDays(1);
        page = Math.Max(1, page);

        var query = _db.JournalEntries
            .Include(j => j.Lines).ThenInclude(l => l.Account)
            .AsNoTracking()
            .Where(j => j.Date >= start && j.Date < endExclusive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(j => j.EntryNo.Contains(term) || j.Description.Contains(term));
        }
        if (accountId.HasValue) query = query.Where(j => j.Lines.Any(l => l.AccountId == accountId.Value));

        var total = await query.CountAsync();
        const int pageSize = 25;
        var entries = await query.OrderByDescending(j => j.Date).ThenByDescending(j => j.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.From = start;
        ViewBag.To = end;
        ViewBag.Q = q;
        ViewBag.AccountId = accountId;
        ViewBag.Page = page;
        ViewBag.PageCount = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.Total = total;
        ViewBag.Debit = entries.Sum(e => e.Lines.Sum(l => l.Debit));
        ViewBag.Credit = entries.Sum(e => e.Lines.Sum(l => l.Credit));
        ViewBag.Accounts = await _db.Accounts.OrderBy(a => a.Code).AsNoTracking().ToListAsync();
        return View(entries);
    }

    [HttpGet]
    public async Task<IActionResult> NewEntry()
    {
        var vm = new ManualJournalViewModel
        {
            Lines = Enumerable.Range(0, 3).Select(_ => new JournalLineInput()).ToList()
        };
        await LoadAccountsAsync();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NewEntry(ManualJournalViewModel vm)
    {
        vm.Lines = vm.Lines.Where(l => l.AccountId > 0 && (l.Debit != 0 || l.Credit != 0)).ToList();
        if (vm.Lines.Count < 2) ModelState.AddModelError("", "يجب إدخال سطرين محاسبيين على الأقل.");
        if (vm.Lines.Any(l => l.Debit < 0 || l.Credit < 0)) ModelState.AddModelError("", "لا يمكن إدخال قيم سالبة.");
        if (vm.Lines.Any(l => l.Debit > 0 && l.Credit > 0)) ModelState.AddModelError("", "لا يمكن أن يحتوي السطر على مدين ودائن معاً.");
        if (vm.Lines.Sum(l => l.Debit) != vm.Lines.Sum(l => l.Credit))
            ModelState.AddModelError("", "القيد غير متوازن: مجموع المدين يجب أن يساوي مجموع الدائن.");
        if (!ModelState.IsValid)
        {
            await LoadAccountsAsync();
            return View(vm);
        }

        try
        {
            var ids = vm.Lines.Select(l => l.AccountId).Distinct().ToList();
            var codes = await _db.Accounts.Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Code);
            if (codes.Count != ids.Count) throw new InvalidOperationException("أحد الحسابات المحددة غير موجود.");
            var entry = await _accounting.PostAsync(vm.Date, vm.Description.Trim(), "Manual", null,
                vm.Lines.Select(l => new JournalDraft(codes[l.AccountId], l.Debit, l.Credit, l.Description)),
                User.DisplayName(), User.CurrentUserId());
            await _audit.LogAsync("Manual journal posted", nameof(JournalEntry), entry.Id.ToString(), null,
                new { entry.EntryNo, entry.Description, Amount = vm.Lines.Sum(l => l.Debit) });
            TempData["Success"] = $"تم ترحيل القيد {entry.EntryNo} بنجاح.";
            return RedirectToAction(nameof(Journal));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            await LoadAccountsAsync();
            return View(vm);
        }
    }

    public async Task<IActionResult> Expenses(ExpenseStatus? status, int? accountId, DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var start = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var end = (to ?? today).Date;
        if (end < start) (start, end) = (end, start);

        var query = _db.Expenses.Include(e => e.Account).Include(e => e.Fund).Include(e => e.Course)
            .AsNoTracking().Where(e => e.Date >= start && e.Date <= end);
        if (status.HasValue) query = query.Where(e => e.Status == status.Value);
        if (accountId.HasValue) query = query.Where(e => e.AccountId == accountId.Value);
        var list = await query.OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).Take(300).ToListAsync();

        ViewBag.Status = status;
        ViewBag.AccountId = accountId;
        ViewBag.From = start;
        ViewBag.To = end;
        ViewBag.Count = list.Count;
        ViewBag.ApprovedTotal = list.Where(e => e.Status == ExpenseStatus.Approved).Sum(e => e.Amount);
        ViewBag.DraftTotal = list.Where(e => e.Status == ExpenseStatus.Draft).Sum(e => e.Amount);
        ViewBag.DraftCount = await _db.Expenses.CountAsync(e => e.Status == ExpenseStatus.Draft);
        ViewBag.Accounts = await _db.Accounts.Where(a => a.Type == AccountType.Expense).OrderBy(a => a.Code).AsNoTracking().ToListAsync();
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> CreateExpense()
    {
        await LoadExpenseListsAsync();
        return View(new ExpenseFormViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExpense(ExpenseFormViewModel vm)
    {
        var account = await _db.Accounts.FindAsync(vm.AccountId);
        if (account is null || account.Type != AccountType.Expense)
            ModelState.AddModelError(nameof(vm.AccountId), "يرجى اختيار حساب مصروف صحيح.");
        if (!ModelState.IsValid)
        {
            await LoadExpenseListsAsync();
            return View(vm);
        }

        string? attachmentPath = null;
        try
        {
            attachmentPath = await _files.SaveAsync(vm.Attachment, "expenses");
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(vm.Attachment), ex.Message);
            await LoadExpenseListsAsync();
            return View(vm);
        }

        Fund fund;
        try
        {
            fund = await _accounting.GetFundAsync(vm.Method, vm.FundId);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            await LoadExpenseListsAsync();
            return View(vm);
        }

        if (vm.PostNow && fund.CurrentBalance < vm.Amount)
        {
            ModelState.AddModelError("", "رصيد الصندوق المحدد غير كافٍ لترحيل هذا المصروف.");
            await LoadExpenseListsAsync();
            return View(vm);
        }

        var year = vm.Date.Year;
        var seq = await _db.Expenses.CountAsync(e => e.Date.Year == year) + 1;
        string no;
        do { no = $"EXP-{year}-{seq:D4}"; seq++; } while (await _db.Expenses.AnyAsync(e => e.ExpenseNo == no));

        var expense = new Expense
        {
            ExpenseNo = no,
            AccountId = vm.AccountId,
            FundId = fund.Id,
            Date = vm.Date.Date,
            Amount = vm.Amount,
            Method = vm.Method,
            Vendor = vm.Vendor.Trim(),
            Description = vm.Description.Trim(),
            AttachmentPath = attachmentPath,
            Status = vm.PostNow ? ExpenseStatus.Approved : ExpenseStatus.Draft,
            CourseId = vm.CourseId,
            CreatedByUserId = User.CurrentUserId(),
            CreatedByName = User.DisplayName(),
            ApprovedAt = vm.PostNow ? DateTime.UtcNow : null
        };
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        if (vm.PostNow)
        {
            _accounting.ApplyOutflow(fund, expense.Amount);
            await _accounting.PostAsync(expense.Date, $"{expense.Description} - {expense.Vendor}", "Expense", expense.Id.ToString(),
                new[]
                {
                    new JournalDraft(account!.Code, expense.Amount, 0),
                    new JournalDraft(fund.Type == FundType.Cash ? CoA.Cash : CoA.Bank, 0, expense.Amount)
                }, User.DisplayName(), User.CurrentUserId());
        }

        await _audit.LogAsync("Expense created", nameof(Expense), expense.Id.ToString(), null,
            new { expense.ExpenseNo, expense.Amount, expense.Status });
        TempData["Success"] = vm.PostNow
            ? $"تم تسجيل المصروف {no} وترحيله على الصندوق."
            : $"تم حفظ المصروف {no} كمسودة.";
        return RedirectToAction(nameof(Expenses));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveExpense(int id)
    {
        var expense = await _db.Expenses.Include(e => e.Account).Include(e => e.Fund).FirstOrDefaultAsync(e => e.Id == id);
        if (expense is null) return NotFound();
        if (expense.Status != ExpenseStatus.Draft)
        {
            TempData["Warning"] = "تم اعتماد هذا المصروف مسبقاً.";
            return RedirectToAction(nameof(Expenses));
        }
        var fund = expense.Fund ?? await _accounting.GetFundAsync(expense.Method);
        if (fund.CurrentBalance < expense.Amount)
        {
            TempData["Error"] = "رصيد الصندوق غير كافٍ لاعتماد المصروف.";
            return RedirectToAction(nameof(Expenses));
        }

        expense.Status = ExpenseStatus.Approved;
        expense.ApprovedAt = DateTime.UtcNow;
        expense.FundId = fund.Id;
        _accounting.ApplyOutflow(fund, expense.Amount);
        await _accounting.PostAsync(expense.Date, $"{expense.Description} - {expense.Vendor}", "Expense", expense.Id.ToString(),
            new[]
            {
                new JournalDraft(expense.Account.Code, expense.Amount, 0),
                new JournalDraft(fund.Type == FundType.Cash ? CoA.Cash : CoA.Bank, 0, expense.Amount)
            }, User.DisplayName(), User.CurrentUserId());
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Expense approved", nameof(Expense), expense.Id.ToString(), null, new { expense.Amount });
        TempData["Success"] = $"تم اعتماد المصروف {expense.ExpenseNo} وترحيله.";
        return RedirectToAction(nameof(Expenses));
    }

    public async Task<IActionResult> Incomes(DateTime? from, DateTime? to, int? accountId)
    {
        var today = DateTime.Today;
        var start = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var end = (to ?? today).Date;
        if (end < start) (start, end) = (end, start);
        var query = _db.Incomes.Include(i => i.Account).Include(i => i.Fund).AsNoTracking()
            .Where(i => i.Date >= start && i.Date <= end);
        if (accountId.HasValue) query = query.Where(i => i.AccountId == accountId.Value);
        var list = await query.OrderByDescending(i => i.Date).ThenByDescending(i => i.Id).Take(300).ToListAsync();
        ViewBag.From = start;
        ViewBag.To = end;
        ViewBag.AccountId = accountId;
        ViewBag.Total = list.Sum(i => i.Amount);
        ViewBag.Accounts = await _db.Accounts.Where(a => a.Type == AccountType.Revenue).OrderBy(a => a.Code).AsNoTracking().ToListAsync();
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> CreateIncome()
    {
        await LoadIncomeListsAsync();
        return View(new IncomeFormViewModel { AccountId = (await _db.Accounts.FirstOrDefaultAsync(a => a.Code == CoA.OtherRevenue))?.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateIncome(IncomeFormViewModel vm)
    {
        var account = await _db.Accounts.FindAsync(vm.AccountId ?? 0);
        if (account is null || account.Type != AccountType.Revenue)
            ModelState.AddModelError(nameof(vm.AccountId), "يرجى اختيار حساب إيراد صحيح.");
        if (!ModelState.IsValid)
        {
            await LoadIncomeListsAsync();
            return View(vm);
        }

        Fund fund;
        try { fund = await _accounting.GetFundAsync(vm.Method, vm.FundId); }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            await LoadIncomeListsAsync();
            return View(vm);
        }

        var year = vm.Date.Year;
        var seq = await _db.Incomes.CountAsync(i => i.Date.Year == year) + 1;
        string no;
        do { no = $"INC-{year}-{seq:D4}"; seq++; } while (await _db.Incomes.AnyAsync(i => i.IncomeNo == no));

        var income = new Income
        {
            IncomeNo = no,
            AccountId = account!.Id,
            FundId = fund.Id,
            Date = vm.Date.Date,
            Amount = vm.Amount,
            Method = vm.Method,
            Source = vm.Source.Trim(),
            Description = vm.Description.Trim(),
            CreatedByUserId = User.CurrentUserId(),
            CreatedByName = User.DisplayName()
        };
        _db.Incomes.Add(income);
        await _db.SaveChangesAsync();

        _accounting.ApplyInflow(fund, income.Amount);
        await _accounting.PostAsync(income.Date, $"إيراد - {income.Source}", "Income", income.Id.ToString(),
            new[]
            {
                new JournalDraft(fund.Type == FundType.Cash ? CoA.Cash : CoA.Bank, income.Amount, 0),
                new JournalDraft(account.Code, 0, income.Amount)
            }, User.DisplayName(), User.CurrentUserId());
        await _audit.LogAsync("Income recorded", nameof(Income), income.Id.ToString(), null, new { income.IncomeNo, income.Amount });

        TempData["Success"] = $"تم تسجيل الإيراد {no} في الصندوق.";
        return RedirectToAction(nameof(Incomes));
    }

    public async Task<IActionResult> Funds()
    {
        var funds = await _db.Funds.Include(f => f.Account).OrderBy(f => f.Type).ThenBy(f => f.Id).AsNoTracking().ToListAsync();
        ViewBag.Cash = funds.Where(f => f.Type == FundType.Cash).Sum(f => f.CurrentBalance);
        ViewBag.Bank = funds.Where(f => f.Type == FundType.Bank).Sum(f => f.CurrentBalance);
        ViewBag.Accounts = await GetAccountBalancesAsync(DateTime.Today.AddDays(1));
        ViewBag.JournalCount = await _db.JournalEntries.CountAsync();
        return View(funds);
    }

    public async Task<IActionResult> TeacherPayables()
    {
        var rows = await _db.TeacherEarnings.Include(e => e.Teacher).AsNoTracking().ToListAsync();
        var vm = new TeacherPayablesViewModel
        {
            Rows = rows.GroupBy(e => e.Teacher)
                .Select(g => new PayableSummary
                {
                    Teacher = g.Key,
                    Unpaid = g.Where(e => !e.IsPaid).Sum(e => e.Amount),
                    Paid = g.Where(e => e.IsPaid).Sum(e => e.Amount),
                    Count = g.Count(),
                    CourseShare = g.Where(e => e.Kind == TeacherEarningKind.CourseShare).Sum(e => e.Amount),
                    PrivateShare = g.Where(e => e.Kind == TeacherEarningKind.PrivateLesson).Sum(e => e.Amount)
                })
                .OrderByDescending(r => r.Unpaid).ThenByDescending(r => r.Paid).ToList(),
            RecentPaid = await _db.TeacherEarnings.Include(e => e.Teacher)
                .Where(e => e.IsPaid).OrderByDescending(e => e.PaidAt).ThenByDescending(e => e.Id).Take(20).ToListAsync()
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    /// <summary>
    /// Legacy quick-payout entry point now creates a governed draft statement instead of moving
    /// money directly. Leadership must approve it, then it is paid from a fund with a voucher.
    /// </summary>
    public async Task<IActionResult> PayTeacher(int teacherId, PaymentMethod method)
    {
        var teacher = await _db.Teachers.FindAsync(teacherId);
        if (teacher is null) return NotFound();
        var earliest = await _db.TeacherEarnings
            .Where(e => e.TeacherId == teacherId && !e.IsPaid)
            .MinAsync(e => (DateTime?)e.Date);
        if (earliest is null)
        {
            TempData["Warning"] = _l.T("No unpaid earnings for this teacher.", "لا توجد مستحقات غير مدفوعة لهذا المدرّس.");
            return RedirectToAction(nameof(TeacherPayables));
        }

        try
        {
            var settlement = await _settlements.GenerateAsync(teacherId, earliest.Value, DateTime.Today,
                "Generated from the teacher payables register", User.CurrentUserId(), User.DisplayName());
            await _audit.LogAsync("Generate", "TeacherSettlement", settlement.Id.ToString(), null,
                new { settlement.SettlementNo, settlement.TeacherId, settlement.Gross });
            TempData["Success"] = _l.T(
                $"Statement {settlement.SettlementNo} generated. Approve it before paying.",
                $"تم إنشاء الكشف {settlement.SettlementNo}. اعتمده قبل الصرف.");
            return RedirectToAction("Details", "TeacherSettlements", new { id = settlement.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(TeacherPayables));
        }
    }

    private async Task<List<AccountBalanceRow>> GetAccountBalancesAsync(DateTime beforeExclusive)
    {
        var rows = await _db.JournalLines
            .Where(l => l.JournalEntry.Date < beforeExclusive)
            .GroupBy(l => new { l.Account.Code, l.Account.NameAr, l.Account.NameEn, l.Account.Type })
            .Select(g => new
            {
                g.Key.Code,
                g.Key.NameAr,
                g.Key.NameEn,
                g.Key.Type,
                Debit = g.Sum(x => x.Debit),
                Credit = g.Sum(x => x.Credit)
            })
            .OrderBy(x => x.Code)
            .ToListAsync();
        return rows.Select(x => new AccountBalanceRow(x.Code, x.NameAr, x.NameEn, x.Type, x.Debit, x.Credit)).ToList();
    }

    private async Task LoadAccountsAsync()
    {
        ViewBag.Accounts = await _db.Accounts.Where(a => a.IsActive).OrderBy(a => a.Code).AsNoTracking().ToListAsync();
    }

    private async Task LoadExpenseListsAsync()
    {
        ViewBag.Accounts = await _db.Accounts.Where(a => a.Type == AccountType.Expense && a.IsActive).OrderBy(a => a.Code).AsNoTracking().ToListAsync();
        ViewBag.Funds = await _db.Funds.Where(f => f.IsActive).OrderBy(f => f.Type).AsNoTracking().ToListAsync();
        ViewBag.Courses = await _db.Courses.OrderBy(c => c.NameAr).AsNoTracking().ToListAsync();
    }

    private async Task LoadIncomeListsAsync()
    {
        ViewBag.Accounts = await _db.Accounts.Where(a => a.Type == AccountType.Revenue && a.IsActive).OrderBy(a => a.Code).AsNoTracking().ToListAsync();
        ViewBag.Funds = await _db.Funds.Where(f => f.IsActive).OrderBy(f => f.Type).AsNoTracking().ToListAsync();
    }

    private record DataRow(DateTime Date, long Amount, string? NameAr = null, string? NameEn = null);
}
