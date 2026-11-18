using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private static readonly string[] StaffRoles =
    {
        Roles.Administrator, Roles.Manager, Roles.Accountant, Roles.Hr, Roles.Registrar
    };

    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly Localizer _l;

    public NotificationsController(AppDbContext db, AuditService audit, Localizer localizer)
    {
        _db = db;
        _audit = audit;
        _l = localizer;
    }

    public async Task<IActionResult> Index(string filter = "all", string? type = null)
    {
        var userId = User.CurrentUserId();
        var roles = await UserRolesAsync(userId);
        var query = AccessibleQuery(userId, roles);

        var vm = new NotificationsViewModel
        {
            Filter = filter is "unread" or "read" ? filter : "all",
            Type = type,
            Total = await query.CountAsync(),
            Unread = await query.CountAsync(n => !n.IsRead)
        };

        if (vm.Filter == "unread") query = query.Where(n => !n.IsRead);
        else if (vm.Filter == "read") query = query.Where(n => n.IsRead);
        if (Enum.TryParse<NotificationType>(type, out var parsedType))
            query = query.Where(n => n.Type == parsedType);
        else
            vm.Type = null;

        vm.Items = await query
            .OrderBy(n => n.IsRead)
            .ThenByDescending(n => n.CreatedAt)
            .Take(200)
            .ToListAsync();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(long id, string? filter, string? type)
    {
        var userId = User.CurrentUserId();
        var roles = await UserRolesAsync(userId);
        var notification = await AccessibleQuery(userId, roles).FirstOrDefaultAsync(n => n.Id == id);
        if (notification is not null && !notification.IsRead)
        {
            notification.IsRead = true;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index), new { filter, type });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(string? filter, string? type)
    {
        var userId = User.CurrentUserId();
        var roles = await UserRolesAsync(userId);
        var ids = await AccessibleQuery(userId, roles).Where(n => !n.IsRead).Select(n => n.Id).ToListAsync();
        if (ids.Count > 0)
        {
            await _db.Notifications.Where(n => ids.Contains(n.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        }
        TempData["Success"] = _l.T("All notifications are marked as read.", "تم تعليم جميع الإشعارات كمقروءة.");
        return RedirectToAction(nameof(Index), new { filter, type });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, string? filter, string? type)
    {
        var userId = User.CurrentUserId();
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification is not null)
        {
            _db.Notifications.Remove(notification);
            await _db.SaveChangesAsync();
            TempData["Success"] = _l.T("Notification deleted.", "تم حذف الإشعار.");
        }
        return RedirectToAction(nameof(Index), new { filter, type });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearRead(string? filter, string? type)
    {
        var userId = User.CurrentUserId();
        var removed = await _db.Notifications.Where(n => n.UserId == userId && n.IsRead).ExecuteDeleteAsync();
        await _audit.LogAsync("Clear read notifications", nameof(Notification), null, null, new { Removed = removed });
        TempData["Success"] = _l.T($"{removed} read notifications were removed.", $"تم حذف {removed} إشعاراً مقروءاً.");
        return RedirectToAction(nameof(Index), new { filter, type });
    }

    [Authorize(Roles = Roles.Leadership)]
    public IActionResult Compose() => View(new AnnouncementFormViewModel());

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Compose(AnnouncementFormViewModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.Url) && !model.Url.StartsWith('/'))
            ModelState.AddModelError(nameof(model.Url), _l.T("Use an internal path starting with '/'.", "استخدم مساراً داخلياً يبدأ بـ '/'."));
        if (!ModelState.IsValid) return View(model);

        string[]? roles = model.Audience switch
        {
            "staff" => StaffRoles,
            "teachers" => new[] { Roles.Teacher },
            "parents" => new[] { Roles.Parent },
            "students" => new[] { Roles.Student },
            _ => null
        };

        var users = _db.Users.Where(u => u.IsActive);
        if (roles is not null)
        {
            users = users.Where(u => _db.UserRoles.Any(ur => ur.UserId == u.Id &&
                _db.Roles.Any(r => r.Id == ur.RoleId && roles.Contains(r.Name!))));
        }
        var ids = await users.Select(u => u.Id).ToListAsync();
        ids.Remove(User.CurrentUserId());

        var titleAr = model.TitleAr.Trim();
        var titleEn = model.TitleEn.Trim();
        var bodyAr = model.BodyAr.Trim();
        var bodyEn = model.BodyEn.Trim();
        var url = string.IsNullOrWhiteSpace(model.Url) ? null : model.Url.Trim();
        foreach (var id in ids)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = id,
                TitleAr = titleAr,
                TitleEn = titleEn,
                BodyAr = bodyAr,
                BodyEn = bodyEn,
                Url = url,
                Type = model.Type,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Send announcement", nameof(Notification), null, null,
            new { Audience = model.Audience, Recipients = ids.Count, Title = titleEn });
        TempData["Success"] = _l.T($"Announcement delivered to {ids.Count} accounts.", $"تم إرسال الإعلان إلى {ids.Count} حساباً.");
        return RedirectToAction(nameof(Index));
    }

    private IQueryable<Notification> AccessibleQuery(string userId, List<string> roles) =>
        _db.Notifications
            .Where(n => n.UserId == userId || (n.AudienceRole != null && roles.Contains(n.AudienceRole)));

    private async Task<List<string>> UserRolesAsync(string userId) =>
        await (from ur in _db.UserRoles
               join r in _db.Roles on ur.RoleId equals r.Id
               where ur.UserId == userId
               select r.Name!).ToListAsync();
}
