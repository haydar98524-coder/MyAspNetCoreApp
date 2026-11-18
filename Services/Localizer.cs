using System.Globalization;
using KirkukInstitute.Models;

namespace KirkukInstitute.Services;

public class Localizer
{
    private static readonly string[] ArMonths =
    {
        "كانون الثاني", "شباط", "آذار", "نيسان", "أيار", "حزيران",
        "تموز", "آب", "أيلول", "تشرين الأول", "تشرين الثاني", "كانون الأول"
    };
    private static readonly string[] EnMonths =
    {
        "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
    };
    private static readonly Dictionary<string, string> ArDays = new()
    {
        ["Sunday"] = "الأحد", ["Monday"] = "الاثنين", ["Tuesday"] = "الثلاثاء",
        ["Wednesday"] = "الأربعاء", ["Thursday"] = "الخميس", ["Friday"] = "الجمعة", ["Saturday"] = "السبت"
    };
    private static readonly Dictionary<string, string> EnDays = new()
    {
        ["Sunday"] = "Sunday", ["Monday"] = "Monday", ["Tuesday"] = "Tuesday",
        ["Wednesday"] = "Wednesday", ["Thursday"] = "Thursday", ["Friday"] = "Friday", ["Saturday"] = "Saturday"
    };

    private static readonly Dictionary<string, (string Ar, string En)> Labels = new()
    {
        ["Gender.Male"] = ("ذكر", "Male"),
        ["Gender.Female"] = ("أنثى", "Female"),
        ["StageType.Primary"] = ("ابتدائي", "Primary"),
        ["StageType.Intermediate"] = ("متوسط", "Intermediate"),
        ["StageType.Preparatory"] = ("إعدادي", "Preparatory"),
        ["GradeBranch.General"] = ("عام", "General"),
        ["GradeBranch.Scientific"] = ("علمي", "Scientific"),
        ["GradeBranch.Literary"] = ("أدبي", "Literary"),
        ["CourseType.Group"] = ("دورة جماعية", "Group course"),
        ["CourseType.Individual"] = ("دورة فردية", "Individual course"),
        ["CourseType.SingleLesson"] = ("درس متفرق", "Single lesson"),
        ["CourseType.Private"] = ("درس خصوصي", "Private lesson"),
        ["CourseType.Home"] = ("درس منزلي", "Home lesson"),
        ["CourseStatus.Draft"] = ("مسودة", "Draft"),
        ["CourseStatus.Active"] = ("نشطة", "Active"),
        ["CourseStatus.Completed"] = ("مكتملة", "Completed"),
        ["CourseStatus.Cancelled"] = ("ملغاة", "Cancelled"),
        ["EnrollmentStatus.Active"] = ("مستمر", "Active"),
        ["EnrollmentStatus.Completed"] = ("مكتمل", "Completed"),
        ["EnrollmentStatus.Cancelled"] = ("ملغى", "Cancelled"),
        ["StudentStatus.Active"] = ("منتظم", "Active"),
        ["StudentStatus.Suspended"] = ("موقوف", "Suspended"),
        ["StudentStatus.Graduated"] = ("متخرج", "Graduated"),
        ["StudentStatus.Withdrawn"] = ("منسحب", "Withdrawn"),
        ["StaffStatus.Active"] = ("على الملاك", "Active"),
        ["StaffStatus.OnLeave"] = ("إجازة", "On leave"),
        ["StaffStatus.Inactive"] = ("غير نشط", "Inactive"),
        ["TeachingMode.Institute"] = ("داخل المعهد", "At institute"),
        ["TeachingMode.Home"] = ("في منزل الطالب", "At student's home"),
        ["TeachingMode.Both"] = ("المعهد والمنزل", "Institute & home"),
        ["AttendanceStatus.Present"] = ("حاضر", "Present"),
        ["AttendanceStatus.Absent"] = ("غائب", "Absent"),
        ["AttendanceStatus.Late"] = ("متأخر", "Late"),
        ["AttendanceStatus.Excused"] = ("غياب بعذر", "Excused"),
        ["ChangeRequestStatus.Pending"] = ("قيد المراجعة", "Pending"),
        ["ChangeRequestStatus.Approved"] = ("تمت الموافقة", "Approved"),
        ["ChangeRequestStatus.Rejected"] = ("مرفوض", "Rejected"),
        ["DueStatus.Unpaid"] = ("غير مدفوع", "Unpaid"),
        ["DueStatus.Partial"] = ("مدفوع جزئياً", "Partially paid"),
        ["DueStatus.Paid"] = ("مدفوع", "Paid"),
        ["DueStatus.Waived"] = ("مُعفى", "Waived"),
        ["PaymentMethod.Cash"] = ("نقداً", "Cash"),
        ["PaymentMethod.BankTransfer"] = ("حوالة مصرفية", "Bank transfer"),
        ["PaymentMethod.Card"] = ("بطاقة", "Card"),
        ["PaymentMethod.Wallet"] = ("محفظة إلكترونية", "E-wallet"),
        ["AccountType.Asset"] = ("أصول", "Assets"),
        ["AccountType.Liability"] = ("التزامات", "Liabilities"),
        ["AccountType.Equity"] = ("حقوق ملكية", "Equity"),
        ["AccountType.Revenue"] = ("إيرادات", "Revenue"),
        ["AccountType.Expense"] = ("مصروفات", "Expenses"),
        ["FundType.Cash"] = ("صندوق نقدي", "Cash fund"),
        ["FundType.Bank"] = ("حساب مصرفي", "Bank account"),
        ["ExpenseStatus.Draft"] = ("مسودة", "Draft"),
        ["ExpenseStatus.Approved"] = ("معتمد", "Approved"),
        ["PayrollStatus.Draft"] = ("مسودة", "Draft"),
        ["PayrollStatus.Approved"] = ("معتمد", "Approved"),
        ["PayrollStatus.Paid"] = ("مدفوع", "Paid"),
        ["BookingPaymentStatus.Unpaid"] = ("غير مدفوع", "Unpaid"),
        ["BookingPaymentStatus.Paid"] = ("مدفوع", "Paid"),
        ["RequestStatus.Pending"] = ("جديد", "Pending"),
        ["RequestStatus.Matched"] = ("تم الإسناد", "Matched"),
        ["RequestStatus.Accepted"] = ("مقبول", "Accepted"),
        ["RequestStatus.Rejected"] = ("مرفوض", "Rejected"),
        ["RequestStatus.Completed"] = ("مكتمل", "Completed"),
        ["RequestStatus.Cancelled"] = ("ملغى", "Cancelled"),
        ["PrivateLessonStatus.Scheduled"] = ("مجدول", "Scheduled"),
        ["PrivateLessonStatus.Completed"] = ("مكتمل", "Completed"),
        ["PrivateLessonStatus.Cancelled"] = ("ملغى", "Cancelled"),
        ["PrivateLessonStatus.NoShow"] = ("لم يحضر", "No-show"),
        ["TeacherEarningKind.CourseShare"] = ("حصة دورة", "Course share"),
        ["TeacherEarningKind.PrivateLesson"] = ("درس خصوصي", "Private lesson"),
        ["ClassroomStatus.Available"] = ("متاحة", "Available"),
        ["ClassroomStatus.Maintenance"] = ("صيانة", "Maintenance"),
        ["ClassroomStatus.Retired"] = ("خارج الخدمة", "Retired"),
        ["AssetStatus.InUse"] = ("قيد الاستخدام", "In use"),
        ["AssetStatus.UnderMaintenance"] = ("تحت الصيانة", "Under maintenance"),
        ["AssetStatus.Disposed"] = ("مستبعد", "Disposed"),
        ["NotificationType.Info"] = ("معلومة", "Info"),
        ["NotificationType.Success"] = ("نجاح", "Success"),
        ["NotificationType.Warning"] = ("تنبيه", "Warning"),
        ["NotificationType.Danger"] = ("عاجل", "Urgent"),
        ["ExamType.Quiz"] = ("اختبار قصير", "Quiz"),
        ["ExamType.Monthly"] = ("امتحان شهري", "Monthly exam"),
        ["ExamType.Midterm"] = ("نصف السنة", "Midterm"),
        ["ExamType.Final"] = ("نهائي", "Final"),
        ["TuitionContractStatus.Draft"] = ("مسودة", "Draft"),
        ["TuitionContractStatus.Active"] = ("نشط", "Active"),
        ["TuitionContractStatus.Completed"] = ("مكتمل", "Completed"),
        ["TuitionContractStatus.Cancelled"] = ("ملغى", "Cancelled"),
        ["CashierShiftStatus.Open"] = ("مفتوحة", "Open"),
        ["CashierShiftStatus.PendingApproval"] = ("بانتظار اعتماد الفرق", "Variance pending"),
        ["CashierShiftStatus.Closed"] = ("مغلقة", "Closed"),
        ["TeacherCompensationType.PercentageCollected"] = ("نسبة من المحصل", "Percentage of collections"),
        ["TeacherCompensationType.FixedMonthly"] = ("مخصص شهري ثابت", "Fixed monthly"),
        ["TeacherSettlementStatus.Draft"] = ("مسودة", "Draft"),
        ["TeacherSettlementStatus.Approved"] = ("معتمد", "Approved"),
        ["TeacherSettlementStatus.Paid"] = ("مدفوع", "Paid"),
        ["TeacherSettlementLineKind.Earning"] = ("حصة مستحقة", "Accrued share"),
        ["TeacherSettlementLineKind.FixedRetainer"] = ("مخصص شهري", "Monthly retainer")
    };

    private static readonly Dictionary<string, (string Ar, string En)> RoleLabels = new()
    {
        [Roles.Administrator] = ("مدير النظام", "System administrator"),
        [Roles.Manager] = ("مدير المعهد", "Institute manager"),
        [Roles.Accountant] = ("المحاسب", "Accountant"),
        [Roles.Hr] = ("الموارد البشرية", "Human resources"),
        [Roles.Registrar] = ("مسؤول التسجيل", "Registrar"),
        [Roles.Teacher] = ("مدرّس", "Teacher"),
        [Roles.Parent] = ("ولي أمر", "Parent"),
        [Roles.Student] = ("طالب", "Student")
    };

    public bool IsArabic => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
    public bool IsRtl => IsArabic;
    public string Dir => IsArabic ? "rtl" : "ltr";
    public string Lang => IsArabic ? "ar" : "en";
    public string OtherLang => IsArabic ? "en" : "ar";
    public string T(string en, string ar) => IsArabic ? ar : en;
    public string Pick(string? ar, string? en) => IsArabic ? (ar ?? en ?? "") : (en ?? ar ?? "");
    public string Money(long amount) => IsArabic
        ? string.Format(CultureInfo.InvariantCulture, "{0:N0} د.ع", amount)
        : string.Format(CultureInfo.InvariantCulture, "IQD {0:N0}", amount);
    public string Num(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
    public string Num(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
    public string Num(decimal value) => value.ToString("N0", CultureInfo.InvariantCulture);
    public string Percent(decimal value) => value.ToString("0.#", CultureInfo.InvariantCulture) + "%";
    public string Date(DateTime d) => IsArabic
        ? $"{d.Day} {ArMonths[d.Month - 1]} {d.Year}"
        : $"{d.Day} {EnMonths[d.Month - 1]} {d.Year}";
    public string DateShort(DateTime d) => d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
    public string DateTimeShort(DateTime d) => d.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
    public string Month(DateTime d) => IsArabic ? $"{ArMonths[d.Month - 1]} {d.Year}" : $"{EnMonths[d.Month - 1]} {d.Year}";
    public string MonthShort(int month) => IsArabic ? ArMonths[month - 1] : EnMonths[month - 1];
    public string Day(DayOfWeek day) => IsArabic ? ArDays[day.ToString()] : EnDays[day.ToString()];
    public string Role(string? role) => role is not null && RoleLabels.TryGetValue(role, out var r) ? Pick(r.Ar, r.En) : (role ?? "");
    public string E<T>(T value) where T : struct, Enum
    {
        var key = $"{typeof(T).Name}.{value}";
        return Labels.TryGetValue(key, out var label) ? Pick(label.Ar, label.En) : value.ToString();
    }
    public string Age(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age)) age--;
        return T($"{age} yrs", $"{age} سنة");
    }
    public string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "؟";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1 ? parts[0][..1] : $"{parts[0][0]}{parts[^1][0]}";
    }
}
