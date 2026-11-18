using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Admissions)]
public class EnrollmentsController : Controller
{
    private readonly AppDbContext _db;
    private readonly Localizer _l;
    private readonly BillingService _billing;
    private readonly TuitionService _tuition;
    private readonly CashierShiftService _cashier;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;

    public EnrollmentsController(AppDbContext db, Localizer l, BillingService billing,
        TuitionService tuition, CashierShiftService cashier, AuditService audit, NotificationService notifications)
    {
        _db = db;
        _l = l;
        _billing = billing;
        _tuition = tuition;
        _cashier = cashier;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task<IActionResult> Index(string? q, int? courseId, EnrollmentStatus? status)
    {
        var query = _db.Enrollments
            .Include(e => e.Student).ThenInclude(s => s.Grade)
            .Include(e => e.Course).ThenInclude(c => c.Teacher)
            .Include(e => e.Course).ThenInclude(c => c.Subject)
            .Include(e => e.Section)
            .Include(e => e.Dues)
            .AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(e => e.Student.FullNameAr.Contains(term) || e.Student.FullNameEn.Contains(term)
                || e.Student.StudentNo.Contains(term) || e.Course.NameAr.Contains(term));
        }
        if (courseId.HasValue) query = query.Where(e => e.CourseId == courseId.Value);
        if (status.HasValue) query = query.Where(e => e.Status == status.Value);
        var rows = await query.OrderByDescending(e => e.EnrollmentDate).ThenByDescending(e => e.Id).Take(200).ToListAsync();
        ViewBag.Q = q;
        ViewBag.CourseId = courseId;
        ViewBag.Status = status;
        ViewBag.Courses = await _db.Courses.Where(c => c.Status != CourseStatus.Cancelled).OrderBy(c => c.NameAr).ToListAsync();
        ViewBag.ActiveCount = await _db.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Active);
        ViewBag.TotalValue = await _db.Enrollments.Where(e => e.Status != EnrollmentStatus.Cancelled).SumAsync(e => (long?)e.NetFee) ?? 0;
        ViewBag.Collected = await _db.Payments.SumAsync(p => (long?)p.Amount) ?? 0;
        return View(rows);
    }

    public async Task<IActionResult> Create(int? studentId, int? courseId)
    {
        await LoadListsAsync();
        var model = new RegisterEnrollmentViewModel { StudentId = studentId ?? 0, CourseId = courseId ?? 0 };
        if (courseId.HasValue)
        {
            var course = await _db.Courses.FindAsync(courseId.Value);
            if (course is not null) model.Discount = 0;
        }
        ViewBag.SelectedStudent = studentId.HasValue
            ? await _db.Students.Include(s => s.Grade).Include(s => s.Parent).FirstOrDefaultAsync(s => s.Id == studentId)
            : null;
        ViewBag.OpenShift = await _cashier.GetOpenAsync(User.CurrentUserId());
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RegisterEnrollmentViewModel model)
    {
        var student = await _db.Students.FindAsync(model.StudentId);
        var course = await _db.Courses.Include(c => c.Sections).Include(c => c.Grade).FirstOrDefaultAsync(c => c.Id == model.CourseId);
        if (student is null) ModelState.AddModelError(nameof(model.StudentId), _l.T("Select a valid student.", "اختر طالباً صحيحاً."));
        if (course is null) ModelState.AddModelError(nameof(model.CourseId), _l.T("Select a valid course.", "اختر دورة صحيحة."));
        if (student is not null && course is not null)
        {
            if (student.GradeId != course.GradeId)
                ModelState.AddModelError(nameof(model.CourseId), _l.T("The course is not offered for this student's grade.", "هذه الدورة غير متاحة لصف الطالب."));
            if (await _db.Enrollments.AnyAsync(e => e.StudentId == model.StudentId && e.CourseId == model.CourseId && e.Status == EnrollmentStatus.Active))
                ModelState.AddModelError(string.Empty, _l.T("The student already has an active enrollment in this course.", "الطالب مشترك بالفعل في هذه الدورة."));
            if (model.Discount > course.Fee)
                ModelState.AddModelError(nameof(model.Discount), _l.T("Discount cannot exceed the course fee.", "الخصم لا يمكن أن يتجاوز رسوم الدورة."));

            var count = Math.Clamp(model.InstallmentCount, 1, TuitionService.MaxInstallments);
            var previewNet = course.Fee - model.Discount;
            var firstInstallment = count == 1 ? previewNet : previewNet / count;
            if (model.InitialPayment > firstInstallment)
                ModelState.AddModelError(nameof(model.InitialPayment), _l.T(
                    "Initial payment cannot exceed the first installment. Collect later installments from the dues register.",
                    "الدفعة الأولى لا يمكن أن تتجاوز القسط الأول. حصّل الأقساط اللاحقة من سجل الأقساط."));

            if (model.Method == PaymentMethod.Cash && model.InitialPayment > 0
                && await _cashier.GetOpenAsync(User.CurrentUserId()) is null)
                ModelState.AddModelError(nameof(model.Method), _l.T(
                    "Open a cashier shift before receiving a cash initial payment.",
                    "افتح وردية الصندوق قبل استلام دفعة أولى نقداً."));
        }
        if (!ModelState.IsValid)
        {
            await LoadListsAsync();
            ViewBag.SelectedStudent = student;
            ViewBag.OpenShift = await _cashier.GetOpenAsync(User.CurrentUserId());
            return View(model);
        }

        var net = course!.Fee - model.Discount;
        var installments = Math.Clamp(model.InstallmentCount, 1, TuitionService.MaxInstallments);
        var enrollment = new Enrollment
        {
            StudentId = student!.Id,
            Course = course,
            CourseId = course.Id,
            SectionId = model.SectionId,
            EnrollmentDate = DateTime.Today,
            Fee = course.Fee,
            Discount = model.Discount,
            NetFee = net,
            Status = EnrollmentStatus.Active,
            Notes = model.Notes
        };
        _db.Enrollments.Add(enrollment);
        await _db.SaveChangesAsync();

        var contract = await _tuition.CreateAsync(enrollment, model.Discount, installments,
            model.DueDate, model.Notes, User.CurrentUserId(), User.DisplayName());
        var firstDue = contract.Installments.OrderBy(i => i.InstallmentNo).First();

        Payment? payment = null;
        if (model.InitialPayment > 0)
        {
            var shift = model.Method == PaymentMethod.Cash
                ? await _cashier.GetOpenAsync(User.CurrentUserId())
                : null;
            payment = await _billing.ReceiveAsync(student, model.InitialPayment, model.Method, DateTime.Today,
                _l.T("Initial enrollment payment", "دفعة الاشتراك الأولى"), User.CurrentUserId(), User.DisplayName(),
                firstDue, enrollment, course, notifyGuardian: true, shift: shift);
        }

        await _audit.LogAsync("Create", "Enrollment", enrollment.Id.ToString(), null,
            new
            {
                enrollment.StudentId,
                enrollment.CourseId,
                enrollment.NetFee,
                enrollment.SectionId,
                ContractNo = contract.ContractNo,
                Installments = contract.InstallmentCount,
                InitialPayment = model.InitialPayment
            });
        await _notifications.NotifyGuardianAsync(student,
            "تم تسجيل اشتراك جديد", "New course enrollment",
            $"تم تسجيل ابنكم في دورة {course.NameAr}. الرسوم الصافية {_l.Money(net)}.",
            $"Your child was enrolled in {course.NameEn}. Net fee IQD {net:N0}.",
            $"/Students/Details/{student.Id}", NotificationType.Success);

        TempData["Success"] = _l.T($"Enrollment created for {student.FullNameEn}.", $"تم إنشاء اشتراك للطالب {student.FullNameAr}.");
        if (payment is not null)
            return RedirectToAction("Receipt", "Payments", new { id = payment.Id });
        return RedirectToAction("Details", "Students", new { id = student.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var enrollment = await _db.Enrollments
            .Include(e => e.Dues)
            .Include(e => e.TuitionContract).ThenInclude(c => c!.Installments)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (enrollment is null) return NotFound();
        enrollment.Status = EnrollmentStatus.Cancelled;
        if (enrollment.TuitionContract is not null)
        {
            await _tuition.CancelAsync(enrollment.TuitionContract);
            await _db.SaveChangesAsync();
        }
        else
        {
            foreach (var due in enrollment.Dues.Where(d => d.Status == DueStatus.Unpaid || d.Status == DueStatus.Partial))
                due.Status = DueStatus.Waived;
            await _db.SaveChangesAsync();
        }
        await _audit.LogAsync("Cancel", "Enrollment", id.ToString(), null,
            new { StudentId = enrollment.StudentId, CourseId = enrollment.CourseId, ContractNo = enrollment.TuitionContract?.ContractNo });
        TempData["Success"] = _l.T("Enrollment cancelled and unpaid installments waived.", "تم إلغاء الاشتراك وإعفاء الأقساط غير المدفوعة.");
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadListsAsync()
    {
        ViewBag.Students = await _db.Students.Where(s => s.Status == StudentStatus.Active)
            .Include(s => s.Grade).OrderBy(s => s.FullNameAr).ToListAsync();
        ViewBag.Courses = await _db.Courses.Where(c => c.Status == CourseStatus.Active)
            .Include(c => c.Grade).Include(c => c.Subject).Include(c => c.Sections)
            .OrderBy(c => c.Grade.Level).ThenBy(c => c.NameAr).ToListAsync();
        ViewBag.Sections = await _db.CourseSections.Where(s => s.IsActive).ToListAsync();
    }
}
