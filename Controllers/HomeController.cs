using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly DashboardService _dashboard;
    private readonly Localizer _l;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;
    private readonly UserManager<ApplicationUser> _users;
    private readonly StudentNumberService _numbers;

    public HomeController(AppDbContext db, DashboardService dashboard, Localizer l,
        AuditService audit, NotificationService notifications, UserManager<ApplicationUser> users,
        StudentNumberService numbers)
    {
        _db = db;
        _dashboard = dashboard;
        _l = l;
        _audit = audit;
        _notifications = notifications;
        _users = users;
        _numbers = numbers;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1) ?? new InstituteSettings();
        var vm = new LandingViewModel
        {
            Settings = settings,
            StudentCount = await _db.Students.CountAsync(s => s.Status == StudentStatus.Active),
            TeacherCount = await _db.Teachers.CountAsync(t => t.IsActive),
            CourseCount = await _db.Courses.CountAsync(c => c.Status == CourseStatus.Active),
            GraduateCount = await _db.Students.CountAsync(s => s.Status == StudentStatus.Graduated),
            GradeCount = await _db.Grades.CountAsync(),
            FeaturedCourses = await _db.Courses.AsNoTracking()
                .Where(c => c.Status == CourseStatus.Active)
                .Include(c => c.Teacher).Include(c => c.Subject).Include(c => c.Grade)
                .Include(c => c.Sections).ThenInclude(s => s.Schedules)
                .Include(c => c.Enrollments)
                .OrderByDescending(c => c.Enrollments.Count)
                .Take(6).ToListAsync(),
            FeaturedTeachers = await _db.Teachers.AsNoTracking()
                .Where(t => t.IsActive && t.IsPublic)
                .Include(t => t.Subjects).ThenInclude(ts => ts.Subject)
                .OrderByDescending(t => t.RatingAvg).ThenByDescending(t => t.RatingCount)
                .Take(4).ToListAsync(),
            Stages = await _db.Stages.AsNoTracking()
                .Include(s => s.Grades).OrderBy(s => s.Order).ToListAsync()
        };
        return View(vm);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Dashboard(string period = "month", DateTime? from = null, DateTime? to = null)
    {
        if (User.IsInRole(Roles.Teacher) && !User.IsStaff()) return RedirectToAction("Index", "Teacher");
        if (User.IsInRole(Roles.Parent) && !User.IsStaff()) return RedirectToAction("Index", "Parent");
        if (User.IsInRole(Roles.Student) && !User.IsStaff()) return RedirectToAction("Index", "Student");

        var today = DateTime.Today;
        DateTime start;
        DateTime end;
        string label;
        switch (period)
        {
            case "year":
                start = new DateTime(today.Year, 1, 1);
                end = today;
                label = _l.T($"Year {today.Year}", $"سنة {today.Year}");
                break;
            case "week":
                start = today.AddDays(-6);
                end = today;
                label = _l.T("Last 7 days", "آخر 7 أيام");
                break;
            case "custom" when from.HasValue && to.HasValue:
                start = from.Value.Date;
                end = to.Value.Date < start ? start : to.Value.Date;
                if ((end - start).TotalDays > 730) start = end.AddDays(-730);
                label = $"{_l.DateShort(start)} — {_l.DateShort(end)}";
                break;
            default:
                period = "month";
                start = new DateTime(today.Year, today.Month, 1);
                end = today;
                label = _l.Month(today);
                break;
        }

        var vm = await _dashboard.BuildAsync(start, end, label, User.CurrentUserId(), _l.IsArabic);
        ViewData["Period"] = period;
        ViewData["From"] = start;
        ViewData["To"] = end;
        ViewData["Title"] = _l.T("Dashboard", "لوحة التحكم");
        return View(vm);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Register(int? courseId = null)
    {
        await LoadRegistrationListsAsync();
        var model = new PublicRegistrationViewModel();
        if (courseId.HasValue)
        {
            var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId.Value && c.Status == CourseStatus.Active);
            if (course is not null)
            {
                model.CourseId = course.Id;
                model.GradeId = course.GradeId;
            }
        }
        return View(model);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(PublicRegistrationViewModel model)
    {
        if (!model.Consent)
            ModelState.AddModelError(nameof(model.Consent), _l.T("Please confirm the accuracy of the information.", "يرجى تأكيد صحة المعلومات المقدمة."));
        var grade = await _db.Grades.Include(g => g.Stage).FirstOrDefaultAsync(g => g.Id == model.GradeId);
        if (grade is null)
            ModelState.AddModelError(nameof(model.GradeId), _l.T("Please choose a valid grade.", "يرجى اختيار صف دراسي صحيح."));
        Course? course = null;
        if (model.CourseId.HasValue)
        {
            course = await _db.Courses.Include(c => c.Sections).Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.Id == model.CourseId.Value && c.Status == CourseStatus.Active);
            if (course is null)
                ModelState.AddModelError(nameof(model.CourseId), _l.T("The selected course is no longer available.", "الدورة المختارة لم تعد متاحة."));
            else if (course.GradeId != model.GradeId)
                ModelState.AddModelError(nameof(model.CourseId), _l.T("The selected course does not match the student's grade.", "الدورة المختارة لا تطابق صف الطالب."));
            else if (course.Enrollments.Count(e => e.Status == EnrollmentStatus.Active) >= course.Capacity)
                ModelState.AddModelError(nameof(model.CourseId), _l.T("The selected course is full.", "الدورة المختارة مكتملة العدد."));
        }

        if (!ModelState.IsValid)
        {
            await LoadRegistrationListsAsync();
            return View(model);
        }

        var phone = model.GuardianPhone.Trim();
        var parent = await _db.Parents.FirstOrDefaultAsync(p => p.Phone == phone);
        if (parent is null)
        {
            parent = new Parent
            {
                FullNameAr = model.GuardianName.Trim(),
                FullNameEn = model.GuardianName.Trim(),
                Phone = phone,
                Email = model.GuardianEmail?.Trim(),
                Address = model.Address.Trim(),
                Area = model.Area.Trim()
            };
            _db.Parents.Add(parent);
            await _db.SaveChangesAsync();
        }

        // Same atomic allocator as the registrar screen: online registrations and staff
        // registrations share one per-year sequence and can never collide.
        var studentNo = await _numbers.AllocateAsync();

        var student = new Student
        {
            StudentNo = studentNo,
            FullNameAr = model.StudentNameAr.Trim(),
            FullNameEn = string.IsNullOrWhiteSpace(model.StudentNameEn) ? model.StudentNameAr.Trim() : model.StudentNameEn.Trim(),
            Gender = model.Gender,
            BirthDate = model.BirthDate.Date,
            Phone = model.StudentPhone?.Trim(),
            SchoolName = model.SchoolName.Trim(),
            GradeId = model.GradeId,
            ParentId = parent.Id,
            Address = model.Address.Trim(),
            Area = model.Area.Trim(),
            Status = StudentStatus.Active,
            QrToken = Guid.NewGuid().ToString("N"),
            RegistrationDate = DateTime.Today,
            Notes = model.Notes?.Trim(),
            BranchId = await _db.Branches.Where(b => b.IsMain).Select(b => (int?)b.Id).FirstOrDefaultAsync()
        };
        _db.Students.Add(student);
        await _db.SaveChangesAsync();

        string? messageAr = null;
        if (course is not null)
        {
            var section = course.Sections.FirstOrDefault(s => s.IsActive);
            var enrollment = new Enrollment
            {
                StudentId = student.Id,
                CourseId = course.Id,
                SectionId = section?.Id,
                EnrollmentDate = DateTime.Today,
                Fee = course.Fee,
                Discount = 0,
                NetFee = course.Fee,
                Status = EnrollmentStatus.Active,
                Notes = _l.T("Registered online", "تسجيل إلكتروني")
            };
            _db.Enrollments.Add(enrollment);
            if (course.Fee > 0)
            {
                _db.Dues.Add(new Due
                {
                    StudentId = student.Id,
                    Enrollment = enrollment,
                    Description = _l.T($"Course fee — {course.NameEn}", $"رسوم دورة — {course.NameAr}"),
                    Amount = course.Fee,
                    PaidAmount = 0,
                    DueDate = DateTime.Today.AddDays(30),
                    Status = DueStatus.Unpaid
                });
            }
            await _db.SaveChangesAsync();
            messageAr = $"تم تسجيل {student.FullNameAr} في دورة {course.NameAr}. القسط المستحق: {_l.Money(course.Fee)}.";
        }

        await _audit.LogAsync("Public registration", "Student", student.Id.ToString(), null, new
        {
            student.StudentNo,
            student.FullNameAr,
            Grade = grade?.NameAr,
            Course = course?.NameAr
        });
        await _notifications.NotifyRoleAsync(Roles.Registrar,
            "طلب تسجيل جديد", "New online registration",
            $"سجّل {student.FullNameAr} ({student.StudentNo}) عبر الموقع" + (course is null ? "." : $" في دورة {course.NameAr}."),
            $"{student.FullNameEn} ({student.StudentNo}) registered online" + (course is null ? "." : $" for {course.NameEn}."),
            $"/Students/Details/{student.Id}", NotificationType.Success);
        await _notifications.NotifyRoleAsync(Roles.Manager,
            "طلب تسجيل جديد", "New online registration",
            $"وصل طلب تسجيل جديد للطالب {student.FullNameAr} ({student.StudentNo}).",
            $"A new registration request was received for {student.FullNameEn} ({student.StudentNo}).",
            $"/Students/Details/{student.Id}", NotificationType.Info);

        TempData["Success"] = _l.T($"Your registration request has been received. Student number: {student.StudentNo}. Our registrar will contact you.",
            $"تم استلام طلب التسجيل. رقم الطالب: {student.StudentNo}. سيتواصل معكم مسؤول التسجيل.");
        TempData["RegisterNote"] = messageAr;
        return RedirectToAction(nameof(RegisterSuccess));
    }

    [AllowAnonymous]
    public IActionResult RegisterSuccess() => View();

    [AllowAnonymous]
    public new IActionResult StatusCode(int code)
    {
        ViewData["Code"] = code;
        ViewData["Title"] = code == 404 ? _l.T("Page not found", "الصفحة غير موجودة") : _l.T("Something went wrong", "حدث خطأ");
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();

    private async Task LoadRegistrationListsAsync()
    {
        ViewBag.Grades = await _db.Grades.AsNoTracking().Include(g => g.Stage).OrderBy(g => g.Stage.Order).ThenBy(g => g.Level).ToListAsync();
        ViewBag.Courses = await _db.Courses.AsNoTracking()
            .Where(c => c.Status == CourseStatus.Active)
            .Include(c => c.Grade).Include(c => c.Subject).Include(c => c.Teacher).Include(c => c.Enrollments)
            .OrderBy(c => c.Grade.Level).ToListAsync();
    }
}
