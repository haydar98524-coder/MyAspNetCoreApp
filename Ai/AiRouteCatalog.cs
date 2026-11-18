using KirkukInstitute.Models;

namespace KirkukInstitute.Ai;

/// <summary>
/// Whitelist of application pages the assistant may route a user to. The assistant never performs
/// actions; for anything that changes data it sends the user to the page where a human does it.
/// A route the model proposes is only returned if it is in this list and allowed for the caller's role.
/// </summary>
public static class AiRouteCatalog
{
    public sealed record AiRoute(string Path, string TitleEn, string TitleAr, string Keywords, string Roles, bool IsForm = false);

    private const string TeachingAdmin = Roles.Administrator + "," + Roles.Manager + "," + Roles.Registrar;
    private const string TeacherAdmin = Roles.Administrator + "," + Roles.Manager + "," + Roles.Hr + "," + Roles.Registrar;
    private const string FinanceRegistrar = Roles.Finance + "," + Roles.Registrar;
    private const string HrAccess = Roles.People + "," + Roles.Accountant;

    public static readonly IReadOnlyList<AiRoute> All = new List<AiRoute>
    {
        new("/Home/Dashboard", "Dashboard", "لوحة التحكم", "dashboard home overview summary kpi لوحة الرئيسية ملخص مؤشرات", Roles.Staff),
        new("/Assistant", "AI assistant", "المساعد الذكي", "assistant ai chat help مساعد ذكاء سؤال", Roles.Staff),

        new("/Students", "Students", "الطلاب", "students student list search طلاب طالب قائمة بحث", Roles.Admissions),
        new("/Students/Create", "Register a new student", "تسجيل طالب جديد", "register add new student enrol تسجيل اضافة طالب جديد", Roles.Admissions, true),
        new("/Parents", "Parents", "أولياء الأمور", "parents guardian ولي امر اولياء", TeachingAdmin),
        new("/Parents/Create", "Add a parent", "إضافة ولي أمر", "add new parent guardian اضافة ولي امر", TeachingAdmin, true),
        new("/Enrollments", "Enrollments", "الاشتراكات", "enrollments subscriptions اشتراكات تسجيل دورات", Roles.Admissions),
        new("/Enrollments/Create", "New enrollment", "اشتراك جديد", "enroll subscribe new enrollment اشتراك جديد تسجيل في دورة", Roles.Admissions, true),
        new("/Dues", "Dues & instalments", "الأقساط والمستحقات", "dues instalments outstanding unpaid fees اقساط مستحقات متأخرات غير مدفوعة", Roles.Admissions),
        new("/Payments", "Payments & receipts", "المدفوعات والإيصالات", "payments receipts paid مدفوعات ايصالات وصل", Roles.Admissions),
        new("/Payments/Collect", "Collect a payment", "استلام دفعة", "collect receive payment pay fee استلام دفع قسط تسديد", Roles.Admissions, true),

        new("/Courses", "Courses", "الدورات", "courses classes groups دورات صفوف مجاميع", TeachingAdmin),
        new("/Courses/Create", "Create a course", "إنشاء دورة", "create add new course انشاء اضافة دورة", TeachingAdmin, true),
        new("/Subjects", "Subjects", "المواد", "subjects مواد مادة", TeachingAdmin),
        new("/Grades", "Stages & grades", "المراحل والصفوف", "grades stages levels مراحل صفوف مرحلة", TeachingAdmin),
        new("/Classrooms", "Classrooms", "القاعات", "classrooms rooms halls قاعات قاعة غرف", TeachingAdmin),
        new("/Exams", "Exams & results", "الامتحانات والنتائج", "exams results marks grades امتحانات نتائج درجات علامات", Roles.TeachingStaff),
        new("/Exams/Create", "Create an exam", "إنشاء امتحان", "create new exam انشاء امتحان", Roles.TeachingStaff, true),

        new("/Attendance", "Attendance register", "سجل الحضور", "attendance absence present absent حضور غياب سجل", Roles.Staff),
        new("/Attendance/Requests", "Attendance change requests", "طلبات تعديل الحضور", "attendance change requests corrections طلبات تعديل حضور", Roles.TeachingStaff),
        new("/PrivateLessons", "Private lessons", "الدروس الخصوصية", "private lessons tutoring home دروس خصوصية منزلية", FinanceRegistrar),

        new("/Teachers", "Teachers", "المدرسون", "teachers staff tutors مدرسين مدرس اساتذة", TeacherAdmin),
        new("/Teachers/Create", "Add a teacher", "إضافة مدرس", "add new teacher hire اضافة مدرس جديد", TeacherAdmin, true),
        new("/Teachers/Directory", "Teacher directory", "دليل المدرسين", "teacher directory ratings دليل المدرسين تقييم", Roles.Staff),

        new("/Finance", "Financial overview", "الملخص المالي", "finance revenue profit income summary مالية ايرادات ارباح ملخص مالي", Roles.Finance),
        new("/Finance/Journal", "Journal entries", "القيود اليومية", "journal entries ledger accounting قيود يومية دفتر محاسبة", Roles.Finance),
        new("/Finance/NewEntry", "New journal entry", "قيد يومية جديد", "new journal entry قيد جديد", Roles.Finance, true),
        new("/Finance/Expenses", "Expenses", "المصروفات", "expenses costs spending مصروفات مصاريف نفقات", Roles.Finance),
        new("/Finance/CreateExpense", "Record an expense", "تسجيل مصروف", "record add expense تسجيل اضافة مصروف", Roles.Finance, true),
        new("/Finance/Incomes", "Other income", "الإيرادات الأخرى", "income other revenue ايرادات اخرى دخل", Roles.Finance),
        new("/Finance/CreateIncome", "Record income", "تسجيل إيراد", "record add income تسجيل ايراد", Roles.Finance, true),
        new("/Finance/Funds", "Funds & accounts", "الصناديق والحسابات", "funds cash bank accounts balance صناديق حسابات رصيد بنك نقد", Roles.Finance),
        new("/Finance/TeacherPayables", "Teacher payables", "مستحقات المدرسين", "teacher payables earnings share مستحقات المدرسين حصة", Roles.Finance),

        new("/Rentals", "Rentals overview", "الإيجارات", "rentals rent ايجارات ايجار", FinanceRegistrar),
        new("/Rentals/Bookings", "Hall bookings", "حجوزات القاعات", "hall bookings room rent حجوزات قاعات", FinanceRegistrar),
        new("/Rentals/CreateBooking", "New hall booking", "حجز قاعة جديد", "new booking book hall حجز قاعة جديد", FinanceRegistrar, true),
        new("/Rentals/Cafeteria", "Cafeteria contract", "عقد الكافتيريا", "cafeteria canteen contract كافتيريا عقد", FinanceRegistrar),
        new("/Rentals/TeacherRent", "Teacher room rent", "إيجار مدرسي السادس", "teacher rent room ايجار مدرسين", FinanceRegistrar),

        new("/Hr", "Employees", "الموظفون", "employees staff hr موظفين موارد بشرية", HrAccess),
        new("/Hr/Create", "Add an employee", "إضافة موظف", "add new employee hire اضافة موظف", Roles.People, true),
        new("/Hr/Attendance", "Staff attendance", "حضور الموظفين", "staff employee attendance حضور الموظفين", HrAccess),
        new("/Hr/Payroll", "Payroll", "الرواتب", "payroll salaries wages رواتب اجور", HrAccess),

        new("/Reports", "Reports", "التقارير", "reports analytics تقارير تحليلات", Roles.Staff),
        new("/Reports/Profit", "Profit report", "تقرير الأرباح", "profit report margin تقرير ارباح", Roles.Staff),
        new("/Reports/Attendance", "Attendance report", "تقرير الحضور", "attendance report تقرير حضور غياب", Roles.Staff),
        new("/Reports/Dues", "Dues report", "تقرير الأقساط", "dues report outstanding تقرير اقساط متأخرات", Roles.Staff),
        new("/Reports/Payroll", "Payroll report", "تقرير الرواتب", "payroll report تقرير رواتب", Roles.Staff),
        new("/Reports/Enrollments", "Enrollments report", "تقرير الاشتراكات", "enrollments report تقرير اشتراكات", Roles.Staff),

        new("/Admin/Settings", "Institute settings", "إعدادات المعهد", "settings configuration اعدادات", Roles.Leadership),
        new("/Admin/Users", "Users & roles", "المستخدمون والصلاحيات", "users roles permissions accounts مستخدمين صلاحيات", Roles.Administrator),
        new("/Admin/Audit", "Audit trail", "سجل التدقيق", "audit log trail history تدقيق سجل", Roles.Administrator),
        new("/Admin/Backup", "Backup & API", "النسخ الاحتياطي", "backup restore database نسخ احتياطي قاعدة بيانات", Roles.Administrator),
        new("/Admin/Compliance", "Compliance & docs", "الامتثال والتوثيق", "compliance documentation امتثال توثيق", Roles.Staff),
        new("/Notifications", "Notifications", "الإشعارات", "notifications alerts اشعارات تنبيهات", Roles.Staff),
    };

    public static IEnumerable<AiRoute> ForCaller(AiCaller caller) => All.Where(r => caller.InAny(r.Roles));

    /// <summary>
    /// Keyword router: ranks the caller's pages against free text (Arabic or English). Only matching pages are
    /// returned. <paramref name="preferForms"/> ranks action pages first (used when routing a refused action).
    /// </summary>
    public static IReadOnlyList<AiRoute> Match(string? text, AiCaller caller, bool preferForms = false, int take = 5)
    {
        var tokens = (text ?? "").ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '،', '?', '؟', '/', '-', '«', '»', '"', '(', ')', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.StartsWith("ال") && t.Length > 3 ? t[2..] : t)
            .Where(t => t.Length >= 3).Distinct().ToList();
        if (tokens.Count == 0) return [];

        var scored = ForCaller(caller)
            .Select(r => new { Route = r, Score = tokens.Count(t => $"{r.Keywords} {r.TitleEn} {r.TitleAr}".ToLowerInvariant().Contains(t)) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score);
        var ordered = preferForms ? scored.ThenByDescending(x => x.Route.IsForm) : scored.ThenBy(x => x.Route.IsForm);
        return ordered.Take(take).Select(x => x.Route).ToList();
    }

    /// <summary>Validates a model-proposed route. Returns the canonical route or null.</summary>
    public static (AiRoute Route, string Url)? Validate(string? proposed, AiCaller caller)
    {
        if (string.IsNullOrWhiteSpace(proposed)) return null;
        var value = proposed.Trim();
        if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase) || value.StartsWith("//")) return null;

        var q = value.IndexOf('?');
        var path = (q >= 0 ? value[..q] : value).TrimEnd('/');
        var query = q >= 0 ? value[(q + 1)..] : "";
        if (path.EndsWith("/Index", StringComparison.OrdinalIgnoreCase)) path = path[..^"/Index".Length];

        var route = ForCaller(caller).FirstOrDefault(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (route is null) return null;

        // Only simple, harmless query strings (e.g. studentId=12) are kept.
        var safeQuery = query.Length <= 80 && System.Text.RegularExpressions.Regex.IsMatch(query, "^[A-Za-z0-9_=&\\-]*$") ? query : "";
        return (route, safeQuery.Length > 0 ? $"{route.Path}?{safeQuery}" : route.Path);
    }
}
