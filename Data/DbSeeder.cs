using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var accounting = sp.GetRequiredService<AccountingService>();

        // Repair legacy settings rows whose dash was persisted as a Windows-1252 escape
        // (e.g. "2026%962027"). Runs on every start so existing installs self-heal.
        var existingSettings = await db.Settings.FirstOrDefaultAsync(s => s.Id == 1);
        if (existingSettings is not null
            && (existingSettings.AcademicYear.Contains("%96") || existingSettings.AcademicYear.Contains("\\u2013")))
        {
            existingSettings.AcademicYear = "2026–2027";
            existingSettings.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        // Notifications seeded before the portals were renamed still point at the old
        // /TeacherPortal, /ParentPortal and /StudentPortal URLs. Rewrite them in place.
        var staleLinks = await db.Notifications
            .Where(n => n.Url != null && (n.Url.StartsWith("/TeacherPortal")
                || n.Url.StartsWith("/ParentPortal") || n.Url.StartsWith("/StudentPortal")))
            .ToListAsync();
        if (staleLinks.Count > 0)
        {
            foreach (var n in staleLinks)
                n.Url = n.Url!
                    .Replace("/TeacherPortal", "/Teacher")
                    .Replace("/ParentPortal", "/Parent")
                    .Replace("/StudentPortal", "/Student");
            await db.SaveChangesAsync();
        }

        if (await db.Students.AnyAsync()) return;

        foreach (var role in new[] { Roles.Administrator, Roles.Manager, Roles.Accountant, Roles.Hr, Roles.Registrar, Roles.Teacher, Roles.Parent, Roles.Student })
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }

        var settings = await db.Settings.SingleAsync(s => s.Id == 1);
        settings.LicenseNo = "KRK-EDU-2019-0142";
        settings.MinistryCode = "MOE-KRK-0142";
        settings.Phone = "0770 123 4567";
        settings.Email = "info@kirkuk-institute.iq";
        settings.PrincipalName = "أ. عبد الرحمن الجبوري";
        settings.AcademicYear = "2026–2027";
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var mainBranch = new Branch
        {
            NameAr = "الفرع الرئيسي - المصلى",
            NameEn = "Main Branch – Al-Musalla",
            Code = "KRI-01",
            Address = "كركوك - حي المصلى - شارع المعلمين",
            Phone = "0770 123 4567",
            LicenseNo = "KRK-EDU-2019-0142",
            IsMain = true
        };
        var secondBranch = new Branch
        {
            NameAr = "فرع شورجة",
            NameEn = "Shorja Branch",
            Code = "KRI-02",
            Address = "كركوك - شورجة - قرب سوق الذهب",
            Phone = "0771 555 8900",
            LicenseNo = "KRK-EDU-2019-0143",
            IsActive = false
        };
        db.Branches.AddRange(mainBranch, secondBranch);
        await db.SaveChangesAsync();

        // ---- Chart of accounts ----
        var roots = new[]
        {
            new Account { Code = "1000", NameAr = "الأصول", NameEn = "Assets", Type = AccountType.Asset, IsSystem = true },
            new Account { Code = "2000", NameAr = "الالتزامات", NameEn = "Liabilities", Type = AccountType.Liability, IsSystem = true },
            new Account { Code = "3000", NameAr = "حقوق الملكية", NameEn = "Equity", Type = AccountType.Equity, IsSystem = true },
            new Account { Code = "4000", NameAr = "الإيرادات", NameEn = "Revenue", Type = AccountType.Revenue, IsSystem = true },
            new Account { Code = "5000", NameAr = "المصروفات", NameEn = "Expenses", Type = AccountType.Expense, IsSystem = true }
        };
        db.Accounts.AddRange(roots);
        await db.SaveChangesAsync();
        var rootByCode = roots.ToDictionary(a => a.Code);

        Account Acc(string code, string ar, string en, AccountType type, string parentCode) => new()
        {
            Code = code, NameAr = ar, NameEn = en, Type = type, ParentId = rootByCode[parentCode].Id, IsSystem = true
        };

        var accounts = new List<Account>
        {
            Acc("1100", "الصندوق النقدي", "Cash on hand", AccountType.Asset, "1000"),
            Acc("1110", "الحساب المصرفي", "Bank account", AccountType.Asset, "1000"),
            Acc("1200", "ذمم الطلبة", "Student receivables", AccountType.Asset, "1000"),
            Acc("1500", "الأصول الثابتة", "Fixed assets", AccountType.Asset, "1000"),
            Acc("2100", "مستحقات المدرسين", "Teacher entitlements payable", AccountType.Liability, "2000"),
            Acc("2200", "رواتب مستحقة", "Accrued salaries", AccountType.Liability, "2000"),
            Acc("3100", "رأس المال", "Capital", AccountType.Equity, "3000"),
            Acc("4100", "الإيرادات التعليمية", "Educational revenue", AccountType.Revenue, "4000"),
            Acc("4110", "دورات ابتدائية", "Primary courses", AccountType.Revenue, "4000"),
            Acc("4120", "دورات متوسطة", "Intermediate courses", AccountType.Revenue, "4000"),
            Acc("4130", "دورات إعدادية", "Preparatory courses", AccountType.Revenue, "4000"),
            Acc("4140", "دورات اللغة", "Language courses", AccountType.Revenue, "4000"),
            Acc("4150", "دورات أخرى", "Other courses", AccountType.Revenue, "4000"),
            Acc("4200", "إيرادات التدريس الخصوصي", "Private teaching revenue", AccountType.Revenue, "4000"),
            Acc("4210", "إيجار مدرسي السادس الإعدادي", "Preparatory teacher room rent", AccountType.Revenue, "4000"),
            Acc("4220", "عمولة الدروس الخصوصية", "Private lesson commission", AccountType.Revenue, "4000"),
            Acc("4230", "الدروس المنزلية", "Home lessons", AccountType.Revenue, "4000"),
            Acc("4300", "إيرادات إيجار القاعات", "Classroom rental revenue", AccountType.Revenue, "4000"),
            Acc("4400", "إيرادات إيجار الكافتيريا", "Cafeteria rental revenue", AccountType.Revenue, "4000"),
            Acc("4500", "إيرادات أخرى", "Other revenue", AccountType.Revenue, "4000"),
            Acc("5100", "حصة المدرسين من الإيرادات", "Teacher share of revenue", AccountType.Expense, "5000"),
            Acc("5200", "رواتب الموظفين", "Employee salaries", AccountType.Expense, "5000"),
            Acc("5300", "أجور الخدمات", "Service fees", AccountType.Expense, "5000"),
            Acc("5400", "الكهرباء", "Electricity", AccountType.Expense, "5000"),
            Acc("5500", "الماء", "Water", AccountType.Expense, "5000"),
            Acc("5600", "الإنترنت", "Internet", AccountType.Expense, "5000"),
            Acc("5700", "المولدة والطاقة", "Generator & power", AccountType.Expense, "5000"),
            Acc("5800", "التنظيف", "Cleaning", AccountType.Expense, "5000"),
            Acc("5900", "الصيانة", "Maintenance", AccountType.Expense, "5000"),
            Acc("6000", "القرطاسية", "Stationery", AccountType.Expense, "5000"),
            Acc("6100", "الدعاية والإعلان", "Marketing", AccountType.Expense, "5000"),
            Acc("6200", "المصروفات الإدارية", "Administrative expenses", AccountType.Expense, "5000"),
            Acc("6300", "مصاريف أخرى", "Other expenses", AccountType.Expense, "5000")
        };
        // parent 4100 / 4200 links, others directly under expense root
        db.Accounts.AddRange(accounts);
        await db.SaveChangesAsync();
        var accountByCode = await db.Accounts.ToDictionaryAsync(a => a.Code);
        foreach (var code in new[] { "4110", "4120", "4130", "4140", "4150" })
            accounts.Single(a => a.Code == code).ParentId = accountByCode["4100"].Id;
        foreach (var code in new[] { "4210", "4220", "4230" })
            accounts.Single(a => a.Code == code).ParentId = accountByCode["4200"].Id;
        await db.SaveChangesAsync();

        var cashFund = new Fund
        {
            NameAr = "صندوق المعهد", NameEn = "Institute cash fund", Type = FundType.Cash,
            AccountId = accountByCode[CoA.Cash].Id, OpeningBalance = 6_000_000, CurrentBalance = 6_000_000
        };
        var bankFund = new Fund
        {
            NameAr = "حساب مصرف الرشيد", NameEn = "Rasheed Bank account", Type = FundType.Bank,
            AccountId = accountByCode[CoA.Bank].Id, OpeningBalance = 18_000_000, CurrentBalance = 18_000_000,
            BankName = "مصرف الرشيد", AccountNumber = "IQ12RASH0000123456"
        };
        db.Funds.AddRange(cashFund, bankFund);
        await db.SaveChangesAsync();

        var adminUser = await EnsureUser(users, "admin@kirkuk.edu.iq", "Admin@12345", "أ. هيثم عبد الكريم", Roles.Administrator);
        var managerUser = await EnsureUser(users, "manager@kirkuk.edu.iq", "Manager@12345", "أ. عبد الرحمن الجبوري", Roles.Manager);
        var accountantUser = await EnsureUser(users, "accountant@kirkuk.edu.iq", "Accountant@12345", "أ. سعدية محمد", Roles.Accountant);
        var hrUser = await EnsureUser(users, "hr@kirkuk.edu.iq", "Hr@12345", "أ. ليث سمير", Roles.Hr);
        var registrarUser = await EnsureUser(users, "registrar@kirkuk.edu.iq", "Registrar@12345", "أ. رنا وليد", Roles.Registrar);

        await accounting.PostAsync(DateTime.Today.AddMonths(-5), "الأرصدة الافتتاحية للصندوق والحساب المصرفي",
            "Opening", null, new[]
            {
                new JournalDraft(CoA.Cash, 6_000_000, 0),
                new JournalDraft(CoA.Bank, 18_000_000, 0),
                new JournalDraft(CoA.Capital, 0, 24_000_000)
            }, adminUser.FullName, adminUser.Id);

        // ---- Academic structure ----
        var primary = new Stage { NameAr = "المرحلة الابتدائية", NameEn = "Primary Stage", Type = StageType.Primary, Order = 1 };
        var intermediate = new Stage { NameAr = "المرحلة المتوسطة", NameEn = "Intermediate Stage", Type = StageType.Intermediate, Order = 2 };
        var preparatory = new Stage { NameAr = "المرحلة الإعدادية", NameEn = "Preparatory Stage", Type = StageType.Preparatory, Order = 3 };
        db.Stages.AddRange(primary, intermediate, preparatory);
        await db.SaveChangesAsync();

        var grades = new List<Grade>();
        string[] primaryNames = { "الأول ابتدائي", "الثاني ابتدائي", "الثالث ابتدائي", "الرابع ابتدائي", "الخامس ابتدائي", "السادس ابتدائي" };
        string[] primaryNamesEn = { "First Primary", "Second Primary", "Third Primary", "Fourth Primary", "Fifth Primary", "Sixth Primary" };
        for (var i = 0; i < 6; i++)
            grades.Add(new Grade { StageId = primary.Id, Stage = primary, NameAr = primaryNames[i], NameEn = primaryNamesEn[i], Level = i + 1 });
        string[] intermediateNames = { "الأول متوسط", "الثاني متوسط", "الثالث متوسط" };
        string[] intermediateNamesEn = { "First Intermediate", "Second Intermediate", "Third Intermediate" };
        for (var i = 0; i < 3; i++)
            grades.Add(new Grade { StageId = intermediate.Id, Stage = intermediate, NameAr = intermediateNames[i], NameEn = intermediateNamesEn[i], Level = i + 1 });
        for (var level = 4; level <= 6; level++)
        {
            var ordinalAr = level == 4 ? "الرابع" : level == 5 ? "الخامس" : "السادس";
            var ordinalEn = level == 4 ? "Fourth" : level == 5 ? "Fifth" : "Sixth";
            grades.Add(new Grade { StageId = preparatory.Id, Stage = preparatory, NameAr = $"{ordinalAr} الإعدادي - علمي", NameEn = $"{ordinalEn} Preparatory – Scientific", Level = level, Branch = GradeBranch.Scientific });
            grades.Add(new Grade { StageId = preparatory.Id, Stage = preparatory, NameAr = $"{ordinalAr} الإعدادي - أدبي", NameEn = $"{ordinalEn} Preparatory – Literary", Level = level, Branch = GradeBranch.Literary });
        }
        db.Grades.AddRange(grades);
        await db.SaveChangesAsync();
        Grade G(int level, GradeBranch branch = GradeBranch.General)
        {
            // General levels 1-3 belong to intermediate; 4-6 belong to primary.
            if (branch == GradeBranch.General)
            {
                var stage = level <= 3 ? StageType.Intermediate : StageType.Primary;
                return grades.Single(g => g.Level == level && g.Branch == GradeBranch.General && g.Stage.Type == stage);
            }
            return grades.Single(g => g.Level == level && g.Branch == branch);
        }

        string[] subjectNamesAr = { "التربية الإسلامية", "اللغة العربية", "اللغة الإنجليزية", "الرياضيات", "العلوم", "الاجتماعيات", "الفيزياء", "الكيمياء", "الأحياء", "التاريخ", "الجغرافيا", "الاقتصاد", "الحاسوب", "التربية الفنية", "اللغة الفرنسية" };
        string[] subjectNamesEn = { "Islamic Education", "Arabic", "English", "Mathematics", "Science", "Social Studies", "Physics", "Chemistry", "Biology", "History", "Geography", "Economics", "Computer Science", "Art Education", "French" };
        string[] subjectCodes = { "ISL", "ARB", "ENG", "MAT", "SCI", "SOC", "PHY", "CHM", "BIO", "HIS", "GEO", "ECO", "CMP", "ART", "FRN" };
        string[] subjectColors = { "#15803d", "#0f766e", "#2563eb", "#c2410c", "#7c3aed", "#a16207", "#0891b2", "#be123c", "#4d7c0f", "#92400e", "#1d4ed8", "#b45309", "#6d28d9", "#db2777", "#475569" };
        var subjects = subjectCodes.Select((code, i) => new Subject
        {
            Code = code, NameAr = subjectNamesAr[i], NameEn = subjectNamesEn[i], Color = subjectColors[i]
        }).ToList();
        db.Subjects.AddRange(subjects);
        await db.SaveChangesAsync();
        Subject Sub(string code) => subjects.Single(s => s.Code == code);

        var rooms = new List<Classroom>
        {
            new() { Number = "101", NameAr = "قاعة النخبة", NameEn = "Elite Hall", Floor = "الطابق الأرضي", Capacity = 30, RentPerHour = 15_000, Equipment = "شاشة عرض، مكيف، سبورة ذكية", BranchId = mainBranch.Id },
            new() { Number = "102", NameAr = "قاعة الرافدين", NameEn = "Rafidain Hall", Floor = "الطابق الأرضي", Capacity = 25, RentPerHour = 12_000, Equipment = "شاشة عرض، مكيف", BranchId = mainBranch.Id },
            new() { Number = "201", NameAr = "قاعة بابل", NameEn = "Babylon Hall", Floor = "الطابق الأول", Capacity = 35, RentPerHour = 15_000, Equipment = "شاشة عرض، مكيف", BranchId = mainBranch.Id },
            new() { Number = "202", NameAr = "قاعة آشور", NameEn = "Assyria Hall", Floor = "الطابق الأول", Capacity = 24, RentPerHour = 12_000, Equipment = "مكيف، سبورة", BranchId = mainBranch.Id },
            new() { Number = "LAB-1", NameAr = "مختبر العلوم", NameEn = "Science Lab", Floor = "الطابق الأول", Capacity = 25, RentPerHour = 20_000, Equipment = "مختبر فيزياء وكيمياء، شاشة عرض", BranchId = mainBranch.Id },
            new() { Number = "HALL-1", NameAr = "القاعة الكبرى", NameEn = "Grand Hall", Floor = "الطابق الأرضي", Capacity = 80, RentPerHour = 40_000, Equipment = "منصة، صوتيات، شاشة، مكيفات", BranchId = mainBranch.Id },
            new() { Number = "203", NameAr = "قاعة دجلة", NameEn = "Tigris Hall", Floor = "الطابق الثاني", Capacity = 22, RentPerHour = 10_000, Status = ClassroomStatus.Maintenance, BranchId = mainBranch.Id }
        };
        db.Classrooms.AddRange(rooms);
        await db.SaveChangesAsync();
        Classroom Room(string number) => rooms.Single(r => r.Number == number);

        // ---- Teachers ----
        var teacherSeeds = new[]
        {
            new { Ar = "أحمد عبد الله الجبوري", En = "Ahmed Abdullah Al-Jubouri", Gender = Gender.Male, Spec = "الرياضيات", SpecEn = "Mathematics", Cert = "ماجستير رياضيات", CertEn = "M.Sc. Mathematics", Uni = "جامعة كركوك", UniEn = "University of Kirkuk", Grad = 2012, Exp = 14, Mode = TeachingMode.Institute, Areas = "المصلى، شورجة، عرفة، حي النصر", Rating = 4.8, Phone = "0770 111 2233", Subject = "MAT", GradeKeys = new[] { (3, GradeBranch.General), (4, GradeBranch.Scientific), (5, GradeBranch.Scientific), (6, GradeBranch.Scientific) } },
            new { Ar = "مريم حسن العبيدي", En = "Mariam Hassan Al-Obaidi", Gender = Gender.Female, Spec = "اللغة الإنجليزية", SpecEn = "English Language", Cert = "بكالوريوس آداب إنجليزي", CertEn = "B.A. English", Uni = "جامعة تكريت", UniEn = "University of Tikrit", Grad = 2015, Exp = 9, Mode = TeachingMode.Both, Areas = "المصلى، الزهراء، حي النصر، واسطي", Rating = 4.9, Phone = "0771 222 4455", Subject = "ENG", GradeKeys = new[] { (3, GradeBranch.General), (4, GradeBranch.Scientific), (5, GradeBranch.Scientific), (6, GradeBranch.Scientific), (6, GradeBranch.Literary) } },
            new { Ar = "عمر خالد الدليمي", En = "Omar Khalid Al-Dulaimi", Gender = Gender.Male, Spec = "الفيزياء", SpecEn = "Physics", Cert = "ماجستير فيزياء", CertEn = "M.Sc. Physics", Uni = "جامعة بغداد", UniEn = "University of Baghdad", Grad = 2010, Exp = 15, Mode = TeachingMode.Institute, Areas = "المصلى، عرفة", Rating = 4.7, Phone = "0770 333 8899", Subject = "PHY", GradeKeys = new[] { (5, GradeBranch.Scientific), (6, GradeBranch.Scientific) } },
            new { Ar = "زينب علي التميمي", En = "Zainab Ali Al-Tamimi", Gender = Gender.Female, Spec = "الكيمياء", SpecEn = "Chemistry", Cert = "بكالوريوس علوم كيمياء", CertEn = "B.Sc. Chemistry", Uni = "جامعة كركوك", UniEn = "University of Kirkuk", Grad = 2017, Exp = 7, Mode = TeachingMode.Both, Areas = "المصلى، واسطي، حي النصر", Rating = 4.6, Phone = "0772 444 1122", Subject = "CHM", GradeKeys = new[] { (5, GradeBranch.Scientific), (6, GradeBranch.Scientific) } },
            new { Ar = "يوسف نجم الشمري", En = "Yousif Najm Al-Shammari", Gender = Gender.Male, Spec = "اللغة العربية", SpecEn = "Arabic Language", Cert = "بكالوريوس آداب عربي", CertEn = "B.A. Arabic", Uni = "جامعة الموصل", UniEn = "University of Mosul", Grad = 2008, Exp = 18, Mode = TeachingMode.Institute, Areas = "المصلى، شورجة، عرفة", Rating = 4.8, Phone = "0770 555 6677", Subject = "ARB", GradeKeys = new[] { (4, GradeBranch.Literary), (5, GradeBranch.Literary), (6, GradeBranch.Literary) } },
            new { Ar = "نور محمد البياتي", En = "Noor Mohammed Al-Bayati", Gender = Gender.Female, Spec = "الأحياء", SpecEn = "Biology", Cert = "ماجستير أحياء", CertEn = "M.Sc. Biology", Uni = "جامعة كركوك", UniEn = "University of Kirkuk", Grad = 2013, Exp = 12, Mode = TeachingMode.Both, Areas = "الزهراء، واسطي، المصلى", Rating = 4.5, Phone = "0771 888 3344", Subject = "BIO", GradeKeys = new[] { (5, GradeBranch.Scientific), (6, GradeBranch.Scientific) } },
            new { Ar = "كنعان صباح النعيمي", En = "Kanaan Sabah Al-Nuaimi", Gender = Gender.Male, Spec = "اللغة الإنجليزية", SpecEn = "English Language", Cert = "بكالوريوس تربية إنجليزي", CertEn = "B.Ed. English", Uni = "جامعة صلاح الدين", UniEn = "Salahaddin University", Grad = 2016, Exp = 8, Mode = TeachingMode.Institute, Areas = "المصلى، شورجة", Rating = 4.3, Phone = "0770 777 5544", Subject = "ENG", GradeKeys = new[] { (3, GradeBranch.General), (4, GradeBranch.Scientific) } },
            new { Ar = "هدى فاضل الكناني", En = "Huda Fadhel Al-Kanani", Gender = Gender.Female, Spec = "الرياضيات", SpecEn = "Mathematics", Cert = "بكالوريوس رياضيات", CertEn = "B.Sc. Mathematics", Uni = "جامعة كركوك", UniEn = "University of Kirkuk", Grad = 2019, Exp = 5, Mode = TeachingMode.Home, Areas = "المصلى، الزهراء، واسطي، حي النصر", Rating = 4.7, Phone = "0772 666 7788", Subject = "MAT", GradeKeys = new[] { (5, GradeBranch.General), (6, GradeBranch.General) } },
            new { Ar = "سعد جاسم العبيدي", En = "Saad Jasim Al-Obaidi", Gender = Gender.Male, Spec = "التاريخ", SpecEn = "History", Cert = "بكالوريوس تربية تاريخ", CertEn = "B.Ed. History", Uni = "جامعة بغداد", UniEn = "University of Baghdad", Grad = 2011, Exp = 13, Mode = TeachingMode.Institute, Areas = "المصلى، عرفة", Rating = 4.4, Phone = "0770 222 3311", Subject = "HIS", GradeKeys = new[] { (3, GradeBranch.General), (4, GradeBranch.Literary), (5, GradeBranch.Literary), (6, GradeBranch.Literary) } },
            new { Ar = "لينا صالح عبد الرحمن", En = "Lina Saleh Abdulrahman", Gender = Gender.Female, Spec = "علوم الحاسوب", SpecEn = "Computer Science", Cert = "بكالوريوس علوم حاسوب", CertEn = "B.Sc. Computer Science", Uni = "جامعة كركوك", UniEn = "University of Kirkuk", Grad = 2020, Exp = 4, Mode = TeachingMode.Both, Areas = "المصلى، حي النصر", Rating = 4.6, Phone = "0771 333 9900", Subject = "CMP", GradeKeys = new[] { (4, GradeBranch.Scientific), (5, GradeBranch.Scientific), (6, GradeBranch.Scientific) } }
        };

        var teachers = new List<Teacher>();
        foreach (var t in teacherSeeds)
        {
            var teacher = new Teacher
            {
                Code = $"T-2026-{teachers.Count + 1:D3}",
                FullNameAr = t.Ar, FullNameEn = t.En, Gender = t.Gender,
                Phone = t.Phone, Email = $"teacher{teachers.Count + 1}@kirkuk-institute.iq",
                Specialization = t.Spec, Certificate = t.Cert, University = t.Uni,
                GraduationYear = t.Grad, ExperienceYears = t.Exp, TeachingMode = t.Mode,
                Areas = t.Areas, IsPublic = true, IsActive = true,
                HireDate = new DateTime(2026, 8, 15), RatingAvg = t.Rating, RatingCount = 0,
                BioAr = $"{t.Spec}، خبرة {t.Exp} سنوات في تدريس المراحل العراقية، متخصص في تقوية الطلبة ورفع مستواهم قبل الامتحانات الوزارية.",
                BioEn = $"{t.SpecEn} teacher with {t.Exp} years of experience across Iraqi stages, focused on exam readiness and measurable improvement."
            };
            teachers.Add(teacher);
        }
        db.Teachers.AddRange(teachers);
        await db.SaveChangesAsync();
        Teacher T(string ar) => teachers.Single(t => t.FullNameAr == ar);
        var ahmed = T("أحمد عبد الله الجبوري");
        var mariam = T("مريم حسن العبيدي");
        var omar = T("عمر خالد الدليمي");
        var zainab = T("زينب علي التميمي");
        var yousif = T("يوسف نجم الشمري");
        var noor = T("نور محمد البياتي");
        var kanaan = T("كنعان صباح النعيمي");
        var huda = T("هدى فاضل الكناني");
        var saad = T("سعد جاسم العبيدي");
        var lina = T("لينا صالح عبد الرحمن");

        foreach (var t in teacherSeeds)
        {
            var teacher = T(t.Ar);
            teacher.Subjects.Add(new TeacherSubject { SubjectId = Sub(t.Subject).Id });
            foreach (var (level, branch) in t.GradeKeys)
                teacher.Grades.Add(new TeacherGrade { GradeId = G(level, branch).Id });
            teacher.Certificates.Add(new TeacherCertificate { Title = t.Cert, Issuer = t.Uni, Year = t.Grad });
            teacher.Certificates.Add(new TeacherCertificate { Title = "دورة طرائق التدريس الحديثة", Issuer = "مديرية تربية كركوك", Year = 2023 });
            teacher.Availabilities.Add(new TeacherAvailability { DayOfWeek = DayOfWeek.Saturday, StartTime = "16:00", EndTime = "20:00" });
            teacher.Availabilities.Add(new TeacherAvailability { DayOfWeek = DayOfWeek.Monday, StartTime = "16:00", EndTime = "20:00" });
            teacher.Availabilities.Add(new TeacherAvailability { DayOfWeek = DayOfWeek.Wednesday, StartTime = "16:00", EndTime = "20:00" });
            if (t.Mode != TeachingMode.Institute)
            {
                teacher.Availabilities.Add(new TeacherAvailability { DayOfWeek = DayOfWeek.Sunday, StartTime = "10:00", EndTime = "13:00" });
                teacher.Availabilities.Add(new TeacherAvailability { DayOfWeek = DayOfWeek.Thursday, StartTime = "16:00", EndTime = "20:00" });
            }
        }
        await db.SaveChangesAsync();

        // ---- Parents & students ----
        var parentSeeds = new[]
        {
            ("عبد الرحمن علي حسن", "Abdulrahman Ali Hassan", "0770 401 1122", "المصلى", "موظف حكومي"),
            ("سهاد كريم محمود", "Suhad Karim Mahmoud", "0771 402 3344", "الزهراء", "ربّة منزل"),
            ("نزار عبد الله جاسم", "Nazar Abdullah Jasim", "0770 403 5566", "المصلى", "تاجر"),
            ("وفاء صباح إسماعيل", "Wafaa Sabah Ismail", "0772 404 7788", "واسطي", "مدرّسة"),
            ("حيدر عبد الحسين علي", "Haidar Abdulhussain Ali", "0770 405 9900", "حي النصر", "مهندس"),
            ("إيمان طاهر يوسف", "Iman Taher Yousif", "0771 406 2233", "عرفة", "صيدلانية"),
            ("مثنى عمر أحمد", "Muthanna Omar Ahmed", "0770 407 4455", "المصلى", "موظف"),
            ("رغد سعدي كاظم", "Raghad Saadi Kadhim", "0772 408 6677", "الزهراء", "مصممة"),
            ("سلام محمد رشيد", "Salam Mohammed Rashid", "0770 409 8899", "شورجة", "حرفي"),
            ("بشرى جلال الدين", "Bushra Jalaladdin", "0771 410 1133", "واسطي", "محاسبة"),
            ("عمار فاضل عباس", "Ammar Fadhel Abbas", "0770 411 3355", "عرفة", "سائق"),
            ("صفاء نوري عبد الله", "Safaa Noori Abdullah", "0772 412 5577", "حي النصر", "موظفة")
        };
        var parents = parentSeeds.Select(p => new Parent
        {
            FullNameAr = p.Item1, FullNameEn = p.Item2, Phone = p.Item3, Area = p.Item4,
            Occupation = p.Item5, Address = $"كركوك - {p.Item4}",
            Email = $"parent{parents2FirstEmail(p.Item2)}@example.iq"
        }).ToList();
        db.Parents.AddRange(parents);
        await db.SaveChangesAsync();

        static string parents2FirstEmail(string name) => name.Split(' ')[0].ToLowerInvariant();

        var studentSeeds = new[]
        {
            ("عمار عبد الرحمن علي", "Ammar Abdulrahman Ali", Gender.Male, 0, 6, GradeBranch.Scientific, "مدرسة المصلى الابتدائية", "كركوك - المصلى"),
            ("حسن عبد الرحمن علي", "Hassan Abdulrahman Ali", Gender.Male, 0, 5, GradeBranch.Scientific, "مدرسة المصلى الابتدائية", "كركوك - المصلى"),
            ("ملاك عبد الرحمن علي", "Malak Abdulrahman Ali", Gender.Female, 0, 4, GradeBranch.Scientific, "مدرسة الزهراء", "كركوك - المصلى"),
            ("ريام عبد الرحمن علي", "Riyam Abdulrahman Ali", Gender.Female, 0, 4, GradeBranch.General, "مدرسة المصلى الابتدائية", "كركوك - المصلى"),
            ("زهراء نزار عبد الله", "Zahraa Nazar Abdullah", Gender.Female, 2, 6, GradeBranch.Scientific, "إعدادية الحكمة للبنات", "كركوك - المصلى"),
            ("مريم نزار عبد الله", "Mariam Nazar Abdullah", Gender.Female, 2, 5, GradeBranch.Scientific, "إعدادية الحكمة للبنات", "كركوك - المصلى"),
            ("علي نزار عبد الله", "Ali Nazar Abdullah", Gender.Male, 2, 4, GradeBranch.Scientific, "إعدادية كركوك للبنين", "كركوك - المصلى"),
            ("تبارك نزار عبد الله", "Tabarak Nazar Abdullah", Gender.Female, 2, 6, GradeBranch.General, "مدرسة المصلى الابتدائية", "كركوك - المصلى"),
            ("هدى نزار عبد الله", "Huda Nazar Abdullah", Gender.Female, 2, 2, GradeBranch.General, "متوسطة الزهراء", "كركوك - المصلى"),
            ("محمد حيدر عبد الحسين", "Mohammed Haidar Abdulhussain", Gender.Male, 4, 6, GradeBranch.Scientific, "إعدادية الحكمة للبنين", "كركوك - حي النصر"),
            ("أحمد حيدر عبد الحسين", "Ahmed Haidar Abdulhussain", Gender.Male, 4, 5, GradeBranch.Scientific, "إعدادية الحكمة للبنين", "كركوك - حي النصر"),
            ("يوسف حيدر عبد الحسين", "Yousif Haidar Abdulhussain", Gender.Male, 4, 3, GradeBranch.General, "متوسطة كركوك للبنين", "كركوك - حي النصر"),
            ("بنين حيدر عبد الحسين", "Baneen Haidar Abdulhussain", Gender.Female, 4, 5, GradeBranch.General, "مدرسة المصلى الابتدائية", "كركوك - حي النصر"),
            ("نور الهدى مثنى عمر", "Noor Al-Huda Muthanna Omar", Gender.Female, 6, 6, GradeBranch.Scientific, "إعدادية الحكمة للبنات", "كركوك - المصلى"),
            ("هبة مثنى عمر", "Hiba Muthanna Omar", Gender.Female, 6, 3, GradeBranch.General, "متوسطة الزهراء", "كركوك - المصلى"),
            ("سجاد مثنى عمر", "Sajjad Muthanna Omar", Gender.Male, 6, 6, GradeBranch.General, "مدرسة المصلى الابتدائية", "كركوك - المصلى"),
            ("حسين مثنى عمر", "Hussain Muthanna Omar", Gender.Male, 6, 2, GradeBranch.General, "متوسطة المصلى", "كركوك - المصلى"),
            ("علي سلام محمد", "Ali Salam Mohammed", Gender.Male, 8, 6, GradeBranch.Scientific, "إعدادية كركوك للبنين", "كركوك - شورجة"),
            ("كرار سلام محمد", "Karrar Salam Mohammed", Gender.Male, 8, 3, GradeBranch.General, "متوسطة كركوك للبنين", "كركوك - شورجة"),
            ("عباس سلام محمد", "Abbas Salam Mohammed", Gender.Male, 8, 6, GradeBranch.General, "مدرسة الشورجة الابتدائية", "كركوك - شورجة"),
            ("رقية سلام محمد", "Ruqaya Salam Mohammed", Gender.Female, 8, 6, GradeBranch.Literary, "إعدادية الحكمة للبنات", "كركوك - شورجة"),
            ("فاطمة عمار فاضل", "Fatima Ammar Fadhel", Gender.Female, 10, 6, GradeBranch.Scientific, "إعدادية الحكمة للبنات", "كركوك - عرفة"),
            ("زيد عمار فاضل", "Zaid Ammar Fadhel", Gender.Male, 10, 6, GradeBranch.Literary, "إعدادية عرفة للبنين", "كركوك - عرفة"),
            ("دينا عمار فاضل", "Dina Ammar Fadhel", Gender.Female, 10, 3, GradeBranch.General, "متوسطة عرفة للبنات", "كركوك - عرفة"),
            ("أمير صفاء نوري", "Ameer Safaa Noori", Gender.Male, 11, 4, GradeBranch.Literary, "إعدادية كركوك للبنين", "كركوك - حي النصر"),
            ("غفران صفاء نوري", "Ghufran Safaa Noori", Gender.Female, 11, 6, GradeBranch.Literary, "إعدادية الحكمة للبنات", "كركوك - حي النصر"),
            ("مصطفى صفاء نوري", "Mustafa Safaa Noori", Gender.Male, 11, 5, GradeBranch.General, "مدرسة النصر الابتدائية", "كركوك - حي النصر")
        };

        var students = new List<Student>();
        var seq = 1;
        foreach (var s in studentSeeds)
        {
            var grade = G(s.Item5, s.Item6);
            var age = grade.Stage.Type == StageType.Primary ? s.Item5 + 6 : grade.Stage.Type == StageType.Intermediate ? 12 + s.Item5 : 15 + s.Item5;
            var monthsAgo = 1 + (seq % 5);
            var student = new Student
            {
                StudentNo = $"KRI-2026-{seq:D4}",
                FullNameAr = s.Item1, FullNameEn = s.Item2, Gender = s.Item3,
                BirthDate = todayMinusYears(age, monthsAgo),
                Phone = null,
                SchoolName = s.Item7, Area = s.Item8.Split(" - ").Last(),
                Address = s.Item8, GradeId = grade.Id, ParentId = parents[s.Item4].Id,
                Status = StudentStatus.Active,
                QrToken = Guid.NewGuid().ToString("N"),
                RegistrationDate = DateTime.Today.AddDays(-(5 + (seq * 3) % 45)),
                BranchId = mainBranch.Id,
                Notes = seq % 7 == 0 ? "يحتاج متابعة إضافية في الرياضيات." : null
            };
            students.Add(student);
            seq++;
        }
        static DateTime todayMinusYears(int years, int months) => DateTime.Today.AddYears(-years).AddMonths(-months).Date;
        db.Students.AddRange(students);
        await db.SaveChangesAsync();

        // ---- Portal users ----
        var teacherUser = await EnsureUser(users, "teacher@kirkuk.edu.iq", "Teacher@12345", ahmed.FullNameAr, Roles.Teacher);
        teacherUser.TeacherId = ahmed.Id;
        await users.UpdateAsync(teacherUser);
        ahmed.UserId = teacherUser.Id;

        var parentUser = await EnsureUser(users, "parent@kirkuk.edu.iq", "Parent@12345", parents[0].FullNameAr, Roles.Parent);
        parentUser.ParentId = parents[0].Id;
        await users.UpdateAsync(parentUser);
        parents[0].UserId = parentUser.Id;
        for (var i = 1; i < parents.Count; i++)
        {
            var pu = await EnsureUser(users, $"parent{i + 1}@kirkuk.edu.iq", "Parent@12345", parents[i].FullNameAr, Roles.Parent);
            pu.ParentId = parents[i].Id;
            await users.UpdateAsync(pu);
            parents[i].UserId = pu.Id;
        }

        var firstChild = students.First(s => s.ParentId == parents[0].Id);
        var studentUser = await EnsureUser(users, "student@kirkuk.edu.iq", "Student@12345", firstChild.FullNameAr, Roles.Student);
        studentUser.StudentId = firstChild.Id;
        studentUser.ParentId = parents[0].Id;
        await users.UpdateAsync(studentUser);
        firstChild.UserId = studentUser.Id;
        await db.SaveChangesAsync();

        // ---- Courses ----
        var courseSeeds = new[]
        {
            new { NameAr = "الرياضيات - السادس العلمي", NameEn = "Mathematics – Sixth Scientific", Grade = (6, GradeBranch.Scientific), Subject = "MAT", Teacher = ahmed, Fee = 300_000L, Share = 55m, Start = Today(-45), End = new DateTime(2027, 5, 15), Type = CourseType.Group, Status = CourseStatus.Active },
            new { NameAr = "الفيزياء - السادس العلمي", NameEn = "Physics – Sixth Scientific", Grade = (6, GradeBranch.Scientific), Subject = "PHY", Teacher = omar, Fee = 300_000L, Share = 55m, Start = Today(-44), End = new DateTime(2027, 5, 15), Type = CourseType.Group, Status = CourseStatus.Active },
            new { NameAr = "الكيمياء - السادس العلمي", NameEn = "Chemistry – Sixth Scientific", Grade = (6, GradeBranch.Scientific), Subject = "CHM", Teacher = zainab, Fee = 300_000L, Share = 55m, Start = Today(-43), End = new DateTime(2027, 5, 15), Type = CourseType.Group, Status = CourseStatus.Active },
            new { NameAr = "اللغة الإنجليزية - السادس العلمي", NameEn = "English – Sixth Scientific", Grade = (6, GradeBranch.Scientific), Subject = "ENG", Teacher = mariam, Fee = 250_000L, Share = 55m, Start = Today(-42), End = new DateTime(2027, 5, 15), Type = CourseType.Group, Status = CourseStatus.Active },
            new { NameAr = "الرياضيات - الثالث المتوسط", NameEn = "Mathematics – Third Intermediate", Grade = (3, GradeBranch.General), Subject = "MAT", Teacher = ahmed, Fee = 200_000L, Share = 55m, Start = Today(-40), End = new DateTime(2027, 5, 1), Type = CourseType.Group, Status = CourseStatus.Active },
            new { NameAr = "اللغة العربية - السادس الأدبي", NameEn = "Arabic – Sixth Literary", Grade = (6, GradeBranch.Literary), Subject = "ARB", Teacher = yousif, Fee = 250_000L, Share = 55m, Start = Today(-38), End = new DateTime(2027, 5, 15), Type = CourseType.Group, Status = CourseStatus.Active },
            new { NameAr = "الأحياء - الخامس العلمي", NameEn = "Biology – Fifth Scientific", Grade = (5, GradeBranch.Scientific), Subject = "BIO", Teacher = noor, Fee = 250_000L, Share = 55m, Start = Today(-36), End = new DateTime(2027, 5, 10), Type = CourseType.Group, Status = CourseStatus.Active },
            new { NameAr = "اللغة الإنجليزية - الثالث المتوسط", NameEn = "English – Third Intermediate", Grade = (3, GradeBranch.General), Subject = "ENG", Teacher = kanaan, Fee = 180_000L, Share = 50m, Start = Today(-34), End = new DateTime(2027, 5, 1), Type = CourseType.Group, Status = CourseStatus.Active },
            new { NameAr = "الحاسوب - الرابع العلمي", NameEn = "Computer Science – Fourth Scientific", Grade = (4, GradeBranch.Scientific), Subject = "CMP", Teacher = lina, Fee = 150_000L, Share = 50m, Start = Today(-12), End = new DateTime(2027, 4, 30), Type = CourseType.Group, Status = CourseStatus.Active },
            new { NameAr = "الرياضيات - السادس الابتدائي", NameEn = "Mathematics – Sixth Primary", Grade = (6, GradeBranch.General), Subject = "MAT", Teacher = huda, Fee = 150_000L, Share = 60m, Start = Today(-10), End = new DateTime(2027, 4, 30), Type = CourseType.Group, Status = CourseStatus.Active },
            new { NameAr = "مراجعة رياضيات مكثفة - السادس العلمي", NameEn = "Intensive Math Revision – Sixth Scientific", Grade = (6, GradeBranch.Scientific), Subject = "MAT", Teacher = ahmed, Fee = 350_000L, Share = 55m, Start = new DateTime(2026, 7, 1), End = new DateTime(2026, 8, 25), Type = CourseType.Group, Status = CourseStatus.Completed },
            new { NameAr = "الفيزياء - الخامس العلمي", NameEn = "Physics – Fifth Scientific", Grade = (5, GradeBranch.Scientific), Subject = "PHY", Teacher = omar, Fee = 250_000L, Share = 55m, Start = Today(-30), End = new DateTime(2027, 5, 10), Type = CourseType.Group, Status = CourseStatus.Active }
        };
        DateTime Today(int days) => DateTime.Today.AddDays(days);
        var courses = new List<Course>();
        foreach (var c in courseSeeds)
        {
            var grade = G(c.Grade.Item1, c.Grade.Item2);
            courses.Add(new Course
            {
                Code = $"C-2026-{courses.Count + 1:D3}",
                NameAr = c.NameAr, NameEn = c.NameEn,
                StageId = grade.StageId, GradeId = grade.Id,
                SubjectId = Sub(c.Subject).Id, TeacherId = c.Teacher.Id,
                Type = c.Type, Fee = c.Fee, TeacherSharePercent = c.Share,
                StartDate = c.Start, EndDate = c.End,
                Capacity = 30, Status = c.Status,
                BranchId = mainBranch.Id,
                DescriptionAr = "دورة تقوية منهجية تشمل حل الأسئلة الوزارية والامتحانات التجريبية مع متابعة أسبوعية لأولياء الأمور.",
                DescriptionEn = "Structured revision course with past-paper practice and weekly guardian follow-up."
            });
        }
        db.Courses.AddRange(courses);
        await db.SaveChangesAsync();
        Course C(string nameArContains) => courses.Single(c => c.NameAr.Contains(nameArContains));
        var cMath6 = C("الرياضيات - السادس العلمي");
        var cPhys6 = C("الفيزياء - السادس العلمي");
        var cChem6 = C("الكيمياء - السادس العلمي");
        var cEng6 = C("اللغة الإنجليزية - السادس العلمي");
        var cMath3 = C("الرياضيات - الثالث المتوسط");
        var cArabic6Lit = C("اللغة العربية - السادس الأدبي");
        var cBio5 = C("الأحياء - الخامس العلمي");
        var cEng3 = C("اللغة الإنجليزية - الثالث المتوسط");
        var cComputer4 = C("الحاسوب - الرابع العلمي");
        var cMathPrimary = C("الرياضيات - السادس الابتدائي");
        var cSummer = C("مراجعة رياضيات مكثفة");
        var cPhys5 = C("الفيزياء - الخامس العلمي");

        var sections = new List<CourseSection>();
        CourseSection MakeSection(Course course, string name, int capacity = 30, bool schedule = true)
        {
            var section = new CourseSection { CourseId = course.Id, Name = name, TeacherId = course.TeacherId, Capacity = capacity };
            sections.Add(section);
            return section;
        }

        var sMath6 = MakeSection(cMath6, "شعبة أ");
        MakeSection(cMath6, "شعبة ب", 28);
        var sPhys6 = MakeSection(cPhys6, "شعبة أ");
        var sChem6 = MakeSection(cChem6, "شعبة أ");
        var sEng6 = MakeSection(cEng6, "شعبة أ");
        MakeSection(cEng6, "شعبة ب", 25);
        var sMath3 = MakeSection(cMath3, "شعبة أ");
        var sArabic6Lit = MakeSection(cArabic6Lit, "شعبة أ");
        var sBio5 = MakeSection(cBio5, "شعبة أ");
        var sEng3 = MakeSection(cEng3, "شعبة أ");
        var sComputer4 = MakeSection(cComputer4, "شعبة أ");
        var sMathPrimary = MakeSection(cMathPrimary, "شعبة أ");
        var sSummer = MakeSection(cSummer, "الشعبة الصيفية", 30, false);
        var sPhys5 = MakeSection(cPhys5, "شعبة أ");
        db.CourseSections.AddRange(sections);
        await db.SaveChangesAsync();

        void AddSchedule(CourseSection section, Classroom room, DayOfWeek day, string start, string end) =>
            db.ClassSchedules.Add(new ClassSchedule { SectionId = section.Id, ClassroomId = room.Id, DayOfWeek = day, StartTime = start, EndTime = end });

        foreach (var day in new[] { DayOfWeek.Saturday, DayOfWeek.Monday, DayOfWeek.Wednesday })
            AddSchedule(sMath6, Room("101"), day, "16:00", "17:30");
        foreach (var day in new[] { DayOfWeek.Saturday, DayOfWeek.Monday, DayOfWeek.Wednesday })
            AddSchedule(sPhys6, Room("102"), day, "17:30", "19:00");
        foreach (var day in new[] { DayOfWeek.Sunday, DayOfWeek.Tuesday, DayOfWeek.Thursday })
            AddSchedule(sChem6, Room("201"), day, "16:00", "17:30");
        foreach (var day in new[] { DayOfWeek.Sunday, DayOfWeek.Tuesday, DayOfWeek.Thursday })
            AddSchedule(sEng6, Room("102"), day, "17:30", "19:00");
        foreach (var day in new[] { DayOfWeek.Saturday, DayOfWeek.Monday })
            AddSchedule(sMath3, Room("202"), day, "19:00", "20:30");
        foreach (var day in new[] { DayOfWeek.Sunday, DayOfWeek.Tuesday })
            AddSchedule(sArabic6Lit, Room("201"), day, "19:00", "20:30");
        foreach (var day in new[] { DayOfWeek.Saturday, DayOfWeek.Tuesday, DayOfWeek.Thursday })
            AddSchedule(sBio5, day == DayOfWeek.Saturday ? Room("201") : Room("202"), day, "16:00", "17:30");
        foreach (var day in new[] { DayOfWeek.Sunday, DayOfWeek.Tuesday, DayOfWeek.Thursday })
            AddSchedule(sEng3, Room("101"), day, "16:00", "17:30");
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Wednesday })
            AddSchedule(sComputer4, Room("LAB-1"), day, "16:00", "17:30");
        foreach (var day in new[] { DayOfWeek.Saturday, DayOfWeek.Monday })
            AddSchedule(sMathPrimary, Room("102"), day, "15:00", "16:00");
        foreach (var day in new[] { DayOfWeek.Sunday, DayOfWeek.Wednesday })
            AddSchedule(sPhys5, Room("102"), day, "19:00", "20:30");
        await db.SaveChangesAsync();

        // ---- Enrollments, dues, payments, teacher earnings, receipts ----
        var receiptSeq = 0;
        var rnd = new Random(20261026);
        var parentUserIds = parents.ToDictionary(p => p.Id, p => p.UserId);
        async Task<Payment> AddPayment(Enrollment enrollment, Due due, long amount, PaymentMethod method, DateTime date, string note)
        {
            receiptSeq++;
            var fund = method == PaymentMethod.Cash ? cashFund : bankFund;
            var payment = new Payment
            {
                ReceiptNo = $"RC-{date.Year}-{receiptSeq:D4}",
                StudentId = enrollment.StudentId,
                EnrollmentId = enrollment.Id,
                DueId = due.Id,
                Amount = amount,
                Method = method,
                Date = date.Date,
                Notes = note,
                ReceivedByUserId = registrarUser.Id,
                ReceivedByName = registrarUser.FullName
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();
            due.PaidAmount += amount;
            due.Status = due.PaidAmount >= due.Amount ? DueStatus.Paid : due.PaidAmount > 0 ? DueStatus.Partial : DueStatus.Unpaid;
            fund.CurrentBalance += amount;
            var share = (long)Math.Round(amount * enrollment.Course.TeacherSharePercent / 100m);
            var earning = new TeacherEarning
            {
                TeacherId = enrollment.Course.TeacherId,
                CourseId = enrollment.CourseId,
                EnrollmentId = enrollment.Id,
                PaymentId = payment.Id,
                Kind = TeacherEarningKind.CourseShare,
                BaseAmount = amount,
                SharePercent = enrollment.Course.TeacherSharePercent,
                Amount = share,
                Date = date.Date
            };
            db.TeacherEarnings.Add(earning);
            await db.SaveChangesAsync();
            await accounting.PostAsync(date, $"قبض رسوم الطالب {students.Single(s => s.Id == enrollment.StudentId).FullNameAr} - {enrollment.Course.NameAr}",
                "Payment", payment.Id.ToString(), new[]
                {
                    new JournalDraft(method == PaymentMethod.Cash ? CoA.Cash : CoA.Bank, amount, 0),
                    new JournalDraft(CoA.TuitionFor(enrollment.Course.Stage.Type), 0, amount),
                    new JournalDraft(CoA.TeacherShare, share, 0),
                    new JournalDraft(CoA.TeacherPayable, 0, share)
                }, registrarUser.FullName, registrarUser.Id);
            var guardian = parentUserIds.GetValueOrDefault(students.Single(s => s.Id == enrollment.StudentId).ParentId ?? 0);
            if (guardian is not null)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = guardian,
                    TitleAr = "تم تسجيل دفعة جديدة", TitleEn = "New payment received",
                    BodyAr = $"تم قبض {Amount(amount)} من رسوم {enrollment.Course.NameAr}. رقم الإيصال {payment.ReceiptNo}.",
                    BodyEn = $"We received {Amount(amount)} for {enrollment.Course.NameEn}. Receipt {payment.ReceiptNo}.",
                    Url = $"/Payments/Receipt/{payment.Id}", Type = NotificationType.Success,
                    CreatedAt = date.AddHours(10)
                });
            }
            return payment;
        }
        string Amount(long value) => $"{value:N0} د.ع";

        var enrollments = new List<Enrollment>();
        async Task Enroll(Student student, Course course, CourseSection? section, DateTime date, long discount, double paidRatio, bool summer = false)
        {
            var net = course.Fee - discount;
            var enrollment = new Enrollment
            {
                StudentId = student.Id, Course = course, CourseId = course.Id, SectionId = section?.Id,
                EnrollmentDate = date.Date, Fee = course.Fee, Discount = discount, NetFee = net,
                Status = summer ? EnrollmentStatus.Completed : EnrollmentStatus.Active,
                Notes = discount > 0 ? $"خصم {discount:N0} د.ع (حالة اجتماعية)" : null
            };
            db.Enrollments.Add(enrollment);
            await db.SaveChangesAsync();
            var due = new Due
            {
                StudentId = student.Id, EnrollmentId = enrollment.Id,
                Description = $"قسط {course.NameAr}",
                Amount = net, PaidAmount = 0,
                DueDate = date.AddDays(30).Date, Status = DueStatus.Unpaid
            };
            db.Dues.Add(due);
            await db.SaveChangesAsync();
            if (paidRatio > 0)
            {
                var paid = (long)(Math.Round(net * paidRatio / 5000d) * 5000);
                paid = Math.Min(paid, net);
                await AddPayment(enrollment, due, paid, rnd.Next(4) == 0 ? PaymentMethod.BankTransfer : PaymentMethod.Cash, date, "الدفعة الأولى عند التسجيل");
                if (!summer && paidRatio < 1 && rnd.Next(2) == 0)
                {
                    var remaining = due.Amount - due.PaidAmount;
                    if (remaining > 0)
                    {
                        var extra = Math.Min(remaining, (long)(Math.Round(remaining * 0.5 / 5000d) * 5000));
                        if (extra > 0)
                            await AddPayment(enrollment, due, extra, PaymentMethod.Cash, DateTime.Today.AddDays(-rnd.Next(1, 10)), "دفعة إضافية");
                    }
                }
            }
            enrollments.Add(enrollment);
        }

        foreach (var student in students)
        {
            var level = student.Grade.Level;
            var branch = student.Grade.Branch;
            var enrollmentDate = student.RegistrationDate.AddDays(5);
            if (level == 6 && branch == GradeBranch.Scientific)
            {
                await Enroll(student, cMath6, sMath6, enrollmentDate, student.Id % 5 == 0 ? 20_000 : 0, 0.6 + rnd.NextDouble() * 0.4);
                await Enroll(student, cPhys6, sPhys6, enrollmentDate, 0, 0.7 + rnd.NextDouble() * 0.3);
                await Enroll(student, cEng6, sEng6, enrollmentDate.AddDays(2), 0, 0.6 + rnd.NextDouble() * 0.4);
                if (student.Id % 2 == 0) await Enroll(student, cChem6, sChem6, enrollmentDate.AddDays(4), 0, 0.5 + rnd.NextDouble() * 0.5);
                if (student.Id % 3 == 0) await Enroll(student, cSummer, sSummer, new DateTime(2026, 7, 5), 0, 1, true);
            }
            else if (level == 5 && branch == GradeBranch.Scientific)
            {
                await Enroll(student, cBio5, sBio5, enrollmentDate, 0, 0.7 + rnd.NextDouble() * 0.3);
                await Enroll(student, cPhys5, sPhys5, enrollmentDate.AddDays(1), 0, 0.6 + rnd.NextDouble() * 0.4);
            }
            else if (level == 6 && branch == GradeBranch.Literary)
            {
                await Enroll(student, cArabic6Lit, sArabic6Lit, enrollmentDate, student.Id % 2 == 0 ? 15_000 : 0, 0.7 + rnd.NextDouble() * 0.3);
            }
            else if (level == 3)
            {
                await Enroll(student, cMath3, sMath3, enrollmentDate, 0, 0.65 + rnd.NextDouble() * 0.35);
                await Enroll(student, cEng3, sEng3, enrollmentDate.AddDays(1), 0, 0.5 + rnd.NextDouble() * 0.5);
            }
            else if (level == 4 && branch == GradeBranch.Scientific)
            {
                await Enroll(student, cComputer4, sComputer4, enrollmentDate, 0, 0.8 + rnd.NextDouble() * 0.2);
            }
            else if (level == 6 && branch == GradeBranch.General)
            {
                await Enroll(student, cMathPrimary, sMathPrimary, enrollmentDate, 0, 0.6 + rnd.NextDouble() * 0.4);
            }
        }
        await db.SaveChangesAsync();

        // ---- Attendance ----
        var attendanceRnd = new Random(77);
        var attendanceSet = new HashSet<(int, int, DateTime)>();
        var activeSchedules = await db.ClassSchedules.Include(s => s.Section).ThenInclude(s => s.Course).ToListAsync();
        var allEnrollments = await db.Enrollments.Include(e => e.Student).ToListAsync();
        async Task RecordAttendance(CourseSection section, DateTime date, string recordedByUserId, string recordedByName, bool allowEdited = false)
        {
            var roster = allEnrollments.Where(e => e.SectionId == section.Id && e.Status != EnrollmentStatus.Cancelled).ToList();
            foreach (var e in roster)
            {
                if (!attendanceSet.Add((e.StudentId, section.Id, date.Date))) continue;
                var roll = attendanceRnd.Next(100);
                var status = roll < 82 ? AttendanceStatus.Present : roll < 90 ? AttendanceStatus.Absent : roll < 96 ? AttendanceStatus.Late : AttendanceStatus.Excused;
                db.StudentAttendances.Add(new StudentAttendance
                {
                    StudentId = e.StudentId, CourseId = section.CourseId, SectionId = section.Id,
                    Date = date.Date, Status = status,
                    RecordedByUserId = recordedByUserId, RecordedByName = recordedByName,
                    RecordedAt = date.Date.AddHours(16).AddMinutes(attendanceRnd.Next(0, 20))
                });
            }
            await db.SaveChangesAsync();
        }

        foreach (var schedule in activeSchedules)
        {
            var dates = new List<DateTime>();
            for (var i = 1; i <= 21; i++)
            {
                var date = DateTime.Today.AddDays(-i);
                if (date.DayOfWeek == schedule.DayOfWeek) dates.Add(date);
            }
            foreach (var date in dates.Take(4))
            {
                var isDemoTeacher = schedule.Section.TeacherId == ahmed.Id;
                await RecordAttendance(schedule.Section, date, isDemoTeacher ? teacherUser.Id : "", isDemoTeacher ? ahmed.FullNameAr : teachers.Single(t => t.Id == schedule.Section.TeacherId).FullNameAr);
            }
        }

        // today's attendance for other teachers so the dashboard has live data
        foreach (var schedule in activeSchedules.Where(s => s.DayOfWeek == DateTime.Today.DayOfWeek))
        {
            if (schedule.Section.TeacherId == ahmed.Id) continue;
            await RecordAttendance(schedule.Section, DateTime.Today, "", teachers.Single(t => t.Id == schedule.Section.TeacherId).FullNameAr);
        }

        var attendanceForRequest = await db.StudentAttendances
            .Where(a => a.SectionId == sMath6.Id && a.Status == AttendanceStatus.Absent)
            .OrderByDescending(a => a.Date).FirstOrDefaultAsync();
        if (attendanceForRequest is not null)
        {
            db.AttendanceChangeRequests.Add(new AttendanceChangeRequest
            {
                AttendanceId = attendanceForRequest.Id,
                RequestedByUserId = teacherUser.Id,
                RequestedByName = ahmed.FullNameAr,
                NewStatus = AttendanceStatus.Excused,
                Reason = "الطالب كان مريضاً وأرفق ولي الأمر تقريراً طبياً، نرجو تعديل الحالة إلى غياب بعذر.",
                Status = ChangeRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddHours(-5)
            });
            await db.SaveChangesAsync();
        }

        // ---- Exams & results ----
        var examRnd = new Random(303);
        foreach (var course in new[] { cMath6, cEng6, cMath3, cEng3, cMathPrimary })
        {
            var exam = new Exam
            {
                CourseId = course.Id, NameAr = "الامتحان الشهري الأول", NameEn = "First Monthly Exam",
                Type = ExamType.Monthly, Date = DateTime.Today.AddDays(-8), MaxMark = 100, PassMark = 50
            };
            db.Exams.Add(exam);
            await db.SaveChangesAsync();
            var roster = await db.Enrollments.Where(e => e.CourseId == course.Id).Select(e => e.StudentId).ToListAsync();
            foreach (var studentId in roster)
            {
                db.ExamResults.Add(new ExamResult
                {
                    ExamId = exam.Id, StudentId = studentId,
                    Mark = examRnd.Next(52, 99),
                    Notes = examRnd.Next(8) == 0 ? "يحتاج مراجعة الفصل الثاني" : null
                });
            }
            await db.SaveChangesAsync();
        }

        // ---- Private lessons & ratings ----
        var request1 = new PrivateLessonRequest
        {
            StudentId = students[0].Id, ParentId = parents[0].Id,
            GuardianName = parents[0].FullNameAr, GuardianPhone = parents[0].Phone,
            GradeId = G(6, GradeBranch.Scientific).Id, SubjectId = Sub("MAT").Id,
            PreferredTeacherId = huda.Id, Mode = TeachingMode.Home, Area = "المصلى",
            Address = "كركوك - المصلى - قرب جامع الرحمة", DaysPerWeek = 2,
            PreferredTime = "17:00", DurationMinutes = 90, OfferedFee = 30_000,
            Notes = "الطالب يحتاج تقوية في التفاضل والتكامل قبل الامتحانات.",
            MatchedTeacherId = huda.Id, Status = RequestStatus.Completed, CreatedAt = DateTime.UtcNow.AddDays(-18)
        };
        var request2 = new PrivateLessonRequest
        {
            StudentId = students[12].Id, ParentId = parents[6].Id,
            GuardianName = parents[6].FullNameAr, GuardianPhone = parents[6].Phone,
            GradeId = G(3, GradeBranch.General).Id, SubjectId = Sub("ENG").Id,
            Mode = TeachingMode.Home, Area = "المصلى", Address = "كركوك - المصلى - محلة المعلمين",
            DaysPerWeek = 1, PreferredTime = "16:00", DurationMinutes = 60, OfferedFee = 20_000,
            Notes = "تقوية في قواعد اللغة الإنجليزية.", Status = RequestStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        var request3 = new PrivateLessonRequest
        {
            StudentId = students[2].Id, ParentId = parents[0].Id,
            GuardianName = parents[0].FullNameAr, GuardianPhone = parents[0].Phone,
            GradeId = G(4, GradeBranch.Scientific).Id, SubjectId = Sub("MAT").Id,
            PreferredTeacherId = ahmed.Id, Mode = TeachingMode.Institute, Area = "المصلى",
            DaysPerWeek = 2, PreferredTime = "18:00", DurationMinutes = 90, OfferedFee = 25_000,
            Status = RequestStatus.Pending, CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var request4 = new PrivateLessonRequest
        {
            StudentId = students[1].Id, ParentId = parents[0].Id,
            GuardianName = parents[0].FullNameAr, GuardianPhone = parents[0].Phone,
            GradeId = G(5, GradeBranch.Scientific).Id, SubjectId = Sub("PHY").Id,
            Mode = TeachingMode.Institute, Area = "المصلى",
            DaysPerWeek = 2, PreferredTime = "17:30", DurationMinutes = 90, OfferedFee = 25_000,
            MatchedTeacherId = omar.Id, Status = RequestStatus.Matched,
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        };
        db.PrivateLessonRequests.AddRange(request1, request2, request3, request4);
        await db.SaveChangesAsync();

        var lesson1 = new PrivateLesson
        {
            RequestId = request1.Id, TeacherId = huda.Id, StudentId = students[0].Id, ParentId = parents[0].Id,
            SubjectId = Sub("MAT").Id, GradeId = request1.GradeId, Mode = TeachingMode.Home,
            Area = "المصلى", Address = request1.Address, Date = DateTime.Today.AddDays(-7),
            StartTime = "17:00", DurationMinutes = 90, Fee = 30_000, TeacherSharePercent = 60,
            Status = PrivateLessonStatus.Completed, IsPaid = true, PaidDate = DateTime.Today.AddDays(-7)
        };
        var lesson2 = new PrivateLesson
        {
            RequestId = request1.Id, TeacherId = huda.Id, StudentId = students[0].Id, ParentId = parents[0].Id,
            SubjectId = Sub("MAT").Id, GradeId = request1.GradeId, Mode = TeachingMode.Home,
            Area = "المصلى", Address = request1.Address, Date = DateTime.Today.AddDays(-2),
            StartTime = "17:00", DurationMinutes = 90, Fee = 30_000, TeacherSharePercent = 60,
            Status = PrivateLessonStatus.Completed, IsPaid = true, PaidDate = DateTime.Today.AddDays(-2)
        };
        db.PrivateLessons.AddRange(lesson1, lesson2);
        await db.SaveChangesAsync();
        foreach (var lesson in new[] { lesson1, lesson2 })
        {
            var teacherCut = (long)Math.Round(lesson.Fee * lesson.TeacherSharePercent / 100m);
            db.TeacherEarnings.Add(new TeacherEarning
            {
                TeacherId = lesson.TeacherId, PrivateLessonId = lesson.Id, Kind = TeacherEarningKind.PrivateLesson,
                BaseAmount = lesson.Fee, SharePercent = lesson.TeacherSharePercent, Amount = teacherCut,
                Date = lesson.PaidDate ?? lesson.Date
            });
            var revenueCode = lesson.Mode == TeachingMode.Home ? CoA.HomeLessons : CoA.PrivateCommission;
            cashFund.CurrentBalance += lesson.Fee;
            await accounting.PostAsync(lesson.PaidDate ?? lesson.Date, $"درس خصوصي - {lesson.Subject.NameAr} - {teachers.Single(t => t.Id == lesson.TeacherId).FullNameAr}",
                "PrivateLesson", lesson.Id.ToString(), new[]
                {
                    new JournalDraft(CoA.Cash, lesson.Fee, 0),
                    new JournalDraft(revenueCode, 0, lesson.Fee),
                    new JournalDraft(CoA.TeacherShare, teacherCut, 0),
                    new JournalDraft(CoA.TeacherPayable, 0, teacherCut)
                }, registrarUser.FullName, registrarUser.Id);
        }
        await db.SaveChangesAsync();

        var ratingRnd = new Random(501);
        var comments = new[]
        {
            "شرح ممتاز وأسلوب واضح جداً، استفاد ابني كثيراً.",
            "مدرّس ملتزم بالوقت ويحفّز الطالب على المشاركة.",
            "المتابعة مع ولي الأمر ممتازة والتقرير الأسبوعي مفيد.",
            "تحسن مستوى الطالب بشكل ملموس خلال شهر واحد.",
            "خبرة عالية في المنهج العراقي والأسئلة الوزارية.",
            "تعامل راقٍ مع الطلبة، ننصح به بشدة."
        };
        foreach (var teacher in teachers)
        {
            var baseScore = teacher.Code switch
            {
                "T-2026-002" => 5,
                "T-2026-001" => 5,
                "T-2026-005" => 5,
                _ => 4
            };
            for (var i = 0; i < 5; i++)
            {
                var overall = Math.Clamp(baseScore + (ratingRnd.Next(3) - 1), 3, 5);
                db.TeacherRatings.Add(new TeacherRating
                {
                    TeacherId = teacher.Id,
                    ParentId = parents[ratingRnd.Next(parents.Count)].Id,
                    RatedByUserId = parents[ratingRnd.Next(parents.Count)].UserId ?? "",
                    RatedByName = parents[ratingRnd.Next(parents.Count)].FullNameAr,
                    Explanation = overall, Punctuality = Math.Clamp(overall + ratingRnd.Next(-1, 2), 3, 5),
                    Interaction = overall, FollowUp = Math.Clamp(overall + ratingRnd.Next(-1, 2), 3, 5), Overall = overall,
                    Comment = comments[ratingRnd.Next(comments.Length)],
                    CreatedAt = DateTime.UtcNow.AddDays(-ratingRnd.Next(3, 60))
                });
            }
        }
        db.TeacherRatings.Add(new TeacherRating
        {
            TeacherId = huda.Id, ParentId = parents[0].Id, LessonId = lesson1.Id,
            RatedByUserId = parentUser.Id, RatedByName = parents[0].FullNameAr,
            Explanation = 5, Punctuality = 5, Interaction = 5, FollowUp = 4, Overall = 5,
            Comment = "الدرس المنزلي كان منظمًا جدًا وابني فهم الموضوع بسرعة.", CreatedAt = DateTime.UtcNow.AddDays(-6)
        });
        await db.SaveChangesAsync();
        foreach (var teacher in teachers)
        {
            var ratings = await db.TeacherRatings.Where(r => r.TeacherId == teacher.Id).ToListAsync();
            teacher.RatingCount = ratings.Count;
            teacher.RatingAvg = ratings.Count == 0 ? 0 : Math.Round(ratings.Average(r => (double)r.Average), 1);
        }
        await db.SaveChangesAsync();

        // ---- Teacher rent contracts, cafeteria & bookings ----
        foreach (var (teacher, note) in new[] { (ahmed, "عقد استخدام القاعات للدروس الخصوصية للسادس الإعدادي"), (mariam, "عقد استخدام القاعات للدروس الخصوصية للسادس الإعدادي") })
        {
            var contract = new TeacherRentContract
            {
                TeacherId = teacher.Id, MonthlyAmount = 250_000,
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), IsActive = true,
                Notes = note
            };
            db.TeacherRentContracts.Add(contract);
            await db.SaveChangesAsync();
            foreach (var monthOffset in new[] { -1, 0 })
            {
                var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(monthOffset);
                var rentPayment = new TeacherRentPayment
                {
                    ContractId = contract.Id, Month = month, Amount = 250_000,
                    IsPaid = true, PaidDate = month.AddDays(2), Reference = $"TR-{month:yyyyMM}-{teacher.Id}"
                };
                db.TeacherRentPayments.Add(rentPayment);
                await db.SaveChangesAsync();
                cashFund.CurrentBalance += 250_000;
                await accounting.PostAsync(rentPayment.PaidDate!.Value,
                    $"إيجار تدريس خصوصي - {teacher.FullNameAr} - {month:yyyy/MM}",
                    "TeacherRent", rentPayment.Id.ToString(), new[]
                    {
                        new JournalDraft(CoA.Cash, 250_000, 0),
                        new JournalDraft(CoA.PrepTeacherRent, 0, 250_000)
                    }, accountantUser.FullName, accountantUser.Id);
            }
        }

        var cafeteria = new CafeteriaContract
        {
            TenantName = "مطعم أبو راكان", TenantPhone = "0770 999 8877",
            AnnualAmount = 8_400_000, StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 8, 31),
            IsActive = true, Notes = "إيجار سنوي للكافيتيريا، يدفع شهرياً 700,000 د.ع."
        };
        db.CafeteriaContracts.Add(cafeteria);
        await db.SaveChangesAsync();
        for (var i = 0; i < 2; i++)
        {
            var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-i);
            var pay = new CafeteriaPayment
            {
                ContractId = cafeteria.Id, Month = month, Amount = 700_000,
                IsPaid = true, PaidDate = month.AddDays(3), Reference = $"CAF-{month:yyyyMM}"
            };
            db.CafeteriaPayments.Add(pay);
            await db.SaveChangesAsync();
            cashFund.CurrentBalance += 700_000;
            await accounting.PostAsync(pay.PaidDate!.Value, $"إيجار الكافتيريا لشهر {month:yyyy/MM}", "Cafeteria", pay.Id.ToString(), new[]
            {
                new JournalDraft(CoA.Cash, 700_000, 0),
                new JournalDraft(CoA.CafeteriaRent, 0, 700_000)
            }, accountantUser.FullName, accountantUser.Id);
        }

        var bookingSeeds = new[]
        {
            ("مدرسة النور الأهلية", "ورشة تدريب المعلمين", -32, "10:00", "13:00", "101", 15_000L, true),
            ("مديرية تربية كركوك", "اجتماع لجنة التربية", -21, "09:00", "12:00", "HALL-1", 40_000L, true),
            ("مركز أفق للتدريب", "دورة تطوير مهارات", -9, "14:00", "18:00", "201", 15_000L, true),
            ("الأستاذ كرار العبيدي", "مراجعة خاصة", -4, "20:00", "22:00", "202", 12_000L, true),
            ("شركة الرافدين للتقنية", "عرض تقني", 3, "10:00", "12:00", "HALL-1", 40_000L, false),
            ("مدرسة بابل الأهلية", "امتحان تجريبي", 6, "09:00", "13:00", "101", 15_000L, false)
        };
        foreach (var (tenant, purpose, offset, start, end, roomNo, rate, paid) in bookingSeeds)
        {
            var date = DateTime.Today.AddDays(offset);
            var hours = (TimeSpan.Parse(end) - TimeSpan.Parse(start)).TotalHours;
            var booking = new ClassroomBooking
            {
                ClassroomId = Room(roomNo).Id, TenantName = tenant, Purpose = purpose,
                Date = date, StartTime = start, EndTime = end, HourlyRate = rate,
                TotalAmount = (long)(hours * rate), PaymentStatus = paid ? BookingPaymentStatus.Paid : BookingPaymentStatus.Unpaid,
                CreatedByUserId = registrarUser.Id, Notes = paid ? "تم إصدار سند قبض." : null
            };
            db.ClassroomBookings.Add(booking);
            await db.SaveChangesAsync();
            if (paid)
            {
                cashFund.CurrentBalance += booking.TotalAmount;
                await accounting.PostAsync(date, $"إيجار قاعة - {tenant}", "ClassroomBooking", booking.Id.ToString(), new[]
                {
                    new JournalDraft(CoA.Cash, booking.TotalAmount, 0),
                    new JournalDraft(CoA.HallRent, 0, booking.TotalAmount)
                }, registrarUser.FullName, registrarUser.Id);
            }
        }

        // ---- Other income, expenses ----
        var otherIncome = new Income
        {
            IncomeNo = "INC-2026-0001", AccountId = accountByCode[CoA.OtherRevenue].Id, FundId = cashFund.Id,
            Date = DateTime.Today.AddDays(-20), Amount = 150_000, Method = PaymentMethod.Cash,
            Source = "رعاية نشاط طلابي", Description = "مساهمة من أحد أولياء الأمور في نشاط المعهد",
            CreatedByUserId = accountantUser.Id, CreatedByName = accountantUser.FullName
        };
        db.Incomes.Add(otherIncome);
        cashFund.CurrentBalance += otherIncome.Amount;
        await db.SaveChangesAsync();
        await accounting.PostAsync(otherIncome.Date, $"إيراد آخر - {otherIncome.Source}", "Income", otherIncome.Id.ToString(),
            new[] { new JournalDraft(CoA.Cash, otherIncome.Amount, 0), new JournalDraft(CoA.OtherRevenue, 0, otherIncome.Amount) },
            accountantUser.FullName, accountantUser.Id);

        var expenseSeeds = new[]
        {
            ("5900", "صيانة مكيفات القاعات", -55, 500_000L, "شركة كركوك للتبريد", (int?)null, ExpenseStatus.Approved),
            ("5400", "فاتورة كهرباء أيلول", -24, 750_000L, "مديرية كهرباء كركوك", null, ExpenseStatus.Approved),
            ("5700", "وقود المولدة", -12, 900_000L, "محطة وقود القادسية", null, ExpenseStatus.Approved),
            ("5600", "اشتراك الإنترنت", -20, 150_000L, "شركة أور للاتصالات", null, ExpenseStatus.Approved),
            ("5500", "فاتورة الماء", -18, 75_000L, "مديرية ماء كركوك", null, ExpenseStatus.Approved),
            ("6000", "قرطاسية وطباعة", -28, 180_000L, "مكتبة الأمل", null, ExpenseStatus.Approved),
            ("5800", "أجور تنظيف", -10, 250_000L, "شركة النظافة الحديثة", null, ExpenseStatus.Approved),
            ("6100", "لوحات إعلانية", -48, 300_000L, "وكالة كركوك للإعلان", null, ExpenseStatus.Approved),
            ("6200", "مصاريف إدارية", -8, 200_000L, "متنوع", null, ExpenseStatus.Approved),
            ("5900", "صيانة مختبر الفيزياء", -6, 120_000L, "ورشة المعرفة", cPhys6.Id, ExpenseStatus.Approved),
            ("5300", "أجور خدمات ونقل", -9, 400_000L, "مكتب خدمات المصلى", null, ExpenseStatus.Approved),
            ("6300", "مصاريف نثرية", -2, 60_000L, "متنوع", null, ExpenseStatus.Draft)
        };
        var expenseSeq = 0;
        foreach (var (code, desc, offset, amount, vendor, courseId, status) in expenseSeeds)
        {
            expenseSeq++;
            var date = DateTime.Today.AddDays(offset);
            var expense = new Expense
            {
                ExpenseNo = $"EXP-2026-{expenseSeq:D4}", AccountId = accountByCode[code].Id, FundId = cashFund.Id,
                Date = date, Amount = amount, Method = PaymentMethod.Cash, Vendor = vendor,
                Description = desc, Status = status, CourseId = courseId,
                CreatedByUserId = accountantUser.Id, CreatedByName = accountantUser.FullName,
                ApprovedAt = status == ExpenseStatus.Approved ? date : null
            };
            db.Expenses.Add(expense);
            await db.SaveChangesAsync();
            if (status == ExpenseStatus.Approved)
            {
                cashFund.CurrentBalance -= amount;
                await accounting.PostAsync(date, $"{desc} - {vendor}", "Expense", expense.Id.ToString(), new[]
                {
                    new JournalDraft(code, amount, 0),
                    new JournalDraft(CoA.Cash, 0, amount)
                }, accountantUser.FullName, accountantUser.Id);
            }
        }

        // ---- Employees, attendance, payroll ----
        var employeeSeeds = new[]
        {
            ("إدارة", "مدير إداري", "Administrative Manager", "ليث سمير عبد الله", "Laith Samir Abdullah", "0770 501 2233", 1_200_000L, 200_000L, 4),
            ("المحاسبة", "محاسب", "Accountant", "سعدية محمد حسن", "Saadia Mohammed Hassan", "0771 502 4455", 900_000L, 100_000L, 3),
            ("التسجيل", "مسؤول تسجيل", "Registrar", "رنا وليد إبراهيم", "Rana Waleed Ibrahim", "0772 503 6677", 750_000L, 75_000L, 2),
            ("الاستقبال", "موظفة استقبال", "Receptionist", "إسراء عادل جميل", "Israa Adel Jameel", "0770 504 8899", 550_000L, 50_000L, 1),
            ("الخدمات", "حارس أمن", "Security guard", "خالد سلوم عباس", "Khalid Saloom Abbas", "0771 505 1122", 500_000L, 0L, 6),
            ("الخدمات", "عاملة نظافة", "Cleaner", "أمينة حسن علي", "Amina Hassan Ali", "0772 506 3344", 450_000L, 0L, 3),
            ("المختبر", "فني مختبر", "Lab technician", "مصطفى كمال ياسين", "Mustafa Kamal Yaseen", "0770 507 5566", 650_000L, 50_000L, 2),
            ("الإدارة", "مساعد إداري", "Administrative assistant", "شهد وليد مهدي", "Shahad Waleed Mahdi", "0771 508 7788", 600_000L, 50_000L, 2)
        };
        var employees = new List<Employee>();
        foreach (var e in employeeSeeds)
        {
            employees.Add(new Employee
            {
                Code = $"E-2026-{employees.Count + 1:D3}", FullNameAr = e.Item4, FullNameEn = e.Item5,
                JobTitleAr = e.Item2, JobTitleEn = e.Item3, Department = e.Item1,
                Phone = e.Item6, Email = $"staff{employees.Count + 1}@kirkuk-institute.iq",
                BasicSalary = e.Item7, Allowances = e.Item8,
                HireDate = new DateTime(2024, 9, 1).AddMonths(employees.Count),
                Status = StaffStatus.Active
            });
        }
        db.Employees.AddRange(employees);
        await db.SaveChangesAsync();
        employees[1].UserId = accountantUser.Id;
        employees[2].UserId = registrarUser.Id;
        employees[0].UserId = hrUser.Id;
        await db.SaveChangesAsync();

        var empRnd = new Random(99);
        for (var d = 0; d < 10; d++)
        {
            var date = DateTime.Today.AddDays(-d);
            if (date.DayOfWeek == DayOfWeek.Friday) continue;
            foreach (var employee in employees)
            {
                var status = empRnd.Next(100) < 93 ? AttendanceStatus.Present : empRnd.Next(2) == 0 ? AttendanceStatus.Late : AttendanceStatus.Absent;
                db.EmployeeAttendances.Add(new EmployeeAttendance
                {
                    EmployeeId = employee.Id, Date = date, Status = status,
                    CheckIn = status == AttendanceStatus.Absent ? null : status == AttendanceStatus.Late ? "08:25" : "08:00",
                    CheckOut = status == AttendanceStatus.Absent ? null : "14:00"
                });
            }
        }
        await db.SaveChangesAsync();

        var payrollTotalSept = 0L;
        var payrollTotalOct = 0L;
        foreach (var monthOffset in new[] { -1, 0 })
        {
            var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(monthOffset);
            var paidAt = monthOffset == -1 ? month.AddMonths(1).AddDays(-1) : DateTime.Today.AddDays(-2);
            foreach (var employee in employees)
            {
                var absenceDays = empRnd.Next(0, 3);
                var deduction = (long)(employee.BasicSalary / 30m * absenceDays);
                var overtime = empRnd.Next(0, 3) * 25_000L;
                var otherDeductions = empRnd.Next(2) == 0 ? 25_000L : 0L;
                var net = employee.BasicSalary + employee.Allowances + overtime - deduction - otherDeductions;
                db.Payrolls.Add(new Payroll
                {
                    EmployeeId = employee.Id, Month = month,
                    Basic = employee.BasicSalary, Allowances = employee.Allowances,
                    Overtime = overtime, AbsenceDeduction = deduction,
                    OtherDeductions = otherDeductions, Net = net,
                    Status = PayrollStatus.Paid, PaidAt = paidAt
                });
                if (monthOffset == -1) payrollTotalSept += net; else payrollTotalOct += net;
            }
        }
        await db.SaveChangesAsync();
        bankFund.CurrentBalance -= payrollTotalSept + payrollTotalOct;
        var payrollMonthSept = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
        var payrollMonthOct = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var payrollPaidSept = payrollMonthSept.AddMonths(1).AddDays(-1);
        var payrollPaidOct = DateTime.Today.AddDays(-2);
        await accounting.PostAsync(payrollPaidSept, $"رواتب الموظفين لشهر {payrollMonthSept:yyyy/MM}",
            "Payroll", payrollMonthSept.ToString("yyyyMM"), new[]
            {
                new JournalDraft(CoA.Salaries, payrollTotalSept, 0),
                new JournalDraft(CoA.Bank, 0, payrollTotalSept)
            }, accountantUser.FullName, accountantUser.Id);
        await accounting.PostAsync(payrollPaidOct, $"رواتب الموظفين لشهر {payrollMonthOct:yyyy/MM}",
            "Payroll", payrollMonthOct.ToString("yyyyMM"), new[]
            {
                new JournalDraft(CoA.Salaries, payrollTotalOct, 0),
                new JournalDraft(CoA.Bank, 0, payrollTotalOct)
            }, accountantUser.FullName, accountantUser.Id);

        // ---- Assets ----
        db.Assets.AddRange(
            new Asset { AssetNo = "FA-0001", NameAr = "شاشة عرض تفاعلية 75 بوصة", NameEn = "75\" interactive display", Category = "أجهزة تعليمية", PurchaseDate = new DateTime(2025, 9, 10), Cost = 1_800_000, SalvageValue = 200_000, UsefulLifeYears = 5, Location = "قاعة النخبة", Responsible = "إدارة المعهد" },
            new Asset { AssetNo = "FA-0002", NameAr = "حاسوب مكتبي - التسجيل", NameEn = "Desktop computer – registrar", Category = "أجهزة حاسوب", PurchaseDate = new DateTime(2024, 11, 2), Cost = 950_000, SalvageValue = 100_000, UsefulLifeYears = 4, Location = "مكتب التسجيل", Responsible = "رنا وليد" },
            new Asset { AssetNo = "FA-0003", NameAr = "طابعة ليزر", NameEn = "Laser printer", Category = "طابعات", PurchaseDate = new DateTime(2025, 2, 15), Cost = 450_000, SalvageValue = 50_000, UsefulLifeYears = 4, Location = "المحاسبة", Responsible = "سعدية محمد" },
            new Asset { AssetNo = "FA-0004", NameAr = "مولد كهربائي 60 كيلو", NameEn = "60 KVA generator", Category = "مولدات", PurchaseDate = new DateTime(2024, 6, 1), Cost = 6_500_000, SalvageValue = 1_000_000, UsefulLifeYears = 10, Location = "ساحة المعهد", Responsible = "قسم الخدمات" }
        );
        await db.SaveChangesAsync();

        // ---- Audit & notifications ----
        var auditEntries = new (string Action, string Entity, string Name, DateTime When)[]
        {
            ("تسجيل طالب جديد", "Student", students[0].FullNameAr, DateTime.UtcNow.AddDays(-8)),
            ("تعديل بيانات طالب", "Student", students[3].FullNameAr, DateTime.UtcNow.AddDays(-6)),
            ("إصدار سند قبض", "Payment", "RC-2026-0001", DateTime.UtcNow.AddDays(-5)),
            ("اعتماد مصروف", "Expense", "صيانة مكيفات القاعات", DateTime.UtcNow.AddDays(-4)),
            ("إضافة دورة جديدة", "Course", cComputer4.NameAr, DateTime.UtcNow.AddDays(-3)),
            ("تعديل نسبة مدرس", "Course", cMathPrimary.NameAr, DateTime.UtcNow.AddDays(-2)),
            ("إنشاء مستخدم", "User", "teacher@kirkuk.edu.iq", DateTime.UtcNow.AddDays(-1)),
            ("تعديل حضور سابق", "AttendanceChangeRequest", "طلب تعديل قيد", DateTime.UtcNow.AddHours(-5))
        };
        foreach (var (action, entity, name, when) in auditEntries)
        {
            db.AuditLogs.Add(new AuditLog
            {
                UserId = managerUser.Id, UserName = managerUser.FullName,
                Action = action, EntityName = entity, EntityId = "seed",
                NewValues = $"{{\"name\":\"{name}\"}}", CreatedAt = when,
                IpAddress = "127.0.0.1"
            });
        }
        db.Notifications.AddRange(
            new Notification
            {
                UserId = adminUser.Id, TitleAr = "ذمم طلاب متأخرة", TitleEn = "Overdue student dues",
                BodyAr = "توجد أقساط متأخرة تحتاج متابعة من قسم المحاسبة.", BodyEn = "Overdue tuition installments require accounting follow-up.",
                Url = "/Dues", Type = NotificationType.Danger, CreatedAt = DateTime.UtcNow.AddHours(-3)
            },
            new Notification
            {
                UserId = managerUser.Id, TitleAr = "طلب تعديل حضور", TitleEn = "Attendance change request",
                BodyAr = "المدرس أحمد الجبوري أرسل طلب تعديل حالة طالب إلى غياب بعذر.", BodyEn = "Teacher Ahmed submitted a change request to mark a student excused.",
                Url = "/Attendance/Requests", Type = NotificationType.Warning, CreatedAt = DateTime.UtcNow.AddHours(-5)
            },
            new Notification
            {
                UserId = teacherUser.Id, TitleAr = "تذكير بحصة اليوم", TitleEn = "Today's class reminder",
                BodyAr = "لديك حصة الرياضيات للسادس العلمي الساعة 4:00 عصراً في قاعة النخبة.", BodyEn = "Your Sixth Scientific mathematics class starts at 4:00 PM in Elite Hall.",
                Url = "/Teacher", Type = NotificationType.Info, CreatedAt = DateTime.UtcNow.AddHours(-4)
            },
            new Notification
            {
                UserId = parentUser.Id, TitleAr = "تم قبض القسط", TitleEn = "Installment received",
                BodyAr = "شكراً لكم، تم تسجيل دفعة من رسوم ابنكم عمار. يمكنكم مراجعة الإيصال من بوابة ولي الأمر.", BodyEn = "Thank you, a payment for your son Ammar has been recorded. Review it in the parent portal.",
                Url = "/Parent", Type = NotificationType.Success, CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Notification
            {
                UserId = teacherUser.Id, TitleAr = "طلب درس منزلي جديد", TitleEn = "New home lesson request",
                BodyAr = "هناك طلب تقوية في الرياضيات للسادس العلمي في منطقة المصلى.", BodyEn = "A new mathematics tutoring request is available in Al-Musalla.",
                Url = "/PrivateLessons", Type = NotificationType.Info, CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        );
        await db.SaveChangesAsync();

        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        static async Task<ApplicationUser> EnsureUser(UserManager<ApplicationUser> userManager, string email, string password, string fullName, string role)
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is not null) return existing;
            var user = new ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                FullName = fullName, IsActive = true, PreferredLanguage = "ar",
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Could not seed user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            await userManager.AddToRoleAsync(user, role);
            return user;
        }
    }
}
