using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Staff)]
public class AdminController : Controller
{
    private static readonly string[] AssignableRoles =
    {
        Roles.Administrator, Roles.Manager, Roles.Accountant, Roles.Hr,
        Roles.Registrar, Roles.Teacher, Roles.Parent, Roles.Student
    };

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly AuditService _audit;
    private readonly Localizer _l;
    private readonly IWebHostEnvironment _env;

    public AdminController(AppDbContext db, UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn, AuditService audit, Localizer localizer,
        IWebHostEnvironment env)
    {
        _db = db;
        _users = users;
        _signIn = signIn;
        _audit = audit;
        _l = localizer;
        _env = env;
    }

    private string BackupDirectory => Path.Combine(_env.ContentRootPath, "App_Data", "backups");

    // ───────────────────────── Settings ─────────────────────────

    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Settings()
    {
        var settings = await _db.Settings.AsNoTracking().SingleAsync(s => s.Id == 1);
        var vm = MapSettings(settings);
        vm.Branches = await _db.Branches.AsNoTracking()
            .OrderByDescending(b => b.IsMain).ThenBy(b => b.Code).ToListAsync();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Settings(AdminSettingsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Branches = await _db.Branches.AsNoTracking()
                .OrderByDescending(b => b.IsMain).ThenBy(b => b.Code).ToListAsync();
            return View(model);
        }

        var settings = await _db.Settings.SingleAsync(s => s.Id == 1);
        var before = Snapshot(settings);

        settings.NameAr = model.NameAr.Trim();
        settings.NameEn = model.NameEn.Trim();
        settings.LicenseNo = model.LicenseNo.Trim();
        settings.MinistryCode = model.MinistryCode.Trim();
        settings.AddressAr = model.AddressAr.Trim();
        settings.AddressEn = model.AddressEn.Trim();
        settings.Phone = model.Phone.Trim();
        settings.Email = model.Email.Trim();
        settings.PrincipalName = model.PrincipalName.Trim();
        settings.AcademicYear = model.AcademicYear.Trim();
        settings.MottoAr = model.MottoAr.Trim();
        settings.MottoEn = model.MottoEn.Trim();
        settings.CurrencyAr = model.CurrencyAr.Trim();
        settings.CurrencyEn = model.CurrencyEn.Trim();
        settings.TuitionEnabled = model.TuitionEnabled;
        settings.DefaultTeacherShare = model.DefaultTeacherShare;
        settings.PrivateLessonTeacherShare = model.PrivateLessonTeacherShare;
        settings.PrepTeacherMonthlyRent = model.PrepTeacherMonthlyRent;
        settings.CafeteriaAnnualRent = model.CafeteriaAnnualRent;
        settings.PassMark = model.PassMark;
        settings.AttendanceWarningPercent = model.AttendanceWarningPercent;
        settings.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Update settings", nameof(InstituteSettings), "1", before, Snapshot(settings));
        TempData["Success"] = _l.T("Institute settings have been saved.", "تم حفظ إعدادات المعهد بنجاح.");
        return RedirectToAction(nameof(Settings));
    }

    private static AdminSettingsViewModel MapSettings(InstituteSettings s) => new()
    {
        NameAr = s.NameAr,
        NameEn = s.NameEn,
        LicenseNo = s.LicenseNo,
        MinistryCode = s.MinistryCode,
        AddressAr = s.AddressAr,
        AddressEn = s.AddressEn,
        Phone = s.Phone,
        Email = s.Email,
        PrincipalName = s.PrincipalName,
        AcademicYear = s.AcademicYear,
        MottoAr = s.MottoAr,
        MottoEn = s.MottoEn,
        CurrencyAr = s.CurrencyAr,
        CurrencyEn = s.CurrencyEn,
        TuitionEnabled = s.TuitionEnabled,
        DefaultTeacherShare = s.DefaultTeacherShare,
        PrivateLessonTeacherShare = s.PrivateLessonTeacherShare,
        PrepTeacherMonthlyRent = s.PrepTeacherMonthlyRent,
        CafeteriaAnnualRent = s.CafeteriaAnnualRent,
        PassMark = s.PassMark,
        AttendanceWarningPercent = s.AttendanceWarningPercent,
        UpdatedAt = s.UpdatedAt
    };

    private static object Snapshot(InstituteSettings s) => new
    {
        s.NameAr, s.NameEn, s.LicenseNo, s.MinistryCode, s.AddressAr, s.AddressEn,
        s.Phone, s.Email, s.PrincipalName, s.AcademicYear, s.MottoAr, s.MottoEn,
        s.CurrencyAr, s.CurrencyEn, s.TuitionEnabled, s.DefaultTeacherShare,
        s.PrivateLessonTeacherShare, s.PrepTeacherMonthlyRent, s.CafeteriaAnnualRent,
        s.PassMark, s.AttendanceWarningPercent
    };

    // ───────────────────────── Users & roles ─────────────────────────

    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> Users(string? q, string? role, string? status)
    {
        var allUsers = await _db.Users.AsNoTracking().OrderByDescending(u => u.CreatedAt).ToListAsync();
        var roleRows = await (from ur in _db.UserRoles
                              join r in _db.Roles on ur.RoleId equals r.Id
                              select new { ur.UserId, Name = r.Name }).ToListAsync();
        var roleMap = roleRows.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.Select(x => x.Name ?? "").ToList());
        var lastLogins = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == "Login" && a.EntityId != null)
            .GroupBy(a => a.EntityId!)
            .Select(g => new { UserId = g.Key, At = g.Max(x => x.CreatedAt) })
            .ToDictionaryAsync(x => x.UserId, x => x.At);

        var teacherNames = await _db.Teachers.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.FullNameAr);
        var parentNames = await _db.Parents.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.FullNameAr);
        var studentNames = await _db.Students.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.FullNameAr);

        var rows = allUsers.Select(u =>
        {
            var roles = roleMap.TryGetValue(u.Id, out var rs) ? rs : new List<string>();
            var linked = u.TeacherId.HasValue && teacherNames.TryGetValue(u.TeacherId.Value, out var tn) ? tn
                : u.ParentId.HasValue && parentNames.TryGetValue(u.ParentId.Value, out var pn) ? pn
                : u.StudentId.HasValue && studentNames.TryGetValue(u.StudentId.Value, out var sn) ? sn
                : "";
            return new UserListRow
            {
                User = u,
                Roles = roles,
                LinkedTo = linked,
                LastLogin = lastLogins.TryGetValue(u.Id, out var at) ? at : null
            };
        }).ToList();

        var filtered = rows.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            filtered = filtered.Where(r => r.User.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                           (r.User.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                           r.LinkedTo.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(role)) filtered = filtered.Where(r => r.Roles.Contains(role));
        if (status == "active") filtered = filtered.Where(r => r.User.IsActive);
        else if (status == "inactive") filtered = filtered.Where(r => !r.User.IsActive);

        var vm = new UsersIndexViewModel
        {
            Users = filtered.ToList(),
            Total = rows.Count,
            Active = rows.Count(r => r.User.IsActive),
            Staff = rows.Count(r => r.Roles.Any(x => x is Roles.Administrator or Roles.Manager or Roles.Accountant or Roles.Hr or Roles.Registrar)),
            Portal = rows.Count(r => r.Roles.Any(x => x is Roles.Teacher or Roles.Parent or Roles.Student)),
            Q = q,
            Role = role,
            Status = status,
            RoleOptions = AssignableRoles.ToList()
        };
        return View(vm);
    }

    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> CreateUser()
    {
        await LoadLinkOptionsAsync();
        ViewBag.RoleOptions = AssignableRoles;
        return View("UserForm", new AdminUserFormViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> CreateUser(AdminUserFormViewModel model)
    {
        await LoadLinkOptionsAsync();
        ViewBag.RoleOptions = AssignableRoles;

        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), _l.T("A password is required for a new account.", "كلمة المرور مطلوبة للحساب الجديد."));
        if (!AssignableRoles.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), _l.T("Choose a valid role.", "اختر صلاحية صحيحة."));
        if (!ModelState.IsValid) return View("UserForm", model);

        var email = model.Email.Trim();
        if (await _users.FindByEmailAsync(email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), _l.T("This email is already registered.", "هذا البريد مسجل مسبقاً."));
            return View("UserForm", model);
        }
        var linkError = await ValidateLinkAsync(model, null);
        if (linkError is not null)
        {
            ModelState.AddModelError(string.Empty, linkError);
            return View("UserForm", model);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = model.FullName.Trim(),
            IsActive = true,
            PreferredLanguage = model.PreferredLanguage == "en" ? "en" : "ar",
            TeacherId = model.Role == Roles.Teacher ? model.TeacherId : null,
            ParentId = model.Role == Roles.Parent ? model.ParentId : null,
            StudentId = model.Role == Roles.Student ? model.StudentId : null,
            CreatedAt = DateTime.UtcNow
        };
        var result = await _users.CreateAsync(user, model.Password!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View("UserForm", model);
        }
        var roleResult = await _users.AddToRoleAsync(user, model.Role);
        if (!roleResult.Succeeded)
        {
            await _users.DeleteAsync(user);
            foreach (var error in roleResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View("UserForm", model);
        }

        await PersistLinkAsync(user);
        await _audit.LogAsync("Create user", nameof(ApplicationUser), user.Id, null,
            new { user.Email, user.FullName, Role = model.Role, user.IsActive });
        TempData["Success"] = _l.T($"Account {email} was created.", $"تم إنشاء الحساب {email}.");
        return RedirectToAction(nameof(Users));
    }

    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> EditUser(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        await LoadLinkOptionsAsync();
        ViewBag.RoleOptions = AssignableRoles;
        var roles = await _users.GetRolesAsync(user);
        return View("UserForm", new AdminUserFormViewModel
        {
            Id = user.Id,
            Email = user.Email ?? "",
            FullName = user.FullName,
            Role = roles.FirstOrDefault() ?? Roles.Registrar,
            IsActive = user.IsActive,
            PreferredLanguage = user.PreferredLanguage,
            TeacherId = user.TeacherId,
            ParentId = user.ParentId,
            StudentId = user.StudentId
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> EditUser(AdminUserFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Id)) return NotFound();
        var user = await _users.FindByIdAsync(model.Id);
        if (user is null) return NotFound();
        await LoadLinkOptionsAsync();
        ViewBag.RoleOptions = AssignableRoles;

        var isSelf = user.Id == User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (isSelf && !model.IsActive)
            ModelState.AddModelError(string.Empty, _l.T("You cannot disable your own account.", "لا يمكنك تعطيل حسابك الحالي."));
        if (isSelf && model.Role != Roles.Administrator)
            ModelState.AddModelError(nameof(model.Role), _l.T("You cannot remove your own administrator role.", "لا يمكنك إزالة صلاحية مدير النظام عن حسابك."));
        if (!AssignableRoles.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), _l.T("Choose a valid role.", "اختر صلاحية صحيحة."));
        if (!ModelState.IsValid) return View("UserForm", model);

        var email = model.Email.Trim();
        var owner = await _users.FindByEmailAsync(email);
        if (owner is not null && owner.Id != user.Id)
        {
            ModelState.AddModelError(nameof(model.Email), _l.T("This email is used by another account.", "هذا البريد مستخدم من حساب آخر."));
            return View("UserForm", model);
        }
        var linkError = await ValidateLinkAsync(model, user.Id);
        if (linkError is not null)
        {
            ModelState.AddModelError(string.Empty, linkError);
            return View("UserForm", model);
        }

        var before = new { user.Email, user.FullName, user.IsActive, user.PreferredLanguage, user.TeacherId, user.ParentId, user.StudentId };
        user.Email = email;
        user.UserName = email;
        user.FullName = model.FullName.Trim();
        user.IsActive = model.IsActive;
        user.PreferredLanguage = model.PreferredLanguage == "en" ? "en" : "ar";

        await ReleaseLinkAsync(user);
        user.TeacherId = model.Role == Roles.Teacher ? model.TeacherId : null;
        user.ParentId = model.Role == Roles.Parent ? model.ParentId : null;
        user.StudentId = model.Role == Roles.Student ? model.StudentId : null;

        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View("UserForm", model);
        }

        var roles = await _users.GetRolesAsync(user);
        if (!roles.Contains(model.Role))
        {
            if (roles.Count > 0) await _users.RemoveFromRolesAsync(user, roles);
            await _users.AddToRoleAsync(user, model.Role);
        }
        await PersistLinkAsync(user);
        if (isSelf) await _signIn.RefreshSignInAsync(user);

        await _audit.LogAsync("Update user", nameof(ApplicationUser), user.Id, before,
            new { user.Email, user.FullName, user.IsActive, user.PreferredLanguage, Role = model.Role, user.TeacherId, user.ParentId, user.StudentId });
        TempData["Success"] = _l.T("Account details were updated.", "تم تحديث بيانات الحساب.");
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> ToggleUser(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (user.Id == User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value)
        {
            TempData["Error"] = _l.T("You cannot disable your own account.", "لا يمكنك تعطيل حسابك الحالي.");
            return RedirectToAction(nameof(Users));
        }

        user.IsActive = !user.IsActive;
        await _users.UpdateAsync(user);
        if (!user.IsActive) await _users.UpdateSecurityStampAsync(user);
        await _audit.LogAsync(user.IsActive ? "Activate user" : "Deactivate user", nameof(ApplicationUser), user.Id,
            null, new { user.Email, user.IsActive });
        TempData["Success"] = user.IsActive
            ? _l.T("Account activated.", "تم تفعيل الحساب.")
            : _l.T("Account disabled. Active sessions will be signed out.", "تم تعطيل الحساب، وستُغلق الجلسات النشطة.");
        return RedirectToAction(nameof(Users));
    }

    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        return View(new AdminResetPasswordViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? ""
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> ResetPassword(AdminResetPasswordViewModel model)
    {
        var user = await _users.FindByIdAsync(model.Id);
        if (user is null) return NotFound();
        if (!ModelState.IsValid)
        {
            model.FullName = user.FullName;
            model.Email = user.Email ?? "";
            return View(model);
        }

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            model.FullName = user.FullName;
            model.Email = user.Email ?? "";
            return View(model);
        }
        await _users.UpdateSecurityStampAsync(user);
        await _audit.LogAsync("Reset password", nameof(ApplicationUser), user.Id);
        TempData["Success"] = _l.T("Password reset. The user must sign in again.", "تمت إعادة تعيين كلمة المرور، وعلى المستخدم تسجيل الدخول من جديد.");
        return RedirectToAction(nameof(Users));
    }

    private async Task LoadLinkOptionsAsync()
    {
        ViewBag.Teachers = (await _db.Teachers.AsNoTracking().Where(t => t.IsActive)
                .OrderBy(t => t.FullNameAr).Select(t => new { t.Id, Name = t.FullNameAr }).ToListAsync())
            .Select(t => new LinkOption(t.Id, t.Name)).ToList();
        ViewBag.Parents = (await _db.Parents.AsNoTracking()
                .OrderBy(p => p.FullNameAr).Select(p => new { p.Id, Name = p.FullNameAr }).ToListAsync())
            .Select(p => new LinkOption(p.Id, p.Name)).ToList();
        ViewBag.Students = (await _db.Students.AsNoTracking().Where(s => s.Status == StudentStatus.Active)
                .OrderBy(s => s.FullNameAr).Select(s => new { s.Id, Name = s.FullNameAr }).ToListAsync())
            .Select(s => new LinkOption(s.Id, s.Name)).ToList();
    }

    private async Task<string?> ValidateLinkAsync(AdminUserFormViewModel model, string? currentUserId)
    {
        if (model.Role == Roles.Teacher && model.TeacherId.HasValue)
        {
            var teacher = await _db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == model.TeacherId.Value);
            if (teacher is null) return _l.T("The selected teacher does not exist.", "المدرّس المحدد غير موجود.");
            if (!string.IsNullOrEmpty(teacher.UserId) && teacher.UserId != currentUserId)
                return _l.T("This teacher is already linked to another account.", "هذا المدرّس مرتبط بحساب آخر.");
        }
        if (model.Role == Roles.Parent && model.ParentId.HasValue)
        {
            var parent = await _db.Parents.AsNoTracking().FirstOrDefaultAsync(p => p.Id == model.ParentId.Value);
            if (parent is null) return _l.T("The selected guardian does not exist.", "ولي الأمر المحدد غير موجود.");
            if (!string.IsNullOrEmpty(parent.UserId) && parent.UserId != currentUserId)
                return _l.T("This guardian is already linked to another account.", "ولي الأمر هذا مرتبط بحساب آخر.");
        }
        if (model.Role == Roles.Student && model.StudentId.HasValue)
        {
            var student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == model.StudentId.Value);
            if (student is null) return _l.T("The selected student does not exist.", "الطالب المحدد غير موجود.");
            if (!string.IsNullOrEmpty(student.UserId) && student.UserId != currentUserId)
                return _l.T("This student is already linked to another account.", "هذا الطالب مرتبط بحساب آخر.");
        }
        return null;
    }

    private async Task ReleaseLinkAsync(ApplicationUser user)
    {
        if (user.TeacherId.HasValue)
        {
            var teacher = await _db.Teachers.FindAsync(user.TeacherId.Value);
            if (teacher is not null && teacher.UserId == user.Id) teacher.UserId = null;
        }
        if (user.ParentId.HasValue)
        {
            var parent = await _db.Parents.FindAsync(user.ParentId.Value);
            if (parent is not null && parent.UserId == user.Id) parent.UserId = null;
        }
        if (user.StudentId.HasValue)
        {
            var student = await _db.Students.FindAsync(user.StudentId.Value);
            if (student is not null && student.UserId == user.Id) student.UserId = null;
        }
        await _db.SaveChangesAsync();
    }

    private async Task PersistLinkAsync(ApplicationUser user)
    {
        if (user.TeacherId.HasValue)
        {
            var teacher = await _db.Teachers.FindAsync(user.TeacherId.Value);
            if (teacher is not null) teacher.UserId = user.Id;
        }
        if (user.ParentId.HasValue)
        {
            var parent = await _db.Parents.FindAsync(user.ParentId.Value);
            if (parent is not null) parent.UserId = user.Id;
        }
        if (user.StudentId.HasValue)
        {
            var student = await _db.Students.FindAsync(user.StudentId.Value);
            if (student is not null) student.UserId = user.Id;
        }
        await _db.SaveChangesAsync();
    }

    // ───────────────────────── Audit trail ─────────────────────────

    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> Audit(string? q, string? action, string? entity,
        DateTime? from, DateTime? to, int page = 1)
    {
        var query = BuildAuditQuery(q, action, entity, from, to);
        var total = await query.CountAsync();
        var vm = new AuditIndexViewModel
        {
            Q = q,
            Action = action,
            Entity = entity,
            From = from,
            To = to,
            Total = total,
            Today = await query.CountAsync(a => a.CreatedAt >= DateTime.UtcNow.Date),
            Users = await query.Where(a => a.UserId != null).Select(a => a.UserId).Distinct().CountAsync(),
            PageSize = 30,
            Actions = await _db.AuditLogs.AsNoTracking().Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(),
            Entities = await _db.AuditLogs.AsNoTracking().Select(a => a.EntityName).Distinct().OrderBy(a => a).ToListAsync()
        };
        vm.Page = Math.Clamp(page, 1, vm.Pages);
        vm.Entries = await query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip((vm.Page - 1) * vm.PageSize).Take(vm.PageSize).ToListAsync();
        return View(vm);
    }

    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> ExportAudit(string? q, string? action, string? entity,
        DateTime? from, DateTime? to)
    {
        var rows = await BuildAuditQuery(q, action, entity, from, to)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).Take(5000).ToListAsync();

        static string Csv(string? value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', new[] { "Time (UTC)", "User", "Action", "Entity", "Entity ID", "IP", "New values", "Old values" }.Select(Csv)));
        foreach (var a in rows)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                Csv(a.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")), Csv(a.UserName), Csv(a.Action),
                Csv(a.EntityName), Csv(a.EntityId), Csv(a.IpAddress), Csv(a.NewValues), Csv(a.OldValues)
            }));
        }
        await _audit.LogAsync("Export audit trail", nameof(AuditLog), null, null, new { Rows = rows.Count });
        var bytes = new UTF8Encoding(true).GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"audit-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    private IQueryable<AuditLog> BuildAuditQuery(string? q, string? action, string? entity,
        DateTime? from, DateTime? to)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(a => a.UserName.Contains(term) || a.Action.Contains(term) ||
                                     a.EntityName.Contains(term) || (a.EntityId != null && a.EntityId.Contains(term)) ||
                                     (a.NewValues != null && a.NewValues.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(a => a.EntityName == entity);
        if (from.HasValue) query = query.Where(a => a.CreatedAt >= from.Value.Date);
        if (to.HasValue) query = query.Where(a => a.CreatedAt < to.Value.Date.AddDays(1));
        return query;
    }

    // ───────────────────────── Backup & data exports ─────────────────────────

    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> Backup()
    {
        var vm = new AdminBackupViewModel
        {
            Students = await _db.Students.CountAsync(),
            Payments = await _db.Payments.CountAsync(),
            JournalEntries = await _db.JournalEntries.CountAsync(),
            Users = await _db.Users.CountAsync(),
            Tables = _db.Model.GetEntityTypes().Select(e => e.GetTableName()).Distinct().Count()
        };

        var conn = _db.Database.GetDbConnection();
        vm.ServerName = conn.DataSource;
        vm.DatabaseName = conn.Database;

        await _db.Database.OpenConnectionAsync();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(64)) + N' · ' + CAST(SERVERPROPERTY('Edition') AS nvarchar(128));";
            vm.ServerVersion = (await cmd.ExecuteScalarAsync())?.ToString() ?? "";
            cmd.CommandText = "SELECT CAST(SUM(CAST(size AS bigint)) * 8192 AS bigint) FROM sys.database_files;";
            vm.DatabaseBytes = Convert.ToInt64(await cmd.ExecuteScalarAsync() ?? 0L);
            cmd.CommandText = "SELECT TOP (1) physical_name FROM sys.database_files WHERE type = 0;";
            vm.DatabasePath = (await cmd.ExecuteScalarAsync())?.ToString() ?? "";

            // DBCC CHECKDB raises an error for any allocation or consistency problem;
            // with NO_INFOMSGS a healthy database returns silently.
            cmd.CommandText = "DBCC CHECKDB WITH NO_INFOMSGS;";
            cmd.CommandTimeout = 120;
            try
            {
                await cmd.ExecuteNonQueryAsync();
                vm.Integrity = "ok";
            }
            catch (SqlException ex)
            {
                vm.Integrity = ex.Message;
            }
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }

        vm.Backups = ListBackups();
        vm.LastBackupAt = vm.Backups.FirstOrDefault()?.CreatedAt;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> CreateBackup()
    {
        Directory.CreateDirectory(BackupDirectory);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var name = $"kirkuk-institute-{stamp}.bak";
        var suffix = 1;
        while (System.IO.File.Exists(Path.Combine(BackupDirectory, name)))
        {
            name = $"kirkuk-institute-{stamp}-{suffix++}.bak";
        }
        var target = Path.Combine(BackupDirectory, name);
        var databaseName = _db.Database.GetDbConnection().Database;

        try
        {
            await GrantServerWriteAccessAsync(BackupDirectory);
            // BACKUP accepts variables for both the database name and the target file, so the
            // values travel as parameters instead of being concatenated into the statement.
            await _db.Database.ExecuteSqlAsync(
                $"BACKUP DATABASE {databaseName} TO DISK = {target} WITH COPY_ONLY, INIT, CHECKSUM, NAME = {name}");
        }
        catch (Exception ex)
        {
            TempData["Error"] = _l.T($"Backup failed: {ex.Message}", $"فشل إنشاء النسخة الاحتياطية: {ex.Message}");
            return RedirectToAction(nameof(Backup));
        }

        foreach (var old in ListBackups().Skip(10))
        {
            var path = Path.Combine(BackupDirectory, old.Name);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }

        await _audit.LogAsync("Create database backup", "Database", name, null, new { File = name });
        TempData["Success"] = _l.T($"Snapshot {name} was created.", $"تم إنشاء النسخة الاحتياطية {name}.");
        return RedirectToAction(nameof(Backup));
    }

    /// <summary>
    /// SQL Server writes backup files with its own service account, which normally has no rights
    /// on the application folder. Grant it modify access on the backups folder (best effort).
    /// </summary>
    private async Task GrantServerWriteAccessAsync(string directory)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var account = await _db.Database
                .SqlQueryRaw<string>("SELECT TOP (1) service_account AS [Value] FROM sys.dm_server_services WHERE servicename LIKE N'SQL Server (%' ORDER BY servicename")
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(account)) return;

            var info = new DirectoryInfo(directory);
            var acl = info.GetAccessControl();
            acl.AddAccessRule(new FileSystemAccessRule(
                new NTAccount(account),
                FileSystemRights.Modify,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            info.SetAccessControl(acl);
        }
        catch
        {
            // Lacking VIEW SERVER STATE or ACL rights is not fatal: the BACKUP statement will report
            // a clear access-denied error if the server really cannot write here.
        }
    }

    [Authorize(Roles = Roles.Administrator)]
    public IActionResult DownloadBackup(string name)
    {
        var path = SafeBackupPath(name);
        if (path is null) return BadRequest();
        return PhysicalFile(path, "application/octet-stream", Path.GetFileName(path));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> DeleteBackup(string name)
    {
        var path = SafeBackupPath(name);
        if (path is null) return BadRequest();
        System.IO.File.Delete(path);
        await _audit.LogAsync("Delete database backup", "Database", name);
        TempData["Success"] = _l.T("Backup deleted.", "تم حذف النسخة الاحتياطية.");
        return RedirectToAction(nameof(Backup));
    }

    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> Export(string dataset)
    {
        object payload;
        switch (dataset)
        {
            case "students":
                payload = await _db.Students.AsNoTracking().Include(s => s.Grade).Include(s => s.Parent)
                    .OrderBy(s => s.StudentNo)
                    .Select(s => new
                    {
                        s.Id, s.StudentNo, s.FullNameAr, s.FullNameEn,
                        Grade = s.Grade.NameAr, Guardian = s.Parent != null ? s.Parent.FullNameAr : null,
                        s.Phone, s.Address, Status = s.Status.ToString(), s.RegistrationDate
                    }).ToListAsync();
                break;
            case "payments":
                payload = await _db.Payments.AsNoTracking().Include(p => p.Student)
                    .OrderByDescending(p => p.Date)
                    .Select(p => new
                    {
                        p.ReceiptNo, p.Date, Student = p.Student.FullNameAr, p.Student.StudentNo,
                        p.Amount, Method = p.Method.ToString(), p.Notes, p.ReceivedByName
                    }).ToListAsync();
                break;
            case "dues":
                payload = await _db.Dues.AsNoTracking().Include(d => d.Student).Include(d => d.Enrollment)
                    .OrderBy(d => d.DueDate)
                    .Select(d => new
                    {
                        d.Student.StudentNo, Student = d.Student.FullNameAr, d.Description,
                        d.Amount, d.PaidAmount, Remaining = d.Amount - d.PaidAmount,
                        d.DueDate, Status = d.Status.ToString()
                    }).ToListAsync();
                break;
            case "ledger":
                payload = await _db.JournalLines.AsNoTracking().Include(l => l.JournalEntry).Include(l => l.Account)
                    .OrderBy(l => l.JournalEntry.Date).ThenBy(l => l.Id)
                    .Select(l => new
                    {
                        l.JournalEntry.EntryNo, l.JournalEntry.Date, l.JournalEntry.Description,
                        AccountCode = l.Account.Code, Account = l.Account.NameAr,
                        l.Debit, l.Credit, l.JournalEntry.CreatedByName
                    }).ToListAsync();
                break;
            case "employees":
                payload = await _db.Employees.AsNoTracking().OrderBy(e => e.Code)
                    .Select(e => new
                    {
                        e.Code, e.FullNameAr, e.FullNameEn, e.Department, e.JobTitleAr,
                        e.Phone, e.Email, e.BasicSalary, e.Allowances, e.HireDate, Status = e.Status.ToString()
                    }).ToListAsync();
                break;
            case "payroll":
                payload = await _db.Payrolls.AsNoTracking().Include(p => p.Employee)
                    .OrderByDescending(p => p.Month).ThenBy(p => p.Employee.Code)
                    .Select(p => new
                    {
                        Month = p.Month.ToString("yyyy-MM"), Employee = p.Employee.FullNameAr,
                        p.Employee.Code, p.Basic, p.Allowances, p.Overtime, p.AbsenceDeduction,
                        p.OtherDeductions, p.Advances, p.Net, Status = p.Status.ToString(), p.PaidAt
                    }).ToListAsync();
                break;
            default:
                return BadRequest();
        }

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        await _audit.LogAsync("Export data", dataset, null, null, new { Dataset = dataset });
        return File(Encoding.UTF8.GetBytes(json), "application/json",
            $"{dataset}-{DateTime.Now:yyyyMMdd-HHmm}.json");
    }

    private List<BackupFileInfo> ListBackups()
    {
        if (!Directory.Exists(BackupDirectory)) return new List<BackupFileInfo>();
        return new DirectoryInfo(BackupDirectory).GetFiles("*.bak")
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(f => new BackupFileInfo(f.Name, f.CreationTimeUtc.ToLocalTime(), f.Length))
            .ToList();
    }

    private string? SafeBackupPath(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name) return null;
        var path = Path.Combine(BackupDirectory, name);
        return System.IO.File.Exists(path) ? path : null;
    }

    // ───────────────────────── Compliance & documentation ─────────────────────────

    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Compliance()
    {
        var settings = await _db.Settings.AsNoTracking().SingleAsync(s => s.Id == 1);
        var checks = new List<ComplianceCheck>();
        void Add(string titleEn, string titleAr, string detailEn, string detailAr, string status, string? url = null)
            => checks.Add(new ComplianceCheck(titleAr, titleEn, detailAr, detailEn, status, url));

        // Registration & licence
        var licensed = !string.IsNullOrWhiteSpace(settings.LicenseNo) && !string.IsNullOrWhiteSpace(settings.MinistryCode);
        Add("Licence & ministry code", "الترخيص ورمز الوزارة",
            licensed ? $"Licence {settings.LicenseNo} · code {settings.MinistryCode}" : "The licence number or ministry code is missing.",
            licensed ? $"إجازة {settings.LicenseNo} · رمز {settings.MinistryCode}" : "رقم الإجازة أو رمز الوزارة غير مسجل.",
            licensed ? "ok" : "fail", "/Admin/Settings");

        var yearOk = !string.IsNullOrWhiteSpace(settings.AcademicYear) &&
                     settings.AcademicYear.Contains(DateTime.Today.Year.ToString());
        Add("Academic year", "السنة الدراسية",
            yearOk ? $"Current year {settings.AcademicYear} is configured." : $"Academic year '{settings.AcademicYear}' should be reviewed.",
            yearOk ? $"السنة الدراسية {settings.AcademicYear} مهيأة." : $"ينبغي مراجعة السنة الدراسية '{settings.AcademicYear}'.",
            yearOk ? "ok" : "warn", "/Admin/Settings");

        var contactOk = !string.IsNullOrWhiteSpace(settings.Phone) && !string.IsNullOrWhiteSpace(settings.Email);
        Add("Institute contact details", "بيانات التواصل",
            contactOk ? "Official phone and email are recorded." : "Add an official phone number and email.",
            contactOk ? "الهاتف والبريد الرسميان مسجلان." : "أضف هاتفاً وبريداً رسميين للمعهد.",
            contactOk ? "ok" : "warn", "/Admin/Settings");

        var principalOk = !string.IsNullOrWhiteSpace(settings.PrincipalName);
        Add("Registered principal", "مدير المعهد المسجل",
            principalOk ? $"Principal: {settings.PrincipalName}" : "The principal's name is not recorded.",
            principalOk ? $"مدير المعهد: {settings.PrincipalName}" : "اسم مدير المعهد غير مسجل.",
            principalOk ? "ok" : "warn", "/Admin/Settings");

        // Student records
        var activeStudents = await _db.Students.CountAsync(s => s.Status == StudentStatus.Active);
        var noGuardian = await _db.Students.CountAsync(s => s.Status == StudentStatus.Active && s.ParentId == null);
        Add("Guardian records", "سجلات أولياء الأمور",
            noGuardian == 0 ? $"All {activeStudents} active students have a guardian on file." : $"{noGuardian} of {activeStudents} active students have no guardian linked.",
            noGuardian == 0 ? $"جميع الطلبة النشطين ({activeStudents}) لديهم ولي أمر مسجل." : $"{noGuardian} من {activeStudents} طالباً نشطاً بلا ولي أمر مرتبط.",
            noGuardian == 0 ? "ok" : "warn", "/Students");

        var noAddress = await _db.Students.CountAsync(s => s.Status == StudentStatus.Active && s.Address == "");
        Add("Student addresses", "عناوين الطلبة",
            noAddress == 0 ? "Residential addresses are on file for active students." : $"{noAddress} active students are missing an address.",
            noAddress == 0 ? "عناوين السكن مسجلة للطلبة النشطين." : $"{noAddress} طالباً نشطاً بلا عنوان سكن.",
            noAddress == 0 ? "ok" : "warn", "/Students");

        // Academic delivery
        var noSchedule = await _db.Courses.CountAsync(c => c.Status == CourseStatus.Active &&
                                                           !c.Sections.Any(sec => sec.Schedules.Any()));
        Add("Course timetables", "الجداول الدراسية",
            noSchedule == 0 ? "Every active course has at least one scheduled session." : $"{noSchedule} active courses have no scheduled sessions.",
            noSchedule == 0 ? "كل دورة نشطة لديها حصة مجدولة واحدة على الأقل." : $"{noSchedule} دورة نشطة بلا حصص مجدولة.",
            noSchedule == 0 ? "ok" : "warn", "/Courses");

        var idleTeachers = await _db.Teachers.CountAsync(t => t.IsActive && !t.Courses.Any());
        Add("Teaching load", "النصاب التدريسي",
            idleTeachers == 0 ? "All active teachers are assigned to at least one course." : $"{idleTeachers} active teachers have no course assignment.",
            idleTeachers == 0 ? "كل المدرسين النشطين مسندون إلى دورة واحدة على الأقل." : $"{idleTeachers} مدرّساً نشطاً بلا إسناد تدريسي.",
            idleTeachers == 0 ? "ok" : "warn", "/Teachers");

        var examsMissing = await _db.Exams.CountAsync(e => e.Date < DateTime.Today && !e.Results.Any());
        Add("Exam records", "سجلات الامتحانات",
            examsMissing == 0 ? "All past exams have results recorded." : $"{examsMissing} past exams have no marks recorded.",
            examsMissing == 0 ? "جميع الامتحانات السابقة مسجلة النتائج." : $"{examsMissing} امتحاناً سابقاً بلا درجات مسجلة.",
            examsMissing == 0 ? "ok" : "warn", "/Exams");

        var passOk = settings.PassMark >= 50;
        Add("Pass mark policy", "سياسة درجة النجاح",
            passOk ? $"Pass mark is {settings.PassMark}%." : $"Pass mark {settings.PassMark}% is below the ministry baseline of 50%.",
            passOk ? $"درجة النجاح {settings.PassMark}%." : $"درجة النجاح {settings.PassMark}% أقل من الحد الوزاري 50%.",
            passOk ? "ok" : "warn", "/Admin/Settings");

        // Attendance
        var attendancePolicyOk = settings.AttendanceWarningPercent is >= 5 and <= 40;
        Add("Absence warning threshold", "حد التنبيه بالغياب",
            attendancePolicyOk ? $"Guardians are warned after {settings.AttendanceWarningPercent}% absence." : $"Threshold {settings.AttendanceWarningPercent}% is outside the normal 5–40% range.",
            attendancePolicyOk ? $"يُنبَّه ولي الأمر بعد غياب {settings.AttendanceWarningPercent}%." : $"عتبة {settings.AttendanceWarningPercent}% خارج النطاق الطبيعي 5–40%.",
            attendancePolicyOk ? "ok" : "warn", "/Admin/Settings");

        var studentAttendanceDays = await _db.StudentAttendances
            .Where(a => a.Date >= DateTime.Today.AddDays(-7)).Select(a => a.Date).Distinct().CountAsync();
        Add("Student attendance this week", "حضور الطلبة هذا الأسبوع",
            studentAttendanceDays > 0 ? $"Attendance was recorded on {studentAttendanceDays} day(s) in the last week." : "No student attendance has been recorded in the last 7 days.",
            studentAttendanceDays > 0 ? $"تم تسجيل الحضور في {studentAttendanceDays} يوماً خلال الأسبوع الماضي." : "لم يُسجل حضور الطلبة خلال الأيام السبعة الماضية.",
            studentAttendanceDays > 0 ? "ok" : "warn", "/Attendance");

        // Finance controls
        var unbalanced = await _db.JournalLines.GroupBy(l => l.JournalEntryId)
            .Select(g => new { Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
            .CountAsync(x => x.Debit != x.Credit);
        Add("Balanced general ledger", "توازن دفتر اليومية",
            unbalanced == 0 ? "Every journal entry is balanced (debits = credits)." : $"{unbalanced} journal entries are out of balance.",
            unbalanced == 0 ? "جميع القيود المحاسبية متوازنة (مدين = دائن)." : $"{unbalanced} قيداً محاسبياً غير متوازن.",
            unbalanced == 0 ? "ok" : "fail", "/Finance/Journal");

        var overpaid = await _db.Dues.CountAsync(d => d.PaidAmount > d.Amount);
        Add("Student account integrity", "سلامة حسابات الطلبة",
            overpaid == 0 ? "No student account is overpaid." : $"{overpaid} student accounts show payments above the billed amount.",
            overpaid == 0 ? "لا توجد حسابات طلاب مدفوعة زيادة." : $"{overpaid} حساب طالب يظهر دفعاً يتجاوز المبلغ المستحق.",
            overpaid == 0 ? "ok" : "fail", "/Dues");

        var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var payrollMissing = await _db.Employees.CountAsync(e => e.Status == StaffStatus.Active &&
                                                                 !e.Payrolls.Any(p => p.Month == month));
        Add("Current month payroll", "رواتب الشهر الحالي",
            payrollMissing == 0 ? "Payroll has been generated for all active employees." : $"{payrollMissing} active employees have no payroll for this month.",
            payrollMissing == 0 ? "تم توليد رواتب جميع الموظفين النشطين." : $"{payrollMissing} موظفاً نشطاً بلا راتب لهذا الشهر.",
            payrollMissing == 0 ? "ok" : "warn", "/Hr/Payroll");

        var pendingRequests = await _db.PrivateLessonRequests.CountAsync(r =>
            (r.Status == RequestStatus.Pending || r.Status == RequestStatus.Matched) &&
            r.CreatedAt < DateTime.UtcNow.AddDays(-3));
        Add("Private lesson requests", "طلبات الدروس الخصوصية",
            pendingRequests == 0 ? "No lesson request is waiting more than 3 days." : $"{pendingRequests} lesson requests have been waiting more than 3 days.",
            pendingRequests == 0 ? "لا توجد طلبات دروس تنتظر أكثر من 3 أيام." : $"{pendingRequests} طلب درس ينتظر أكثر من 3 أيام.",
            pendingRequests == 0 ? "ok" : "warn", "/PrivateLessons");

        // Access control & continuity
        var noRole = await _db.Users.CountAsync(u => u.IsActive && !_db.UserRoles.Any(ur => ur.UserId == u.Id));
        Add("Account roles", "صلاحيات الحسابات",
            noRole == 0 ? "Every active account has a role assigned." : $"{noRole} active accounts have no role.",
            noRole == 0 ? "كل حساب نشط لديه صلاحية." : $"{noRole} حساب نشط بلا صلاحية.",
            noRole == 0 ? "ok" : "fail", "/Admin/Users");

        var lastBackup = ListBackups().FirstOrDefault();
        var backupOk = lastBackup is not null && lastBackup.CreatedAt >= DateTime.Now.AddDays(-7);
        Add("Database backup", "النسخ الاحتياطي",
            lastBackup is null ? "No database snapshot has been created yet."
                : backupOk ? $"Last snapshot: {lastBackup.CreatedAt:yyyy/MM/dd HH:mm}."
                : $"Last snapshot is older than 7 days ({lastBackup.CreatedAt:yyyy/MM/dd}).",
            lastBackup is null ? "لم تُنشأ نسخة احتياطية لقاعدة البيانات بعد."
                : backupOk ? $"آخر نسخة: {lastBackup.CreatedAt:yyyy/MM/dd HH:mm}."
                : $"آخر نسخة أقدم من 7 أيام ({lastBackup.CreatedAt:yyyy/MM/dd}).",
            backupOk ? "ok" : "warn", "/Admin/Backup");

        return View(new ComplianceViewModel
        {
            Settings = settings,
            Checks = checks,
            GeneratedAt = DateTime.UtcNow
        });
    }
}
