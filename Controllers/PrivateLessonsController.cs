using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize]
public class PrivateLessonsController : Controller
{
    private const string Managers = Roles.Leadership + "," + Roles.Registrar;
    private const string AdminFinance = Roles.Finance + "," + Roles.Registrar;

    private readonly AppDbContext _db;
    private readonly MatchingService _matching;
    private readonly AccountingService _accounting;
    private readonly NotificationService _notifications;
    private readonly AuditService _audit;

    public PrivateLessonsController(AppDbContext db, MatchingService matching, AccountingService accounting,
        NotificationService notifications, AuditService audit)
    {
        _db = db;
        _matching = matching;
        _accounting = accounting;
        _notifications = notifications;
        _audit = audit;
    }

    [Authorize(Roles = AdminFinance)]
    public async Task<IActionResult> Index(string? q, RequestStatus? status, TeachingMode? mode, int? subjectId)
    {
        var query = _db.PrivateLessonRequests
            .Include(r => r.Student).Include(r => r.Parent)
            .Include(r => r.Grade).ThenInclude(g => g.Stage)
            .Include(r => r.Subject)
            .Include(r => r.PreferredTeacher)
            .Include(r => r.MatchedTeacher)
            .Include(r => r.Lessons)
            .AsNoTracking()
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(r => r.GuardianName.Contains(term) || r.GuardianPhone.Contains(term) ||
                (r.Student != null && r.Student.FullNameAr.Contains(term)));
        }
        if (status.HasValue) query = query.Where(r => r.Status == status.Value);
        if (mode.HasValue) query = query.Where(r => r.Mode == mode.Value);
        if (subjectId.HasValue) query = query.Where(r => r.SubjectId == subjectId.Value);

        var list = await query.OrderBy(r => r.Status == RequestStatus.Pending ? 0 : r.Status == RequestStatus.Matched ? 1 : 2)
            .ThenByDescending(r => r.CreatedAt).Take(200).ToListAsync();

        ViewBag.Q = q;
        ViewBag.Status = status;
        ViewBag.Mode = mode;
        ViewBag.SubjectId = subjectId;
        ViewBag.Subjects = await _db.Subjects.OrderBy(s => s.NameAr).AsNoTracking().ToListAsync();
        ViewBag.Pending = await _db.PrivateLessonRequests.CountAsync(r => r.Status == RequestStatus.Pending);
        ViewBag.Matched = await _db.PrivateLessonRequests.CountAsync(r => r.Status == RequestStatus.Matched || r.Status == RequestStatus.Accepted);
        ViewBag.Completed = await _db.PrivateLessonRequests.CountAsync(r => r.Status == RequestStatus.Completed);
        ViewBag.OpenLessons = await _db.PrivateLessons.CountAsync(l => l.Status == PrivateLessonStatus.Scheduled);
        return View(list);
    }

    [Authorize(Roles = Roles.Staff + "," + Roles.Parent + "," + Roles.Student)]
    public async Task<IActionResult> Details(int id)
    {
        var request = await _db.PrivateLessonRequests
            .Include(r => r.Student).ThenInclude(s => s!.Grade)
            .Include(r => r.Parent)
            .Include(r => r.Grade).ThenInclude(g => g.Stage)
            .Include(r => r.Subject)
            .Include(r => r.PreferredTeacher)
            .Include(r => r.MatchedTeacher)
            .Include(r => r.Lessons).ThenInclude(l => l.Teacher)
            .Include(r => r.Lessons).ThenInclude(l => l.Ratings)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request is null) return NotFound();

        if (User.IsInRole(Roles.Parent) && request.ParentId != User.ParentId() &&
            request.Lessons.All(l => l.ParentId != User.ParentId())) return Forbid();
        if (User.IsInRole(Roles.Student) && request.StudentId != User.StudentId() &&
            request.Lessons.All(l => l.StudentId != User.StudentId())) return Forbid();
        if (User.IsInRole(Roles.Teacher) && !User.IsStaff() &&
            request.MatchedTeacherId != User.TeacherId() && request.Lessons.All(l => l.TeacherId != User.TeacherId()))
            return Forbid();

        ViewBag.CanManage = User.IsInRole(Roles.Administrator) || User.IsInRole(Roles.Manager) || User.IsInRole(Roles.Registrar);
        ViewBag.CanPay = User.IsInRole(Roles.Administrator) || User.IsInRole(Roles.Manager) || User.IsInRole(Roles.Accountant) || User.IsInRole(Roles.Registrar);
        return View(request);
    }

    [Authorize(Roles = Managers)]
    [HttpGet]
    public async Task<IActionResult> Match(int id)
    {
        var request = await LoadRequestAsync(id);
        if (request is null) return NotFound();
        var matches = await _matching.FindAsync(request.GradeId, request.SubjectId, request.Mode, request.Area, request.PreferredTeacherId);
        ViewBag.Request = request;
        return View(matches);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Managers)]
    public async Task<IActionResult> Assign(int id, int teacherId, string? adminNotes)
    {
        var request = await LoadRequestAsync(id);
        if (request is null) return NotFound();
        var teacher = await _db.Teachers.FindAsync(teacherId);
        if (teacher is null) return NotFound();

        request.MatchedTeacherId = teacherId;
        request.Status = RequestStatus.Matched;
        request.AdminNotes = adminNotes;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Private lesson teacher matched", nameof(PrivateLessonRequest), id.ToString(), null,
            new { Teacher = teacher.FullNameAr, request.SubjectId });
        await NotifyRequestGuardianAsync(request,
            "تم إسناد مدرّس لطلبكم",
            "A teacher was assigned to your request",
            $"تم إسناد المدرّس {teacher.FullNameAr} لطلب درس {request.Subject.NameAr}. سنتواصل معكم لتحديد الموعد.",
            $"{teacher.FullNameEn} was assigned to your {request.Subject.NameEn} lesson request. We will contact you to confirm the schedule.",
            $"/PrivateLessons/Details/{id}");
        if (!string.IsNullOrWhiteSpace(teacher.UserId))
        {
            await _notifications.NotifyUserAsync(teacher.UserId,
                "طلب درس خصوصي جديد",
                "New private lesson assignment",
                $"تم إسنادك لطلب درس {request.Subject.NameAr} للطالب {(request.Student?.FullNameAr ?? request.GuardianName)}.",
                $"You were assigned to a {request.Subject.NameEn} request for {(request.Student?.FullNameEn ?? request.GuardianName)}.",
                $"/PrivateLessons/Details/{id}", NotificationType.Info);
        }

        TempData["Success"] = "تم إسناد المدرّس وإشعار ولي الأمر.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = Managers)]
    [HttpGet]
    public async Task<IActionResult> Schedule(int id)
    {
        var request = await LoadRequestAsync(id);
        if (request is null) return NotFound();
        if (request.MatchedTeacherId is null)
        {
            TempData["Warning"] = "يجب إسناد مدرّس قبل جدولة الدرس.";
            return RedirectToAction(nameof(Match), new { id });
        }
        ViewBag.Request = request;
        return View(new ScheduleLessonViewModel
        {
            RequestId = id,
            Date = DateTime.Today.AddDays(1),
            StartTime = request.PreferredTime,
            DurationMinutes = request.DurationMinutes,
            Fee = request.OfferedFee
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Managers)]
    public async Task<IActionResult> Schedule(ScheduleLessonViewModel vm)
    {
        var request = await LoadRequestAsync(vm.RequestId);
        if (request is null) return NotFound();
        if (request.MatchedTeacherId is null)
        {
            TempData["Warning"] = "يجب إسناد مدرّس قبل جدولة الدرس.";
            return RedirectToAction(nameof(Match), new { id = vm.RequestId });
        }
        if (!TimeSpan.TryParse(vm.StartTime, out _))
            ModelState.AddModelError(nameof(vm.StartTime), "صيغة الوقت غير صحيحة.");
        if (vm.Date < DateTime.Today)
            ModelState.AddModelError(nameof(vm.Date), "لا يمكن جدولة درس بتاريخ سابق.");
        if (!ModelState.IsValid)
        {
            ViewBag.Request = request;
            return View(vm);
        }

        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1);
        var lesson = new PrivateLesson
        {
            RequestId = request.Id,
            TeacherId = request.MatchedTeacherId.Value,
            StudentId = request.StudentId,
            ParentId = request.ParentId,
            SubjectId = request.SubjectId,
            GradeId = request.GradeId,
            Mode = request.Mode,
            Area = request.Area,
            Address = request.Address,
            Date = vm.Date.Date,
            StartTime = vm.StartTime,
            DurationMinutes = vm.DurationMinutes,
            Fee = vm.Fee,
            TeacherSharePercent = settings?.PrivateLessonTeacherShare ?? 60m,
            Status = PrivateLessonStatus.Scheduled,
            Notes = vm.Notes,
            CreatedAt = DateTime.UtcNow
        };
        _db.PrivateLessons.Add(lesson);
        request.Status = RequestStatus.Accepted;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Private lesson scheduled", nameof(PrivateLesson), lesson.Id.ToString(), null,
            new { request.Id, lesson.Date, lesson.StartTime, lesson.Fee });
        await NotifyRequestGuardianAsync(request,
            "تم جدولة الدرس الخصوصي",
            "Private lesson scheduled",
            $"تم تحديد موعد درس {request.Subject.NameAr} يوم {vm.Date:yyyy/MM/dd} الساعة {vm.StartTime}.",
            $"Your {request.Subject.NameEn} lesson is scheduled on {vm.Date:yyyy/MM/dd} at {vm.StartTime}.",
            $"/PrivateLessons/Details/{request.Id}", NotificationType.Success);

        TempData["Success"] = "تم جدولة الدرس وإشعار ولي الأمر.";
        return RedirectToAction(nameof(Details), new { id = request.Id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Managers)]
    public async Task<IActionResult> Complete(int id)
    {
        var lesson = await _db.PrivateLessons.Include(l => l.Request).Include(l => l.Student).Include(l => l.Subject)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (lesson is null) return NotFound();
        if (lesson.Status != PrivateLessonStatus.Scheduled)
        {
            TempData["Warning"] = "لا يمكن إكمال درس ليس مجدولاً.";
            return RedirectToAction(nameof(Details), new { id = lesson.RequestId });
        }
        lesson.Status = PrivateLessonStatus.Completed;
        if (lesson.Request.Lessons.All(l => l.Status is PrivateLessonStatus.Completed or PrivateLessonStatus.Cancelled or PrivateLessonStatus.NoShow))
            lesson.Request.Status = RequestStatus.Completed;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Private lesson completed", nameof(PrivateLesson), id.ToString());
        if (lesson.Student is not null)
        {
            await _notifications.NotifyGuardianAsync(lesson.Student,
                "اكتمل الدرس الخصوصي",
                "Private lesson completed",
                $"اكتمل درس {lesson.Subject.NameAr}. يمكنكم تقييم المدرّس من صفحة الدرس.",
                $"The {lesson.Subject.NameEn} lesson is complete. You can rate the teacher from the lesson page.",
                $"/PrivateLessons/Details/{lesson.RequestId}", NotificationType.Success);
        }
        TempData["Success"] = "تم تعليم الدرس كمكتمل.";
        return RedirectToAction(nameof(Details), new { id = lesson.RequestId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Managers)]
    public async Task<IActionResult> CancelLesson(int id, string? reason)
    {
        var lesson = await _db.PrivateLessons.Include(l => l.Request).FirstOrDefaultAsync(l => l.Id == id);
        if (lesson is null) return NotFound();
        if (lesson.IsPaid)
        {
            TempData["Error"] = "لا يمكن إلغاء درس مدفوع. يلزم معالجة استرداد من قسم المالية.";
            return RedirectToAction(nameof(Details), new { id = lesson.RequestId });
        }
        if (lesson.Status == PrivateLessonStatus.Completed || lesson.Status == PrivateLessonStatus.Cancelled)
        {
            TempData["Warning"] = "حالة الدرس لا تسمح بالإلغاء.";
            return RedirectToAction(nameof(Details), new { id = lesson.RequestId });
        }
        lesson.Status = PrivateLessonStatus.Cancelled;
        lesson.Notes = string.IsNullOrWhiteSpace(reason) ? lesson.Notes : reason;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Private lesson cancelled", nameof(PrivateLesson), id.ToString(), null, new { reason });
        TempData["Success"] = "تم إلغاء الدرس.";
        return RedirectToAction(nameof(Details), new { id = lesson.RequestId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = AdminFinance)]
    public async Task<IActionResult> Pay(int id, PaymentMethod method)
    {
        var lesson = await _db.PrivateLessons
            .Include(l => l.Teacher).Include(l => l.Subject)
            .Include(l => l.Student).Include(l => l.Request)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (lesson is null) return NotFound();
        if (lesson.IsPaid)
        {
            TempData["Warning"] = "تم قبض هذا الدرس مسبقاً.";
            return RedirectToAction(nameof(Details), new { id = lesson.RequestId });
        }
        if (lesson.Fee <= 0)
        {
            TempData["Error"] = "قيمة الدرس غير صحيحة.";
            return RedirectToAction(nameof(Details), new { id = lesson.RequestId });
        }

        var fund = await _accounting.GetFundAsync(method);
        var share = (long)Math.Round(lesson.Fee * lesson.TeacherSharePercent / 100m);
        var earning = new TeacherEarning
        {
            TeacherId = lesson.TeacherId,
            PrivateLessonId = lesson.Id,
            Kind = TeacherEarningKind.PrivateLesson,
            BaseAmount = lesson.Fee,
            SharePercent = lesson.TeacherSharePercent,
            Amount = share,
            Date = DateTime.Today
        };
        _db.TeacherEarnings.Add(earning);
        lesson.IsPaid = true;
        lesson.PaidDate = DateTime.Today;

        _accounting.ApplyInflow(fund, lesson.Fee);
        var revenueCode = lesson.Mode == TeachingMode.Home ? CoA.HomeLessons : CoA.PrivateCommission;
        await _accounting.PostAsync(DateTime.Today,
            $"درس خصوصي - {lesson.Subject.NameAr} - {lesson.Teacher.FullNameAr}", "PrivateLesson", lesson.Id.ToString(),
            new[]
            {
                new JournalDraft(fund.Type == FundType.Cash ? CoA.Cash : CoA.Bank, lesson.Fee, 0),
                new JournalDraft(revenueCode, 0, lesson.Fee),
                new JournalDraft(CoA.TeacherShare, share, 0),
                new JournalDraft(CoA.TeacherPayable, 0, share)
            }, User.DisplayName(), User.CurrentUserId());
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Private lesson paid", nameof(PrivateLesson), id.ToString(), null,
            new { lesson.Fee, Share = share });
        if (lesson.Student is not null)
        {
            await _notifications.NotifyGuardianAsync(lesson.Student,
                "تم قبض رسوم الدرس",
                "Lesson fee received",
                $"تم قبض {lesson.Fee:N0} د.ع لدرس {lesson.Subject.NameAr}. شكراً لكم.",
                $"We received {lesson.Fee:N0} IQD for the {lesson.Subject.NameEn} lesson. Thank you.",
                $"/PrivateLessons/Details/{lesson.RequestId}", NotificationType.Success);
        }

        TempData["Success"] = "تم قبض رسوم الدرس واحتساب حصة المدرّس.";
        return RedirectToAction(nameof(Details), new { id = lesson.RequestId });
    }

    [Authorize(Roles = Roles.Parent + "," + Roles.Student + "," + Managers)]
    [HttpGet]
    public new async Task<IActionResult> Request(int? studentId)
    {
        await LoadRequestListsAsync();
        var vm = new PrivateLessonRequestViewModel { StudentId = studentId };
        if (User.IsInRole(Roles.Parent) && User.ParentId() is int parentId)
        {
            var parent = await _db.Parents.FindAsync(parentId);
            if (parent is not null)
            {
                vm.GuardianName = parent.FullNameAr;
                vm.GuardianPhone = parent.Phone;
                vm.Area = parent.Area;
                vm.Address = parent.Address;
            }
            var firstChild = await _db.Students.Where(s => s.ParentId == parentId).OrderBy(s => s.Id).FirstOrDefaultAsync();
            if (firstChild is not null)
            {
                vm.StudentId ??= firstChild.Id;
                vm.GradeId = firstChild.GradeId;
                vm.Area = string.IsNullOrWhiteSpace(firstChild.Area) ? vm.Area : firstChild.Area;
            }
        }
        else if (User.IsInRole(Roles.Student) && User.StudentId() is int sid)
        {
            var student = await _db.Students.Include(s => s.Parent).FirstOrDefaultAsync(s => s.Id == sid);
            if (student is not null)
            {
                vm.StudentId = student.Id;
                vm.GradeId = student.GradeId;
                vm.Area = student.Area;
                vm.Address = student.Address;
                if (student.Parent is not null)
                {
                    vm.GuardianName = student.Parent.FullNameAr;
                    vm.GuardianPhone = student.Parent.Phone;
                    if (string.IsNullOrWhiteSpace(vm.Address)) vm.Address = student.Parent.Address;
                }
            }
        }
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Parent + "," + Roles.Student + "," + Managers)]
    public new async Task<IActionResult> Request(PrivateLessonRequestViewModel vm)
    {
        await LoadRequestListsAsync();
        var grade = await _db.Grades.Include(g => g.Stage).FirstOrDefaultAsync(g => g.Id == vm.GradeId);
        var subject = await _db.Subjects.FindAsync(vm.SubjectId);
        if (grade is null) ModelState.AddModelError(nameof(vm.GradeId), "يرجى اختيار صف صحيح.");
        if (subject is null) ModelState.AddModelError(nameof(vm.SubjectId), "يرجى اختيار مادة صحيحة.");

        Student? student = null;
        Parent? parent = null;
        if (User.IsInRole(Roles.Parent) && User.ParentId() is int parentId)
        {
            parent = await _db.Parents.FindAsync(parentId);
            if (vm.StudentId.HasValue)
            {
                student = await _db.Students.FirstOrDefaultAsync(s => s.Id == vm.StudentId.Value && s.ParentId == parentId);
                if (student is null) ModelState.AddModelError(nameof(vm.StudentId), "الطالب المحدد لا يتبع لحسابكم.");
            }
        }
        else if (User.IsInRole(Roles.Student) && User.StudentId() is int studentId)
        {
            student = await _db.Students.FindAsync(studentId);
            vm.StudentId = studentId;
            if (student is not null) parent = student.ParentId.HasValue ? await _db.Parents.FindAsync(student.ParentId.Value) : null;
        }
        else if (vm.StudentId.HasValue)
        {
            student = await _db.Students.FindAsync(vm.StudentId.Value);
            if (student is not null) parent = student.ParentId.HasValue ? await _db.Parents.FindAsync(student.ParentId.Value) : null;
        }

        if (!ModelState.IsValid)
            return View(vm);

        var request = new PrivateLessonRequest
        {
            StudentId = student?.Id,
            ParentId = parent?.Id,
            GuardianName = vm.GuardianName.Trim(),
            GuardianPhone = vm.GuardianPhone.Trim(),
            GradeId = vm.GradeId,
            SubjectId = vm.SubjectId,
            PreferredTeacherId = vm.PreferredTeacherId,
            Mode = vm.Mode,
            Area = vm.Area.Trim(),
            Address = vm.Address,
            DaysPerWeek = vm.DaysPerWeek,
            PreferredTime = vm.PreferredTime,
            DurationMinutes = vm.DurationMinutes,
            OfferedFee = vm.OfferedFee,
            Notes = vm.Notes,
            Status = RequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _db.PrivateLessonRequests.Add(request);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Private lesson requested", nameof(PrivateLessonRequest), request.Id.ToString(), null,
            new { Student = student?.FullNameAr, Grade = grade!.NameAr, Subject = subject!.NameAr, request.Mode });
        await _notifications.NotifyRoleAsync(Roles.Manager,
            "طلب درس خصوصي جديد",
            "New private lesson request",
            $"طلب جديد لدرس {subject!.NameAr} - {grade!.NameAr} من {request.GuardianName}.",
            $"New {subject.NameEn} request for {grade.NameEn} from {request.GuardianName}.",
            "/PrivateLessons", NotificationType.Info);

        TempData["Success"] = "تم إرسال الطلب. ستتواصل الإدارة معكم لإسناد المدرّس.";
        return RedirectToAction(nameof(Details), new { id = request.Id });
    }

    [Authorize(Roles = Roles.Parent + "," + Roles.Student + "," + Managers)]
    [HttpGet]
    public async Task<IActionResult> Rate(int id)
    {
        var lesson = await _db.PrivateLessons.Include(l => l.Teacher).Include(l => l.Subject)
            .Include(l => l.Ratings).FirstOrDefaultAsync(l => l.Id == id);
        if (lesson is null) return NotFound();
        if (lesson.Status != PrivateLessonStatus.Completed)
        {
            TempData["Warning"] = "يمكن تقييم المدرّس بعد اكتمال الدرس فقط.";
            return RedirectToAction(nameof(Details), new { id = lesson.RequestId });
        }
        if (lesson.Ratings.Any(r => r.RatedByUserId == User.CurrentUserId()))
        {
            TempData["Warning"] = "لقد قيّمتم هذا الدرس مسبقاً.";
            return RedirectToAction(nameof(Details), new { id = lesson.RequestId });
        }
        ViewBag.Lesson = lesson;
        return View(new RateTeacherViewModel { TeacherId = lesson.TeacherId, LessonId = lesson.Id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Parent + "," + Roles.Student + "," + Managers)]
    public async Task<IActionResult> Rate(RateTeacherViewModel vm)
    {
        var lesson = await _db.PrivateLessons.Include(l => l.Teacher).Include(l => l.Ratings)
            .FirstOrDefaultAsync(l => l.Id == vm.LessonId);
        if (lesson is null) return NotFound();
        if (lesson.Status != PrivateLessonStatus.Completed)
        {
            TempData["Warning"] = "يمكن تقييم المدرّس بعد اكتمال الدرس فقط.";
            return RedirectToAction(nameof(Details), new { id = lesson.RequestId });
        }
        if (lesson.Ratings.Any(r => r.RatedByUserId == User.CurrentUserId()))
        {
            TempData["Warning"] = "لقد قيّمتم هذا الدرس مسبقاً.";
            return RedirectToAction(nameof(Details), new { id = lesson.RequestId });
        }

        var rating = new TeacherRating
        {
            TeacherId = lesson.TeacherId,
            ParentId = lesson.ParentId ?? User.ParentId(),
            LessonId = lesson.Id,
            RatedByUserId = User.CurrentUserId(),
            RatedByName = User.DisplayName(),
            Explanation = vm.Explanation,
            Punctuality = vm.Punctuality,
            Interaction = vm.Interaction,
            FollowUp = vm.FollowUp,
            Overall = vm.Overall,
            Comment = vm.Comment,
            CreatedAt = DateTime.UtcNow
        };
        _db.TeacherRatings.Add(rating);
        await _db.SaveChangesAsync();

        var ratings = await _db.TeacherRatings.Where(r => r.TeacherId == lesson.TeacherId).ToListAsync();
        lesson.Teacher.RatingCount = ratings.Count;
        lesson.Teacher.RatingAvg = ratings.Count == 0 ? 0 : Math.Round(ratings.Average(r => (double)r.Average), 1);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Teacher rated", nameof(TeacherRating), rating.Id.ToString(), null,
            new { lesson.Teacher.FullNameAr, rating.Overall });
        TempData["Success"] = "شكراً لتقييمكم.";
        return RedirectToAction(nameof(Details), new { id = lesson.RequestId });
    }

    private async Task<PrivateLessonRequest?> LoadRequestAsync(int id) =>
        await _db.PrivateLessonRequests
            .Include(r => r.Student).Include(r => r.Parent)
            .Include(r => r.Grade).ThenInclude(g => g.Stage)
            .Include(r => r.Subject)
            .Include(r => r.MatchedTeacher)
            .Include(r => r.PreferredTeacher)
            .Include(r => r.Lessons)
            .FirstOrDefaultAsync(r => r.Id == id);

    private async Task NotifyRequestGuardianAsync(PrivateLessonRequest request, string titleAr, string titleEn,
        string bodyAr, string bodyEn, string? url = null, NotificationType type = NotificationType.Info)
    {
        if (request.Student is not null)
        {
            await _notifications.NotifyGuardianAsync(request.Student, titleAr, titleEn, bodyAr, bodyEn, url, type);
            return;
        }
        if (request.Parent?.UserId is string parentUser)
        {
            await _notifications.NotifyUserAsync(parentUser, titleAr, titleEn, bodyAr, bodyEn, url, type);
        }
    }

    private async Task LoadRequestListsAsync()
    {
        ViewBag.Grades = await _db.Grades.Include(g => g.Stage).OrderBy(g => g.Stage.Order).ThenBy(g => g.Level).AsNoTracking().ToListAsync();
        ViewBag.Subjects = await _db.Subjects.Where(s => s.IsActive).OrderBy(s => s.NameAr).AsNoTracking().ToListAsync();
        ViewBag.Teachers = await _db.Teachers.Where(t => t.IsActive).OrderBy(t => t.FullNameAr).AsNoTracking().ToListAsync();
        var allowedStudents = User.IsInRole(Roles.Parent) && User.ParentId() is int parentId
            ? await _db.Students.Where(s => s.ParentId == parentId).OrderBy(s => s.FullNameAr).AsNoTracking().ToListAsync()
            : User.IsInRole(Roles.Student) && User.StudentId() is int studentId
                ? await _db.Students.Where(s => s.Id == studentId).AsNoTracking().ToListAsync()
                : await _db.Students.OrderBy(s => s.FullNameAr).AsNoTracking().ToListAsync();
        ViewBag.Students = allowedStudents;
    }
}
