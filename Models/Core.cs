using Microsoft.AspNetCore.Identity;

namespace KirkukInstitute.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string PreferredLanguage { get; set; } = "ar";
    public int? TeacherId { get; set; }
    public int? ParentId { get; set; }
    public int? StudentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Branch
{
    public int Id { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Code { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public string LicenseNo { get; set; } = "";
    public bool IsMain { get; set; }
    public bool IsActive { get; set; } = true;
}

public class InstituteSettings
{
    public int Id { get; set; }
    public string NameAr { get; set; } = "معهد كركوك الأهلي لدورات التقوية";
    public string NameEn { get; set; } = "Kirkuk National Institute";
    public string LicenseNo { get; set; } = "";
    public string MinistryCode { get; set; } = "";
    public string AddressAr { get; set; } = "كركوك - حي المصلى - شارع المعلمين";
    public string AddressEn { get; set; } = "Kirkuk, Al-Musalla district";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string PrincipalName { get; set; } = "";
    public string AcademicYear { get; set; } = "2026–2027";
    public string MottoAr { get; set; } = "العلم نور والانضباط أساس النجاح";
    public string MottoEn { get; set; } = "Knowledge is light, discipline is the foundation of success";
    public string CurrencyAr { get; set; } = "د.ع";
    public string CurrencyEn { get; set; } = "IQD";
    public bool TuitionEnabled { get; set; } = true;
    public decimal DefaultTeacherShare { get; set; } = 55m;
    public decimal PrivateLessonTeacherShare { get; set; } = 60m;
    public long PrepTeacherMonthlyRent { get; set; } = 250_000;
    public long CafeteriaAnnualRent { get; set; } = 8_400_000;
    public int PassMark { get; set; } = 50;
    public int AttendanceWarningPercent { get; set; } = 20;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string UserName { get; set; } = "system";
    public string Action { get; set; } = "";
    public string EntityName { get; set; } = "";
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string? AudienceRole { get; set; }
    public string TitleAr { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public string BodyAr { get; set; } = "";
    public string BodyEn { get; set; } = "";
    public string? Url { get; set; }
    public NotificationType Type { get; set; } = NotificationType.Info;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
