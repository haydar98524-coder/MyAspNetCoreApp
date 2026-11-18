using KirkukInstitute.Data;
using KirkukInstitute.Models;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Services;

public class DashboardService
{
    private readonly AppDbContext _db;
    private readonly Localizer _l;

    public DashboardService(AppDbContext db, Localizer l)
    {
        _db = db;
        _l = l;
    }

    public async Task<DashboardViewModel> BuildAsync(DateTime from, DateTime to, string periodLabel, string? userId, bool isArabic)
    {
        var today = DateTime.Today;
        var start = from.Date;
        var end = to.Date.AddDays(1);
        var yearStart = new DateTime(today.Year, 1, 1);
        var fetchStart = new[] { start, yearStart, today.AddMonths(-5) }.Min();
        fetchStart = new DateTime(fetchStart.Year, fetchStart.Month, 1);

        var paymentRows = await _db.Payments
            .Where(p => p.Date >= fetchStart && p.Date <= today)
            .Select(p => new DataRow(p.Date, p.Amount)).ToListAsync();
        var incomeRows = await _db.Incomes
            .Where(i => i.Date >= fetchStart && i.Date <= today)
            .Select(i => new DataRow(i.Date, i.Amount)).ToListAsync();
        var hallRows = await _db.ClassroomBookings
            .Where(x => x.PaymentStatus == BookingPaymentStatus.Paid && x.Date >= fetchStart && x.Date <= today)
            .Select(x => new DataRow(x.Date, x.TotalAmount)).ToListAsync();
        var cafeteriaRows = await _db.CafeteriaPayments
            .Where(x => x.IsPaid && x.PaidDate >= fetchStart && x.PaidDate <= today)
            .Select(x => new DataRow(x.PaidDate!.Value, x.Amount)).ToListAsync();
        var rentRows = await _db.TeacherRentPayments
            .Where(x => x.IsPaid && x.PaidDate >= fetchStart && x.PaidDate <= today)
            .Select(x => new DataRow(x.PaidDate!.Value, x.Amount)).ToListAsync();
        var privateLessonRows = await _db.PrivateLessons
            .Where(x => x.IsPaid && x.PaidDate >= fetchStart && x.PaidDate <= today)
            .Select(x => new DataRow(x.PaidDate!.Value, x.Fee)).ToListAsync();

        var expenseRows = await _db.Expenses
            .Where(x => x.Status == ExpenseStatus.Approved && x.Date >= fetchStart && x.Date <= today)
            .Select(x => new DataRow(x.Date, x.Amount)).ToListAsync();
        var earningRows = await _db.TeacherEarnings
            .Where(x => x.Date >= fetchStart && x.Date <= today)
            .Select(x => new DataRow(x.Date, x.Amount)).ToListAsync();
        var payrollRows = await _db.Payrolls
            .Where(x => x.Status == PayrollStatus.Paid && x.PaidAt >= fetchStart && x.PaidAt <= today)
            .Select(x => new DataRow(x.PaidAt!.Value, x.Net)).ToListAsync();

        long InRange(IEnumerable<DataRow> rows, DateTime a, DateTime bExclusive) =>
            rows.Where(r => r.Date >= a && r.Date < bExclusive).Sum(r => r.Amount);

        long RevenueInRange(DateTime a, DateTime bExclusive) =>
            InRange(paymentRows, a, bExclusive) + InRange(incomeRows, a, bExclusive) +
            InRange(hallRows, a, bExclusive) + InRange(cafeteriaRows, a, bExclusive) +
            InRange(rentRows, a, bExclusive) + InRange(privateLessonRows, a, bExclusive);

        var periodRevenue = RevenueInRange(start, end);
        var tuitionPeriod = InRange(paymentRows, start, end);
        var hallPeriod = InRange(hallRows, start, end);
        var cafeteriaPeriod = InRange(cafeteriaRows, start, end);
        var teacherRentPeriod = InRange(rentRows, start, end);
        var privatePeriod = InRange(privateLessonRows, start, end);
        var otherPeriod = InRange(incomeRows, start, end);
        var directExpenses = InRange(expenseRows, start, end);
        var teacherCosts = InRange(earningRows, start, end);
        var payroll = InRange(payrollRows, start, end);

        var dues = await _db.Dues.Where(d => d.Status != DueStatus.Paid && d.Status != DueStatus.Waived)
            .Select(d => new { d.Amount, d.PaidAmount, d.DueDate }).ToListAsync();
        var totalDues = dues.Sum(d => Math.Max(0, d.Amount - d.PaidAmount));

        var attendanceToday = await _db.StudentAttendances.Where(a => a.Date == today)
            .GroupBy(a => a.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
        int presentToday = attendanceToday.Where(a => a.Status is AttendanceStatus.Present or AttendanceStatus.Late).Sum(a => a.Count);
        int absentToday = attendanceToday.Where(a => a.Status == AttendanceStatus.Absent).Sum(a => a.Count);

        var weekStart = today.AddDays(-6);
        var attendanceWeek = await _db.StudentAttendances.Where(a => a.Date >= weekStart && a.Date <= today)
            .Select(a => new { a.Date, a.Status }).ToListAsync();
        var attendancePoints = Enumerable.Range(0, 7).Select(offset =>
        {
            var day = weekStart.AddDays(offset);
            var rows = attendanceWeek.Where(a => a.Date == day).ToList();
            return new AttendancePoint(
                isArabic ? _l.Day(day.DayOfWeek) : day.DayOfWeek.ToString()[..3],
                rows.Count(a => a.Status is AttendanceStatus.Present or AttendanceStatus.Late),
                rows.Count(a => a.Status == AttendanceStatus.Absent));
        }).ToList();

        var trend = new List<ChartPoint>();
        for (var i = 5; i >= 0; i--)
        {
            var month = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
            var next = month.AddMonths(1);
            trend.Add(new ChartPoint(
                _l.MonthShort(month.Month),
                RevenueInRange(month, next),
                InRange(expenseRows, month, next) + InRange(earningRows, month, next) + InRange(payrollRows, month, next)));
        }

        var coursePayments = await _db.Payments
            .Where(p => p.Enrollment != null)
            .Select(p => new { p.Date, p.Amount, CourseId = p.Enrollment!.CourseId, NameAr = p.Enrollment.Course.NameAr, NameEn = p.Enrollment.Course.NameEn })
            .ToListAsync();
        var courseCosts = await _db.TeacherEarnings
            .Where(x => x.Kind == TeacherEarningKind.CourseShare && x.CourseId != null)
            .Select(x => new { x.Date, x.Amount, CourseId = x.CourseId!.Value }).ToListAsync();
        var courseExpenses = await _db.Expenses
            .Where(x => x.CourseId != null && x.Status == ExpenseStatus.Approved)
            .Select(x => new { x.Date, x.Amount, CourseId = x.CourseId!.Value }).ToListAsync();
        var topCourseIds = coursePayments
            .Where(p => p.Date >= start && p.Date < end)
            .GroupBy(p => p.CourseId)
            .Select(g => new { CourseId = g.Key, Revenue = g.Sum(x => x.Amount) })
            .OrderByDescending(x => x.Revenue).Take(5).ToList();
        if (topCourseIds.Count == 0)
        {
            topCourseIds = coursePayments.GroupBy(p => p.CourseId)
                .Select(g => new { CourseId = g.Key, Revenue = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Revenue).Take(5).ToList();
        }
        var todaySchedules = await _db.ClassSchedules
            .Where(s => s.DayOfWeek == today.DayOfWeek && s.Section.IsActive)
            .Include(s => s.Section).ThenInclude(x => x.Course).ThenInclude(c => c.Teacher)
            .Include(s => s.Classroom)
            .OrderBy(s => s.StartTime)
            .Take(6).ToListAsync();

        var recentPayments = await _db.Payments.Include(p => p.Student)
            .OrderByDescending(p => p.Date).ThenByDescending(p => p.Id).Take(5)
            .Select(p => new RecentPayment(p.ReceiptNo, p.Student.FullNameAr, p.Amount, p.Date))
            .ToListAsync();

        var pendingLessons = await _db.PrivateLessonRequests.CountAsync(r => r.Status == RequestStatus.Pending || r.Status == RequestStatus.Matched);
        var pendingChanges = await _db.AttendanceChangeRequests.CountAsync(r => r.Status == ChangeRequestStatus.Pending);
        var overdueDues = dues.Where(d => d.DueDate < today).ToList();
        var ratings = await _db.Teachers.Where(t => t.RatingCount > 0).Select(t => t.RatingAvg).ToListAsync();

        var userRoles = userId is null ? new List<string>()
            : (await _db.UserRoles.Where(ur => ur.UserId == userId).Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name).ToListAsync())
              .Where(r => r is not null).Select(r => r!).ToList();
        var unread = await _db.Notifications.CountAsync(n =>
            !n.IsRead && (n.UserId == userId || (n.AudienceRole != null && userRoles.Contains(n.AudienceRole))));

        var vm = new DashboardViewModel
        {
            PeriodLabel = periodLabel,
            From = start,
            To = to.Date,
            TotalStudents = await _db.Students.CountAsync(s => s.Status == StudentStatus.Active),
            NewStudentsThisMonth = await _db.Students.CountAsync(s => s.RegistrationDate.Year == today.Year && s.RegistrationDate.Month == today.Month),
            PresentToday = presentToday,
            AbsentToday = absentToday,
            TeacherCount = await _db.Teachers.CountAsync(t => t.IsActive),
            EmployeeCount = await _db.Employees.CountAsync(e => e.Status == StaffStatus.Active),
            CourseCount = await _db.Courses.CountAsync(c => c.Status == CourseStatus.Active),
            SectionCount = await _db.CourseSections.CountAsync(s => s.IsActive),
            ClassroomCount = await _db.Classrooms.CountAsync(c => c.Status != ClassroomStatus.Retired),
            OccupiedClassroomsToday = todaySchedules.Select(s => s.ClassroomId).Distinct().Count(),
            RevenueToday = RevenueInRange(today, today.AddDays(1)),
            RevenueMonth = RevenueInRange(new DateTime(today.Year, today.Month, 1), today.AddDays(1)),
            RevenueYear = RevenueInRange(yearStart, today.AddDays(1)),
            PeriodRevenue = periodRevenue,
            PeriodExpenses = directExpenses,
            TeacherCostsPeriod = teacherCosts,
            PayrollPeriod = payroll,
            TotalDues = totalDues,
            NetProfitPeriod = periodRevenue - directExpenses - teacherCosts - payroll,
            HallRentRevenue = hallPeriod,
            CafeteriaRevenue = cafeteriaPeriod,
            TeacherRentRevenue = teacherRentPeriod,
            PrivateLessonRevenue = privatePeriod,
            PendingPrivateRequests = pendingLessons,
            HomeLessonsCount = await _db.PrivateLessonRequests.CountAsync(r => r.Mode == TeachingMode.Home && r.Status != RequestStatus.Cancelled),
            AverageRating = ratings.Count == 0 ? 0 : Math.Round(ratings.Average(), 1),
            PendingChangeRequests = pendingChanges,
            UnreadNotifications = unread,
            Trend = trend,
            AttendanceWeek = attendancePoints,
            RecentPayments = recentPayments,
            TodaySessions = todaySchedules.Select(s => new TodaySession(
                $"{s.StartTime}-{s.EndTime}",
                _l.Pick(s.Section.Course.NameAr, s.Section.Course.NameEn),
                _l.Pick(s.Section.Course.Teacher.FullNameAr, s.Section.Course.Teacher.FullNameEn),
                _l.Pick(s.Classroom.NameAr, s.Classroom.NameEn),
                s.Section.Name)).ToList(),
            RevenueSources = new List<NamedAmount>
            {
                new(_l.T("Tuition fees", "الرسوم الدراسية"), tuitionPeriod, "#0f766e"),
                new(_l.T("Private lessons", "الدروس الخصوصية"), privatePeriod, "#c9a227"),
                new(_l.T("Hall rentals", "إيجار القاعات"), hallPeriod, "#3b82f6"),
                new(_l.T("Cafeteria rent", "إيجار الكافيتريا"), cafeteriaPeriod, "#f97316"),
                new(_l.T("Teacher room rent", "إيجار مدرسي السادس"), teacherRentPeriod, "#8b5cf6"),
                new(_l.T("Other income", "إيرادات أخرى"), otherPeriod, "#64748b")
            }.Where(x => x.Amount > 0).ToList()
        };

        foreach (var top in topCourseIds)
        {
            var payment = coursePayments.FirstOrDefault(p => p.CourseId == top.CourseId);
            if (payment is null) continue;
            var revenue = coursePayments.Where(p => p.CourseId == top.CourseId && p.Date >= start && p.Date < end).Sum(p => p.Amount);
            var cost = courseCosts.Where(c => c.CourseId == top.CourseId && c.Date >= start && c.Date < end).Sum(c => c.Amount);
            var exp = courseExpenses.Where(c => c.CourseId == top.CourseId && c.Date >= start && c.Date < end).Sum(c => c.Amount);
            vm.TopCourses.Add(new CourseProfitRow(_l.Pick(payment.NameAr, payment.NameEn), revenue, cost + exp, revenue - cost - exp));
        }

        if (overdueDues.Count > 0)
            vm.Alerts.Add(new DashboardAlert(
                _l.T("Overdue student dues", "ذمم طلاب متأخرة"),
                _l.T($"{overdueDues.Count} accounts · {_l.Money(overdueDues.Sum(d => Math.Max(0, d.Amount - d.PaidAmount)))}", $"{overdueDues.Count} حساباً · {_l.Money(overdueDues.Sum(d => Math.Max(0, d.Amount - d.PaidAmount)))}"),
                "danger", "/Dues"));
        if (pendingChanges > 0)
            vm.Alerts.Add(new DashboardAlert(
                _l.T("Attendance changes awaiting approval", "طلبات تعديل حضور بانتظار الموافقة"),
                _l.T($"{pendingChanges} requests require a manager decision", $"{pendingChanges} طلباً يحتاج قرار الإدارة"),
                "warning", "/Attendance/Requests"));
        if (pendingLessons > 0)
            vm.Alerts.Add(new DashboardAlert(
                _l.T("Private lesson requests", "طلبات دروس خصوصية"),
                _l.T($"{pendingLessons} requests are waiting for assignment", $"{pendingLessons} طلباً بانتظار الإسناد"),
                "info", "/PrivateLessons"));
        var absentAlert = absentToday > 0 ? new DashboardAlert(
            _l.T("Today's absences", "غياب اليوم"),
            _l.T($"{absentToday} students marked absent", $"{absentToday} طالباً مسجل غياباً"),
            "warning", "/Attendance") : null;
        if (absentAlert is not null) vm.Alerts.Add(absentAlert);

        return vm;
    }

    private record DataRow(DateTime Date, long Amount);
}
