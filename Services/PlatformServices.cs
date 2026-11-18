using System.Text.Json;
using KirkukInstitute.Data;
using KirkukInstitute.Models;
using Microsoft.AspNetCore.Identity;

namespace KirkukInstitute.Services;

public class AuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task LogAsync(string action, string entityName, string? entityId,
        object? oldValues = null, object? newValues = null)
    {
        var user = _http.HttpContext?.User;
        var entry = new AuditLog
        {
            UserId = user?.CurrentUserId(),
            UserName = string.IsNullOrWhiteSpace(user?.DisplayName()) ? "system" : user!.DisplayName(),
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, new JsonSerializerOptions { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles }),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, new JsonSerializerOptions { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles }),
            IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = DateTime.UtcNow
        };
        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync();
    }
}

public class NotificationService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public NotificationService(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task NotifyUserAsync(string? userId, string titleAr, string titleEn,
        string bodyAr, string bodyEn, string? url = null, NotificationType type = NotificationType.Info)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            TitleAr = titleAr,
            TitleEn = titleEn,
            BodyAr = bodyAr,
            BodyEn = bodyEn,
            Url = url,
            Type = type,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task NotifyRoleAsync(string role, string titleAr, string titleEn,
        string bodyAr, string bodyEn, string? url = null, NotificationType type = NotificationType.Info,
        string? exceptUserId = null)
    {
        var users = await _users.GetUsersInRoleAsync(role);
        foreach (var user in users.Where(u => u.IsActive && u.Id != exceptUserId))
        {
            _db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                TitleAr = titleAr,
                TitleEn = titleEn,
                BodyAr = bodyAr,
                BodyEn = bodyEn,
                Url = url,
                Type = type,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
    }

    public async Task NotifyGuardianAsync(Student student, string titleAr, string titleEn,
        string bodyAr, string bodyEn, string? url = null, NotificationType type = NotificationType.Info)
    {
        var userId = student.UserId;
        if (string.IsNullOrWhiteSpace(userId) && student.ParentId.HasValue)
        {
            var parent = await _db.Parents.FindAsync(student.ParentId.Value);
            userId = parent?.UserId;
        }
        await NotifyUserAsync(userId, titleAr, titleEn, bodyAr, bodyEn, url, type);
    }
}

public class FileStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".pdf"
    };
    private const long MaxSize = 5 * 1024 * 1024;
    private readonly IWebHostEnvironment _env;

    public FileStorageService(IWebHostEnvironment env) => _env = env;

    public async Task<string?> SaveAsync(IFormFile? file, string folder)
    {
        if (file is null || file.Length == 0) return null;
        if (file.Length > MaxSize) throw new InvalidOperationException("File exceeds the 5 MB limit.");
        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext)) throw new InvalidOperationException("Unsupported file type.");
        var dir = Path.Combine(_env.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(dir);
        var name = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        await using var stream = File.Create(Path.Combine(dir, name));
        await file.CopyToAsync(stream);
        return $"/uploads/{folder}/{name}";
    }
}
