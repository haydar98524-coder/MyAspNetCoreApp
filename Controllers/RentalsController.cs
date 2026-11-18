using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Finance + "," + Roles.Registrar)]
public class RentalsController : Controller
{
    private readonly AppDbContext _db;
    private readonly AccountingService _accounting;
    private readonly AuditService _audit;

    public RentalsController(AppDbContext db, AccountingService accounting, AuditService audit)
    {
        _db = db;
        _accounting = accounting;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var bookings = await _db.ClassroomBookings.Include(b => b.Classroom)
            .Where(b => b.Date >= monthStart && b.Date < monthEnd).AsNoTracking().ToListAsync();
        var cafeteria = await _db.CafeteriaContracts.Include(c => c.Payments)
            .Where(c => c.IsActive).AsNoTracking().ToListAsync();
        var rents = await _db.TeacherRentContracts.Include(c => c.Teacher).Include(c => c.Payments)
            .Where(c => c.IsActive).AsNoTracking().ToListAsync();

        ViewBag.MonthStart = monthStart;
        ViewBag.HallRevenue = bookings.Where(b => b.PaymentStatus == BookingPaymentStatus.Paid).Sum(b => b.TotalAmount);
        ViewBag.HallUnpaid = bookings.Where(b => b.PaymentStatus == BookingPaymentStatus.Unpaid).Sum(b => b.TotalAmount);
        ViewBag.BookingCount = bookings.Count;
        ViewBag.CafeteriaMonthly = cafeteria.Sum(c => c.AnnualAmount / 12);
        ViewBag.CafeteriaCollected = cafeteria.SelectMany(c => c.Payments)
            .Where(p => p.IsPaid && p.PaidDate.HasValue && p.PaidDate.Value.Year == today.Year).Sum(p => p.Amount);
        ViewBag.TeacherRentMonthly = rents.Sum(c => c.MonthlyAmount);
        ViewBag.TeacherRentCollected = rents.SelectMany(c => c.Payments)
            .Where(p => p.IsPaid && p.Month.Year == today.Year && p.Month.Month == today.Month).Sum(p => p.Amount);
        ViewBag.Upcoming = await _db.ClassroomBookings.Include(b => b.Classroom)
            .Where(b => b.Date >= today).OrderBy(b => b.Date).ThenBy(b => b.StartTime).Take(6)
            .AsNoTracking().ToListAsync();
        ViewBag.CafeteriaContracts = cafeteria;
        ViewBag.RentContracts = rents;
        ViewBag.UnpaidBookings = await _db.ClassroomBookings.CountAsync(b => b.PaymentStatus == BookingPaymentStatus.Unpaid && b.Date < today);
        return View();
    }

    public async Task<IActionResult> Bookings(DateTime? from, DateTime? to, BookingPaymentStatus? status, int? classroomId)
    {
        var today = DateTime.Today;
        var start = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var end = (to ?? today.AddMonths(1)).Date;
        if (end < start) (start, end) = (end, start);

        var query = _db.ClassroomBookings.Include(b => b.Classroom).AsNoTracking()
            .Where(b => b.Date >= start && b.Date <= end);
        if (status.HasValue) query = query.Where(b => b.PaymentStatus == status.Value);
        if (classroomId.HasValue) query = query.Where(b => b.ClassroomId == classroomId.Value);
        var list = await query.OrderBy(b => b.Date).ThenBy(b => b.StartTime).ToListAsync();

        ViewBag.From = start;
        ViewBag.To = end;
        ViewBag.Status = status;
        ViewBag.ClassroomId = classroomId;
        ViewBag.Count = list.Count;
        ViewBag.Total = list.Sum(b => b.TotalAmount);
        ViewBag.Paid = list.Where(b => b.PaymentStatus == BookingPaymentStatus.Paid).Sum(b => b.TotalAmount);
        ViewBag.Unpaid = list.Where(b => b.PaymentStatus == BookingPaymentStatus.Unpaid).Sum(b => b.TotalAmount);
        ViewBag.Classrooms = await _db.Classrooms.Where(c => c.Status != ClassroomStatus.Retired).OrderBy(c => c.Number).AsNoTracking().ToListAsync();
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> CreateBooking()
    {
        var classrooms = await _db.Classrooms.Where(c => c.Status == ClassroomStatus.Available).OrderBy(c => c.Number).AsNoTracking().ToListAsync();
        ViewBag.Classrooms = classrooms;
        return View(new BookingFormViewModel
        {
            Date = DateTime.Today,
            HourlyRate = classrooms.FirstOrDefault()?.RentPerHour ?? 15_000
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBooking(BookingFormViewModel vm)
    {
        var room = await _db.Classrooms.FindAsync(vm.ClassroomId);
        if (room is null || room.Status != ClassroomStatus.Available)
            ModelState.AddModelError(nameof(vm.ClassroomId), "القاعة غير متاحة للحجز.");

        var startOk = TimeSpan.TryParse(vm.StartTime, out var start);
        var endOk = TimeSpan.TryParse(vm.EndTime, out var end);
        if (!startOk || !endOk)
            ModelState.AddModelError("", "صيغة الوقت غير صحيحة.");
        else if (end <= start)
            ModelState.AddModelError("", "وقت النهاية يجب أن يكون بعد وقت البداية.");

        if (vm.Date < DateTime.Today)
            ModelState.AddModelError(nameof(vm.Date), "لا يمكن حجز قاعة بتاريخ سابق.");

        if (ModelState.IsValid)
        {
            var dayBookings = await _db.ClassroomBookings
                .Where(b => b.ClassroomId == vm.ClassroomId && b.Date == vm.Date).ToListAsync();
            if (dayBookings.Any(b => Overlaps(vm.StartTime, vm.EndTime, b.StartTime, b.EndTime)))
                ModelState.AddModelError("", "يوجد حجز آخر يتعارض مع هذا الوقت في نفس القاعة.");

            var schedules = await _db.ClassSchedules.Include(s => s.Section).ThenInclude(s => s.Course)
                .Where(s => s.ClassroomId == vm.ClassroomId && s.DayOfWeek == vm.Date.DayOfWeek).ToListAsync();
            if (schedules.Any(s => Overlaps(vm.StartTime, vm.EndTime, s.StartTime, s.EndTime)))
                ModelState.AddModelError("", "تتعارض الفترة مع درس مجدول في هذه القاعة.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Classrooms = await _db.Classrooms.Where(c => c.Status == ClassroomStatus.Available).OrderBy(c => c.Number).AsNoTracking().ToListAsync();
            return View(vm);
        }

        var hours = (end - start).TotalHours;
        var total = (long)Math.Round(hours * vm.HourlyRate);
        var booking = new ClassroomBooking
        {
            ClassroomId = vm.ClassroomId,
            TenantName = vm.TenantName.Trim(),
            Purpose = vm.Purpose.Trim(),
            Date = vm.Date.Date,
            StartTime = vm.StartTime,
            EndTime = vm.EndTime,
            HourlyRate = vm.HourlyRate,
            TotalAmount = total,
            PaymentStatus = vm.MarkPaid ? BookingPaymentStatus.Paid : BookingPaymentStatus.Unpaid,
            Notes = vm.Notes,
            CreatedByUserId = User.CurrentUserId(),
            CreatedAt = DateTime.UtcNow
        };
        _db.ClassroomBookings.Add(booking);
        await _db.SaveChangesAsync();

        if (vm.MarkPaid && total > 0)
        {
            var fund = await _accounting.GetFundAsync(PaymentMethod.Cash);
            _accounting.ApplyInflow(fund, total);
            await _accounting.PostAsync(booking.Date, $"إيجار قاعة {room!.Number} - {booking.TenantName}", "ClassroomBooking", booking.Id.ToString(),
                new[] { new JournalDraft(CoA.Cash, total, 0), new JournalDraft(CoA.HallRent, 0, total) },
                User.DisplayName(), User.CurrentUserId());
        }

        await _audit.LogAsync("Classroom booking created", nameof(ClassroomBooking), booking.Id.ToString(), null,
            new { booking.TenantName, booking.Date, booking.TotalAmount, booking.PaymentStatus });
        TempData["Success"] = vm.MarkPaid
            ? "تم تسجيل الحجز وقبض المبلغ في الصندوق."
            : "تم تسجيل الحجز، والمبلغ بانتظار القبض.";
        return RedirectToAction(nameof(Bookings));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PayBooking(int id)
    {
        var booking = await _db.ClassroomBookings.Include(b => b.Classroom).FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound();
        if (booking.PaymentStatus == BookingPaymentStatus.Paid)
        {
            TempData["Warning"] = "تم قبض هذا الحجز مسبقاً.";
            return RedirectToAction(nameof(Bookings));
        }
        var fund = await _accounting.GetFundAsync(PaymentMethod.Cash);
        booking.PaymentStatus = BookingPaymentStatus.Paid;
        _accounting.ApplyInflow(fund, booking.TotalAmount);
        await _accounting.PostAsync(DateTime.Today, $"قبض إيجار قاعة {booking.Classroom.Number} - {booking.TenantName}", "ClassroomBooking", booking.Id.ToString(),
            new[]
            {
                new JournalDraft(CoA.Cash, booking.TotalAmount, 0),
                new JournalDraft(CoA.HallRent, 0, booking.TotalAmount)
            }, User.DisplayName(), User.CurrentUserId());
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Classroom booking paid", nameof(ClassroomBooking), booking.Id.ToString(), null, new { booking.TotalAmount });
        TempData["Success"] = "تم قبض مبلغ الحجز وترحيله.";
        return RedirectToAction(nameof(Bookings));
    }

    public async Task<IActionResult> Cafeteria()
    {
        var contracts = await _db.CafeteriaContracts.Include(c => c.Payments)
            .OrderByDescending(c => c.StartDate).AsNoTracking().ToListAsync();
        var today = DateTime.Today;
        ViewBag.CollectedThisYear = contracts.SelectMany(c => c.Payments)
            .Where(p => p.IsPaid && p.PaidDate.HasValue && p.PaidDate.Value.Year == today.Year).Sum(p => p.Amount);
        ViewBag.ExpectedAnnual = contracts.Where(c => c.IsActive).Sum(c => c.AnnualAmount);
        ViewBag.UnpaidMonths = contracts.SelectMany(c => c.Payments).Count(p => !p.IsPaid);
        return View(contracts);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCafeteriaMonth(int contractId, DateTime month, long? amount, bool payNow)
    {
        var contract = await _db.CafeteriaContracts.Include(c => c.Payments).FirstOrDefaultAsync(c => c.Id == contractId);
        if (contract is null) return NotFound();
        var period = new DateTime(month.Year, month.Month, 1);
        if (contract.Payments.Any(p => p.Month == period))
        {
            TempData["Warning"] = "يوجد استحقاق مسجل لهذا الشهر.";
            return RedirectToAction(nameof(Cafeteria));
        }
        var value = amount.GetValueOrDefault(contract.AnnualAmount / 12);
        if (value <= 0)
        {
            TempData["Error"] = "قيمة الاستحقاق غير صحيحة.";
            return RedirectToAction(nameof(Cafeteria));
        }

        var payment = new CafeteriaPayment
        {
            ContractId = contract.Id,
            Month = period,
            Amount = value,
            IsPaid = payNow,
            PaidDate = payNow ? DateTime.Today : null,
            Reference = payNow ? $"CAF-{period:yyyyMM}" : null
        };
        _db.CafeteriaPayments.Add(payment);
        await _db.SaveChangesAsync();

        if (payNow)
        {
            var fund = await _accounting.GetFundAsync(PaymentMethod.Cash);
            _accounting.ApplyInflow(fund, value);
            await _accounting.PostAsync(DateTime.Today, $"إيجار الكافتيريا لشهر {period:yyyy/MM}", "Cafeteria", payment.Id.ToString(),
                new[] { new JournalDraft(CoA.Cash, value, 0), new JournalDraft(CoA.CafeteriaRent, 0, value) },
                User.DisplayName(), User.CurrentUserId());
        }

        await _audit.LogAsync("Cafeteria rent recorded", nameof(CafeteriaPayment), payment.Id.ToString(), null,
            new { contract.TenantName, period, value, payNow });
        TempData["Success"] = payNow ? "تم تسجيل الإيجار وقبضه." : "تم تسجيل استحقاق الإيجار.";
        return RedirectToAction(nameof(Cafeteria));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PayCafeteria(int id)
    {
        var payment = await _db.CafeteriaPayments.Include(p => p.Contract).FirstOrDefaultAsync(p => p.Id == id);
        if (payment is null) return NotFound();
        if (payment.IsPaid)
        {
            TempData["Warning"] = "هذا الاستحقاق مدفوع مسبقاً.";
            return RedirectToAction(nameof(Cafeteria));
        }
        var fund = await _accounting.GetFundAsync(PaymentMethod.Cash);
        payment.IsPaid = true;
        payment.PaidDate = DateTime.Today;
        payment.Reference = $"CAF-{payment.Month:yyyyMM}";
        _accounting.ApplyInflow(fund, payment.Amount);
        await _accounting.PostAsync(DateTime.Today, $"إيجار الكافتيريا لشهر {payment.Month:yyyy/MM}", "Cafeteria", payment.Id.ToString(),
            new[] { new JournalDraft(CoA.Cash, payment.Amount, 0), new JournalDraft(CoA.CafeteriaRent, 0, payment.Amount) },
            User.DisplayName(), User.CurrentUserId());
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Cafeteria rent paid", nameof(CafeteriaPayment), payment.Id.ToString(), null, new { payment.Amount });
        TempData["Success"] = "تم قبض إيجار الكافتيريا.";
        return RedirectToAction(nameof(Cafeteria));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCafeteriaContract(string tenantName, string? tenantPhone, long annualAmount,
        DateTime startDate, DateTime endDate, string? notes)
    {
        if (string.IsNullOrWhiteSpace(tenantName) || annualAmount <= 0 || endDate <= startDate)
        {
            TempData["Error"] = "يرجى إدخال اسم المستأجر ومبلغ سنوي صحيح وتواريخ عقد صحيحة.";
            return RedirectToAction(nameof(Cafeteria));
        }
        var contract = new CafeteriaContract
        {
            TenantName = tenantName.Trim(),
            TenantPhone = (tenantPhone ?? "").Trim(),
            AnnualAmount = annualAmount,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            IsActive = true,
            Notes = notes
        };
        _db.CafeteriaContracts.Add(contract);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Cafeteria contract created", nameof(CafeteriaContract), contract.Id.ToString(), null,
            new { contract.TenantName, contract.AnnualAmount });
        TempData["Success"] = "تم إنشاء عقد الكافتيريا.";
        return RedirectToAction(nameof(Cafeteria));
    }

    public async Task<IActionResult> TeacherRent()
    {
        var contracts = await _db.TeacherRentContracts.Include(c => c.Teacher).Include(c => c.Payments)
            .OrderByDescending(c => c.IsActive).ThenBy(c => c.Teacher.FullNameAr).AsNoTracking().ToListAsync();
        var today = DateTime.Today;
        ViewBag.MonthlyTotal = contracts.Where(c => c.IsActive).Sum(c => c.MonthlyAmount);
        ViewBag.CollectedThisMonth = contracts.SelectMany(c => c.Payments)
            .Where(p => p.IsPaid && p.Month.Year == today.Year && p.Month.Month == today.Month).Sum(p => p.Amount);
        ViewBag.Unpaid = contracts.SelectMany(c => c.Payments).Count(p => !p.IsPaid);
        ViewBag.Teachers = await _db.Teachers.Where(t => t.IsActive).OrderBy(t => t.FullNameAr).AsNoTracking().ToListAsync();
        return View(contracts);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRentContract(RentContractFormViewModel vm)
    {
        var teacher = await _db.Teachers.FindAsync(vm.TeacherId);
        if (teacher is null) ModelState.AddModelError(nameof(vm.TeacherId), "المدرّس غير موجود.");
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "يرجى التحقق من بيانات العقد.";
            return RedirectToAction(nameof(TeacherRent));
        }
        var contract = new TeacherRentContract
        {
            TeacherId = vm.TeacherId,
            MonthlyAmount = vm.MonthlyAmount,
            StartDate = vm.StartDate.Date,
            EndDate = vm.EndDate?.Date,
            IsActive = true,
            Notes = vm.Notes
        };
        _db.TeacherRentContracts.Add(contract);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Teacher rent contract created", nameof(TeacherRentContract), contract.Id.ToString(), null,
            new { teacher!.FullNameAr, contract.MonthlyAmount });
        TempData["Success"] = "تم إنشاء عقد إيجار المدرّس.";
        return RedirectToAction(nameof(TeacherRent));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRentMonth(int contractId, DateTime month, long? amount, bool payNow)
    {
        var contract = await _db.TeacherRentContracts.Include(c => c.Teacher).Include(c => c.Payments)
            .FirstOrDefaultAsync(c => c.Id == contractId);
        if (contract is null) return NotFound();
        var period = new DateTime(month.Year, month.Month, 1);
        if (contract.Payments.Any(p => p.Month == period))
        {
            TempData["Warning"] = "يوجد استحقاق مسجل لهذا الشهر.";
            return RedirectToAction(nameof(TeacherRent));
        }
        var value = amount.GetValueOrDefault(contract.MonthlyAmount);
        if (value <= 0)
        {
            TempData["Error"] = "قيمة الإيجار غير صحيحة.";
            return RedirectToAction(nameof(TeacherRent));
        }

        var payment = new TeacherRentPayment
        {
            ContractId = contract.Id,
            Month = period,
            Amount = value,
            IsPaid = payNow,
            PaidDate = payNow ? DateTime.Today : null,
            Reference = payNow ? $"TR-{period:yyyyMM}-{contract.TeacherId}" : null
        };
        _db.TeacherRentPayments.Add(payment);
        await _db.SaveChangesAsync();

        if (payNow)
        {
            var fund = await _accounting.GetFundAsync(PaymentMethod.Cash);
            _accounting.ApplyInflow(fund, value);
            await _accounting.PostAsync(DateTime.Today, $"إيجار تدريس خصوصي - {contract.Teacher.FullNameAr} - {period:yyyy/MM}",
                "TeacherRent", payment.Id.ToString(),
                new[] { new JournalDraft(CoA.Cash, value, 0), new JournalDraft(CoA.PrepTeacherRent, 0, value) },
                User.DisplayName(), User.CurrentUserId());
        }

        await _audit.LogAsync("Teacher rent recorded", nameof(TeacherRentPayment), payment.Id.ToString(), null,
            new { contract.Teacher.FullNameAr, period, value, payNow });
        TempData["Success"] = payNow ? "تم تسجيل الإيجار وقبضه." : "تم تسجيل استحقاق الإيجار.";
        return RedirectToAction(nameof(TeacherRent));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PayTeacherRent(int id)
    {
        var payment = await _db.TeacherRentPayments.Include(p => p.Contract).ThenInclude(c => c.Teacher)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (payment is null) return NotFound();
        if (payment.IsPaid)
        {
            TempData["Warning"] = "هذا الاستحقاق مدفوع مسبقاً.";
            return RedirectToAction(nameof(TeacherRent));
        }
        var fund = await _accounting.GetFundAsync(PaymentMethod.Cash);
        payment.IsPaid = true;
        payment.PaidDate = DateTime.Today;
        payment.Reference = $"TR-{payment.Month:yyyyMM}-{payment.Contract.TeacherId}";
        _accounting.ApplyInflow(fund, payment.Amount);
        await _accounting.PostAsync(DateTime.Today, $"إيجار تدريس خصوصي - {payment.Contract.Teacher.FullNameAr} - {payment.Month:yyyy/MM}",
            "TeacherRent", payment.Id.ToString(),
            new[] { new JournalDraft(CoA.Cash, payment.Amount, 0), new JournalDraft(CoA.PrepTeacherRent, 0, payment.Amount) },
            User.DisplayName(), User.CurrentUserId());
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Teacher rent paid", nameof(TeacherRentPayment), payment.Id.ToString(), null, new { payment.Amount });
        TempData["Success"] = "تم قبض إيجار المدرّس.";
        return RedirectToAction(nameof(TeacherRent));
    }

    private static bool Overlaps(string startA, string endA, string startB, string endB)
    {
        if (!TimeSpan.TryParse(startA, out var a1) || !TimeSpan.TryParse(endA, out var a2)) return false;
        if (!TimeSpan.TryParse(startB, out var b1) || !TimeSpan.TryParse(endB, out var b2)) return false;
        return a1 < b2 && b1 < a2;
    }
}
