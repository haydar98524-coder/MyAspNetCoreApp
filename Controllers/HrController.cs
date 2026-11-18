using System.Globalization;
using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.People + "," + Roles.Accountant)]
public class HrController : Controller
{
    private readonly AppDbContext _db;
    private readonly AccountingService _accounting;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;
    private readonly Localizer _l;

    public HrController(AppDbContext db, AccountingService accounting, AuditService audit,
        NotificationService notifications, Localizer localizer)
    {
        _db = db;
        _accounting = accounting;
        _audit = audit;
        _notifications = notifications;
        _l = localizer;
    }

    public async Task<IActionResult> Index(string? q, string? status, string? department)
    {
        StaffStatus? parsed = Enum.TryParse<StaffStatus>(status, out var s) ? s : null;
        var query = _db.Employees.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(e => e.FullNameAr.Contains(term) || e.FullNameEn.Contains(term) ||
                                     e.Code.Contains(term) || e.Phone.Contains(term) ||
                                     e.JobTitleAr.Contains(term) || e.JobTitleEn.Contains(term));
        }
        if (parsed.HasValue) query = query.Where(e => e.Status == parsed.Value);
        if (!string.IsNullOrWhiteSpace(department)) query = query.Where(e => e.Department == department);

        var employees = await query.OrderBy(e => e.Department).ThenBy(e => e.FullNameAr).ToListAsync();
        var vm = new EmployeesIndexViewModel
        {
            Total = await _db.Employees.CountAsync(),
            Active = await _db.Employees.CountAsync(e => e.Status == StaffStatus.Active),
            OnLeave = await _db.Employees.CountAsync(e => e.Status == StaffStatus.OnLeave),
            MonthlyPayroll = await _db.Employees.Where(e => e.Status == StaffStatus.Active)
                .SumAsync(e => e.BasicSalary + e.Allowances),
            Employees = employees
        };
        ViewBag.Q = q;
        ViewBag.Status = parsed;
        ViewBag.Department = department;
        ViewBag.Departments = await _db.Employees.AsNoTracking()
            .Select(e => e.Department).Distinct().OrderBy(x => x).ToListAsync();
        return View(vm);
    }

    [Authorize(Roles = Roles.People)]
    public IActionResult Create()
    {
        return View("Form", new EmployeeFormViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.People)]
    public async Task<IActionResult> Create(EmployeeFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Form", model);

        var year = DateTime.Today.Year;
        var seq = await _db.Employees.CountAsync() + 1;
        string code;
        do
        {
            code = $"E-{year}-{seq:D3}";
            seq++;
        } while (await _db.Employees.AnyAsync(e => e.Code == code));

        var employee = new Employee
        {
            Code = code,
            FullNameAr = model.FullNameAr.Trim(),
            FullNameEn = model.FullNameEn.Trim(),
            JobTitleAr = model.JobTitleAr.Trim(),
            JobTitleEn = model.JobTitleEn.Trim(),
            Department = model.Department.Trim(),
            Phone = model.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
            BasicSalary = model.BasicSalary,
            Allowances = model.Allowances,
            HireDate = model.HireDate.Date,
            Status = model.Status,
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim()
        };
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Employee created", nameof(Employee), employee.Id.ToString(), null,
            new { employee.Code, employee.FullNameAr, employee.JobTitleAr, employee.BasicSalary });
        TempData["Success"] = _l.T($"Employee {employee.FullNameAr} was added.", $"تمت إضافة الموظف {employee.FullNameAr}.");
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.People)]
    public async Task<IActionResult> Edit(int id)
    {
        var employee = await _db.Employees.FindAsync(id);
        if (employee is null) return NotFound();
        var vm = new EmployeeFormViewModel
        {
            Id = employee.Id,
            FullNameAr = employee.FullNameAr,
            FullNameEn = employee.FullNameEn,
            JobTitleAr = employee.JobTitleAr,
            JobTitleEn = employee.JobTitleEn,
            Department = employee.Department,
            Phone = employee.Phone,
            Email = employee.Email,
            BasicSalary = employee.BasicSalary,
            Allowances = employee.Allowances,
            HireDate = employee.HireDate,
            Status = employee.Status,
            Notes = employee.Notes
        };
        return View("Form", vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.People)]
    public async Task<IActionResult> Edit(int id, EmployeeFormViewModel model)
    {
        var employee = await _db.Employees.FindAsync(id);
        if (employee is null) return NotFound();
        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View("Form", model);
        }

        var old = new { employee.FullNameAr, employee.JobTitleAr, employee.BasicSalary, employee.Allowances, employee.Status };
        employee.FullNameAr = model.FullNameAr.Trim();
        employee.FullNameEn = model.FullNameEn.Trim();
        employee.JobTitleAr = model.JobTitleAr.Trim();
        employee.JobTitleEn = model.JobTitleEn.Trim();
        employee.Department = model.Department.Trim();
        employee.Phone = model.Phone.Trim();
        employee.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        employee.BasicSalary = model.BasicSalary;
        employee.Allowances = model.Allowances;
        employee.HireDate = model.HireDate.Date;
        employee.Status = model.Status;
        employee.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Employee updated", nameof(Employee), employee.Id.ToString(), old,
            new { employee.FullNameAr, employee.JobTitleAr, employee.BasicSalary, employee.Allowances, employee.Status });
        TempData["Success"] = _l.T("Employee record updated.", "تم تحديث بيانات الموظف.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.People)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var employee = await _db.Employees.FindAsync(id);
        if (employee is null) return NotFound();
        employee.Status = StaffStatus.Inactive;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Employee deactivated", nameof(Employee), id.ToString(), null, new { employee.Status });
        TempData["Success"] = _l.T("Employee marked as inactive.", "تم تعيين الموظف كغير نشط.");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Attendance(DateTime? date)
    {
        var day = (date ?? DateTime.Today).Date;
        if (day > DateTime.Today) day = DateTime.Today;

        var employees = await _db.Employees.AsNoTracking()
            .Where(e => e.Status != StaffStatus.Inactive)
            .OrderBy(e => e.Department).ThenBy(e => e.FullNameAr)
            .ToListAsync();
        var records = await _db.EmployeeAttendances
            .Where(a => a.Date == day)
            .ToDictionaryAsync(a => a.EmployeeId);

        var vm = new StaffAttendanceViewModel
        {
            Date = day,
            AlreadyRecorded = records.Count > 0,
            Rows = employees.Select(e =>
            {
                records.TryGetValue(e.Id, out var record);
                return new StaffAttendanceRow
                {
                    EmployeeId = e.Id,
                    Employee = e,
                    Record = record,
                    Status = record?.Status ?? AttendanceStatus.Present,
                    CheckIn = record?.CheckIn,
                    CheckOut = record?.CheckOut,
                    Notes = record?.Notes
                };
            }).ToList()
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAttendance(StaffAttendanceViewModel model)
    {
        var day = model.Date.Date;
        if (day > DateTime.Today)
        {
            TempData["Error"] = _l.T("You cannot record attendance for a future date.", "لا يمكن تسجيل الحضور بتاريخ مستقبلي.");
            return RedirectToAction(nameof(Attendance));
        }

        var existing = await _db.EmployeeAttendances.Where(a => a.Date == day).ToDictionaryAsync(a => a.EmployeeId);
        var activeIds = await _db.Employees.Where(e => e.Status != StaffStatus.Inactive).Select(e => e.Id).ToListAsync();
        var saved = 0;
        foreach (var row in model.Rows.Where(r => activeIds.Contains(r.EmployeeId)))
        {
            var employeeId = row.EmployeeId;
            if (existing.TryGetValue(employeeId, out var record))
            {
                record.Status = row.Status;
                record.CheckIn = row.CheckIn;
                record.CheckOut = row.CheckOut;
                record.Notes = row.Notes;
            }
            else
            {
                _db.EmployeeAttendances.Add(new EmployeeAttendance
                {
                    EmployeeId = employeeId,
                    Date = day,
                    Status = row.Status,
                    CheckIn = row.CheckIn,
                    CheckOut = row.CheckOut,
                    Notes = row.Notes
                });
            }
            saved++;
        }
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Staff attendance saved", nameof(EmployeeAttendance), day.ToString("yyyy-MM-dd"), null,
            new { Date = day, Count = saved });
        TempData["Success"] = _l.T($"Staff attendance saved for {saved} employees.", $"تم حفظ حضور {saved} موظفاً.");
        return RedirectToAction(nameof(Attendance), new { date = day.ToString("yyyy-MM-dd") });
    }

    public async Task<IActionResult> Payroll(string? month)
    {
        var normalized = NormalizeMonth(month);
        var rows = await _db.Employees.AsNoTracking()
            .Where(e => e.Status != StaffStatus.Inactive || _db.Payrolls.Any(p => p.EmployeeId == e.Id && p.Month == normalized))
            .Include(e => e.Payrolls)
            .OrderBy(e => e.Department).ThenBy(e => e.FullNameAr)
            .ToListAsync();

        var payrolls = await _db.Payrolls.AsNoTracking().Where(p => p.Month == normalized)
            .ToDictionaryAsync(p => p.EmployeeId);

        var vm = new PayrollViewModel
        {
            Month = normalized,
            Rows = rows.Select(e => new PayrollRow
            {
                Employee = e,
                Payroll = payrolls.TryGetValue(e.Id, out var p) ? p : e.Payrolls.FirstOrDefault(x => x.Month == normalized)
            }).ToList()
        };
        ViewBag.MonthValue = normalized.ToString("yyyy-MM");
        ViewBag.HasPaid = vm.Rows.Any(r => r.Payroll?.Status == PayrollStatus.Paid);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.People)]
    public async Task<IActionResult> GeneratePayroll(string? month)
    {
        var normalized = NormalizeMonth(month);
        var employees = await _db.Employees.Where(e => e.Status != StaffStatus.Inactive).ToListAsync();
        var existing = await _db.Payrolls.Where(p => p.Month == normalized).ToDictionaryAsync(p => p.EmployeeId);
        var absences = await _db.EmployeeAttendances
            .Where(a => a.Date >= normalized && a.Date < normalized.AddMonths(1) && a.Status == AttendanceStatus.Absent)
            .GroupBy(a => a.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Count);

        var created = 0;
        foreach (var employee in employees)
        {
            if (existing.TryGetValue(employee.Id, out var payroll) && payroll.Status != PayrollStatus.Draft)
                continue;

            var absenceDays = absences.TryGetValue(employee.Id, out var days) ? days : 0;
            var deduction = (long)Math.Round(employee.BasicSalary / 30m * absenceDays, MidpointRounding.AwayFromZero);
            var net = employee.BasicSalary + employee.Allowances - deduction;
            if (payroll is null)
            {
                payroll = new Payroll { EmployeeId = employee.Id, Month = normalized };
                _db.Payrolls.Add(payroll);
            }
            payroll.Basic = employee.BasicSalary;
            payroll.Allowances = employee.Allowances;
            payroll.Overtime = 0;
            payroll.AbsenceDeduction = deduction;
            payroll.OtherDeductions = 0;
            payroll.Advances = 0;
            payroll.Net = Math.Max(0, net);
            payroll.Status = PayrollStatus.Draft;
            created++;
        }
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Payroll generated", nameof(Payroll), normalized.ToString("yyyyMM"), null,
            new { Month = normalized, Employees = created });
        TempData["Success"] = _l.T($"Payroll prepared for {created} employees. Absence deductions were applied automatically.",
            $"تم إعداد رواتب {created} موظفاً مع احتساب خصومات الغياب تلقائياً.");
        return RedirectToAction(nameof(Payroll), new { month = normalized.ToString("yyyy-MM") });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.People)]
    public async Task<IActionResult> ApprovePayroll(string? month)
    {
        var normalized = NormalizeMonth(month);
        var drafts = await _db.Payrolls.Where(p => p.Month == normalized && p.Status == PayrollStatus.Draft).ToListAsync();
        if (drafts.Count == 0)
        {
            TempData["Warning"] = _l.T("There are no draft payroll rows to approve.", "لا توجد رواتب مسودة للاعتماد.");
            return RedirectToAction(nameof(Payroll), new { month = normalized.ToString("yyyy-MM") });
        }
        foreach (var payroll in drafts) payroll.Status = PayrollStatus.Approved;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Payroll approved", nameof(Payroll), normalized.ToString("yyyyMM"), null, new { Count = drafts.Count });
        TempData["Success"] = _l.T($"Payroll approved for {drafts.Count} employees.", $"تم اعتماد رواتب {drafts.Count} موظفاً.");
        return RedirectToAction(nameof(Payroll), new { month = normalized.ToString("yyyy-MM") });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Finance)]
    public async Task<IActionResult> PayPayroll(string? month)
    {
        var normalized = NormalizeMonth(month);
        var rows = await _db.Payrolls
            .Include(p => p.Employee)
            .Where(p => p.Month == normalized && p.Status == PayrollStatus.Approved)
            .ToListAsync();
        if (rows.Count == 0)
        {
            TempData["Warning"] = _l.T("There are no approved payroll rows to pay.", "لا توجد رواتب معتمدة للصرف.");
            return RedirectToAction(nameof(Payroll), new { month = normalized.ToString("yyyy-MM") });
        }

        var fund = await _db.Funds.Include(f => f.Account)
            .Where(f => f.IsActive && f.Type == FundType.Bank).OrderBy(f => f.Id).FirstOrDefaultAsync()
            ?? await _db.Funds.Include(f => f.Account).Where(f => f.IsActive).OrderBy(f => f.Id).FirstOrDefaultAsync();
        if (fund is null)
        {
            TempData["Error"] = _l.T("No active fund is configured for salary payment.", "لا يوجد صندوق نشط لصرف الرواتب.");
            return RedirectToAction(nameof(Payroll), new { month = normalized.ToString("yyyy-MM") });
        }

        var total = rows.Sum(p => p.Net);
        if (fund.CurrentBalance < total)
        {
            TempData["Error"] = _l.T("The fund balance is not sufficient for this payroll.", "رصيد الصندوق لا يكفي لصرف هذه الرواتب.");
            return RedirectToAction(nameof(Payroll), new { month = normalized.ToString("yyyy-MM") });
        }

        var referenceId = normalized.ToString("yyyyMM");
        var alreadyPosted = await _db.JournalEntries.AnyAsync(j => j.ReferenceType == "Payroll" && j.ReferenceId == referenceId);
        if (!alreadyPosted)
        {
            await _accounting.PostAsync(DateTime.Today,
                _l.T($"Employee payroll for {normalized:yyyy/MM}", $"رواتب الموظفين لشهر {normalized:yyyy/MM}"),
                "Payroll", referenceId, new[]
                {
                    new JournalDraft(CoA.Salaries, total, 0),
                    new JournalDraft(fund.Account.Code, 0, total)
                }, User.DisplayName(), User.CurrentUserId());
        }

        var paidAt = DateTime.Today;
        foreach (var payroll in rows)
        {
            payroll.Status = PayrollStatus.Paid;
            payroll.PaidAt = paidAt;
        }
        fund.CurrentBalance -= total;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Payroll paid", nameof(Payroll), referenceId, null,
            new { Month = normalized, Total = total, Fund = fund.NameAr });
        foreach (var payroll in rows.Where(p => !string.IsNullOrEmpty(p.Employee.UserId)))
        {
            await _notifications.NotifyUserAsync(payroll.Employee.UserId,
                "إشعار راتب", "Salary advice",
                $"تم إيداع راتب شهر {normalized:yyyy/MM}. الصافي: {_l.Money(payroll.Net)}.",
                $"Your salary for {normalized:yyyy/MM} has been paid. Net: {_l.Money(payroll.Net)}.",
                "/Hr/Payroll", NotificationType.Success);
        }

        TempData["Success"] = _l.T($"Payroll paid: {_l.Money(total)} to {rows.Count} employees.",
            $"تم صرف الرواتب: {_l.Money(total)} لـ {rows.Count} موظفاً.");
        return RedirectToAction(nameof(Payroll), new { month = normalized.ToString("yyyy-MM") });
    }

    public async Task<IActionResult> Slip(int id)
    {
        var payroll = await _db.Payrolls
            .Include(p => p.Employee)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (payroll is null) return NotFound();
        ViewBag.Settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1);
        return View(payroll);
    }

    private static DateTime NormalizeMonth(string? month)
    {
        if (!string.IsNullOrWhiteSpace(month) &&
            DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return new DateTime(parsed.Year, parsed.Month, 1);
        if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse(month, out parsed))
            return new DateTime(parsed.Year, parsed.Month, 1);
        return new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    }
}
