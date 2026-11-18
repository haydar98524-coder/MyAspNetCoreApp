using KirkukInstitute.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace KirkukInstitute.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>Used by derived contexts (e.g. the read-only AI context) that carry their own options type.</summary>
    protected AppDbContext(DbContextOptions options) : base(options) { }

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<InstituteSettings> Settings => Set<InstituteSettings>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseSection> CourseSections => Set<CourseSection>();
    public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentNumberCounter> StudentNumberCounters => Set<StudentNumberCounter>();
    public DbSet<StudentSubject> StudentSubjects => Set<StudentSubject>();
    public DbSet<StudentTeacher> StudentTeachers => Set<StudentTeacher>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();
    public DbSet<TeacherGrade> TeacherGrades => Set<TeacherGrade>();
    public DbSet<TeacherCertificate> TeacherCertificates => Set<TeacherCertificate>();
    public DbSet<TeacherAvailability> TeacherAvailabilities => Set<TeacherAvailability>();
    public DbSet<TeacherRating> TeacherRatings => Set<TeacherRating>();
    public DbSet<TeacherEarning> TeacherEarnings => Set<TeacherEarning>();
    public DbSet<TeacherRentContract> TeacherRentContracts => Set<TeacherRentContract>();
    public DbSet<TeacherRentPayment> TeacherRentPayments => Set<TeacherRentPayment>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeAttendance> EmployeeAttendances => Set<EmployeeAttendance>();
    public DbSet<Payroll> Payrolls => Set<Payroll>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<ClassroomBooking> ClassroomBookings => Set<ClassroomBooking>();
    public DbSet<CafeteriaContract> CafeteriaContracts => Set<CafeteriaContract>();
    public DbSet<CafeteriaPayment> CafeteriaPayments => Set<CafeteriaPayment>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<StudentAttendance> StudentAttendances => Set<StudentAttendance>();
    public DbSet<AttendanceChangeRequest> AttendanceChangeRequests => Set<AttendanceChangeRequest>();
    public DbSet<PrivateLessonRequest> PrivateLessonRequests => Set<PrivateLessonRequest>();
    public DbSet<PrivateLesson> PrivateLessons => Set<PrivateLesson>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<Fund> Funds => Set<Fund>();
    public DbSet<Due> Dues => Set<Due>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Asset> Assets => Set<Asset>();

    // ── Phase 1: financial governance ────────────────────────────────────────
    public DbSet<DocumentNumberCounter> DocumentNumberCounters => Set<DocumentNumberCounter>();
    public DbSet<TuitionContract> TuitionContracts => Set<TuitionContract>();
    public DbSet<CashierShift> CashierShifts => Set<CashierShift>();
    public DbSet<CashierDenomination> CashierDenominations => Set<CashierDenomination>();
    public DbSet<TeacherContract> TeacherContracts => Set<TeacherContract>();
    public DbSet<TeacherSettlement> TeacherSettlements => Set<TeacherSettlement>();
    public DbSet<TeacherSettlementLine> TeacherSettlementLines => Set<TeacherSettlementLine>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Percentages, marks and averages: two decimal places is enough everywhere in the model.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Arabic collation: correct alphabetical ordering for Arabic names, case- and kana-insensitive,
        // accent-sensitive so different Arabic letters with diacritics are not merged.
        b.UseCollation("Arabic_100_CI_AS");

        b.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(150);
        });

        // SQL Server cannot index nvarchar(max) columns, so every indexed code / number gets a length.
        var studentNo = b.Entity<Student>().Property(x => x.StudentNo);
        studentNo.HasMaxLength(32);
        // Allocated once at registration, immutable afterwards. EF ignores later changes; the
        // database trigger in StudentRegistrationSchema enforces the same rule for raw SQL too.
        studentNo.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        b.Entity<Student>().Property(x => x.QrToken).HasMaxLength(64);
        b.Entity<Teacher>().Property(x => x.Code).HasMaxLength(32);
        b.Entity<Employee>().Property(x => x.Code).HasMaxLength(32);
        b.Entity<Course>().Property(x => x.Code).HasMaxLength(32);
        b.Entity<Account>().Property(x => x.Code).HasMaxLength(32);
        b.Entity<Payment>().Property(x => x.ReceiptNo).HasMaxLength(32);
        b.Entity<JournalEntry>().Property(x => x.EntryNo).HasMaxLength(32);
        b.Entity<Expense>().Property(x => x.ExpenseNo).HasMaxLength(32);
        b.Entity<Income>().Property(x => x.IncomeNo).HasMaxLength(32);

        b.Entity<InstituteSettings>().HasData(new InstituteSettings { Id = 1 });

        b.Entity<Student>(e =>
        {
            e.Property(x => x.NationalId).HasMaxLength(30);
            e.HasIndex(x => x.StudentNo).IsUnique();
            // Filtered unique index: old rows may not have a national ID yet, new registrations must be unique.
            e.HasIndex(x => x.NationalId).IsUnique().HasFilter("[NationalId] IS NOT NULL");
            e.HasIndex(x => x.QrToken).IsUnique();
            e.HasOne(x => x.Hall).WithMany()
                .HasForeignKey(x => x.HallId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Parent).WithMany(p => p.Children)
                .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Grade).WithMany(g => g.Students)
                .HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<StudentNumberCounter>(e =>
        {
            e.HasKey(x => x.Year);
            e.Property(x => x.Year).ValueGeneratedNever();
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        b.Entity<StudentSubject>(e =>
        {
            e.HasIndex(x => new { x.StudentId, x.SubjectId }).IsUnique();
            e.HasOne(x => x.Student).WithMany(s => s.Subjects)
                .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Subject).WithMany()
                .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<StudentTeacher>(e =>
        {
            e.HasIndex(x => new { x.StudentId, x.TeacherId }).IsUnique();
            e.HasOne(x => x.Student).WithMany(s => s.Teachers)
                .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Teacher).WithMany()
                .HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Teacher>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
        });

        b.Entity<TeacherSubject>(e =>
        {
            e.HasIndex(x => new { x.TeacherId, x.SubjectId }).IsUnique();
            e.HasOne(x => x.Teacher).WithMany(t => t.Subjects).HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TeacherGrade>(e =>
        {
            e.HasIndex(x => new { x.TeacherId, x.GradeId }).IsUnique();
            e.HasOne(x => x.Teacher).WithMany(t => t.Grades).HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Grade).WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Course>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Teacher).WithMany(t => t.Courses).HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Stage).WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Grade).WithMany(g => g.Courses).HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subject).WithMany(s => s.Courses).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CourseSection>(e =>
        {
            e.HasOne(x => x.Course).WithMany(c => c.Sections).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<ClassSchedule>(e =>
        {
            e.HasOne(x => x.Section).WithMany(s => s.Schedules).HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Classroom).WithMany(c => c.Schedules).HasForeignKey(x => x.ClassroomId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Enrollment>(e =>
        {
            e.HasOne(x => x.Student).WithMany(s => s.Enrollments).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Course).WithMany(c => c.Enrollments).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Section).WithMany(s => s.Enrollments).HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Due>(e =>
        {
            e.HasOne(x => x.Enrollment).WithMany(x => x.Dues).HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany(s => s.Dues).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Contract).WithMany(c => c.Installments)
                .HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.ClientSetNull);
            e.HasIndex(x => new { x.ContractId, x.InstallmentNo });
        });

        b.Entity<Payment>(e =>
        {
            e.HasIndex(x => x.ReceiptNo).IsUnique();
            e.HasOne(x => x.Student).WithMany(s => s.Payments).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Enrollment).WithMany(x => x.Payments).HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Due).WithMany(x => x.Payments).HasForeignKey(x => x.DueId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.CashierShift).WithMany(s => s.Payments)
                .HasForeignKey(x => x.CashierShiftId).OnDelete(DeleteBehavior.ClientSetNull);
        });

        b.Entity<TeacherEarning>(e =>
        {
            e.HasOne(x => x.Teacher).WithMany(t => t.Earnings).HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Payment).WithMany(p => p.TeacherEarnings).HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Enrollment).WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.PrivateLesson).WithMany().HasForeignKey(x => x.PrivateLessonId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.TeacherContract).WithMany().HasForeignKey(x => x.TeacherContractId).OnDelete(DeleteBehavior.ClientSetNull);
            e.HasIndex(x => x.TeacherContractId);
        });

        ConfigurePhase1Finance(b);

        b.Entity<TeacherRating>(e =>
        {
            e.HasOne(x => x.Teacher).WithMany(t => t.Ratings).HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Lesson).WithMany(l => l.Ratings).HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<TeacherRentContract>(e =>
        {
            e.HasOne(x => x.Teacher).WithMany(t => t.RentContracts).HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TeacherRentPayment>(e =>
        {
            e.HasOne(x => x.Contract).WithMany(c => c.Payments).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ClassroomBooking>(e =>
        {
            e.HasOne(x => x.Classroom).WithMany(c => c.Bookings).HasForeignKey(x => x.ClassroomId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CafeteriaContract>(e =>
        {
            e.HasMany(x => x.Payments).WithOne(p => p.Contract).HasForeignKey(p => p.ContractId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<StudentAttendance>(e =>
        {
            e.HasIndex(x => new { x.StudentId, x.SectionId, x.Date }).IsUnique();
            e.HasOne(x => x.Student).WithMany(s => s.Attendances).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Section).WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AttendanceChangeRequest>(e =>
        {
            e.HasOne(x => x.Attendance).WithMany(a => a.ChangeRequests).HasForeignKey(x => x.AttendanceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PrivateLessonRequest>(e =>
        {
            e.HasOne(x => x.Grade).WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PreferredTeacher).WithMany().HasForeignKey(x => x.PreferredTeacherId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.MatchedTeacher).WithMany().HasForeignKey(x => x.MatchedTeacherId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Parent).WithMany(p => p.LessonRequests).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<PrivateLesson>(e =>
        {
            e.HasOne(x => x.Request).WithMany(r => r.Lessons).HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Student).WithMany(s => s.PrivateLessons).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Grade).WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Account>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Parent).WithMany(p => p.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<JournalEntry>(e => e.HasIndex(x => x.EntryNo).IsUnique());
        b.Entity<JournalLine>(e =>
        {
            e.HasOne(x => x.JournalEntry).WithMany(j => j.Lines).HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Account).WithMany(a => a.JournalLines).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Fund>(e =>
        {
            e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Expense>(e =>
        {
            e.HasIndex(x => x.ExpenseNo).IsUnique();
            e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Fund).WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Income>(e =>
        {
            e.HasIndex(x => x.IncomeNo).IsUnique();
            e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Fund).WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Employee>(e => e.HasIndex(x => x.Code).IsUnique());
        b.Entity<EmployeeAttendance>(e =>
        {
            e.HasOne(x => x.Employee).WithMany(a => a.Attendances).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Payroll>(e =>
        {
            e.HasOne(x => x.Employee).WithMany(p => p.Payrolls).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Exam>(e =>
        {
            e.HasOne(x => x.Course).WithMany(c => c.Exams).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ExamResult>(e =>
        {
            e.HasIndex(x => new { x.ExamId, x.StudentId }).IsUnique();
            e.HasOne(x => x.Exam).WithMany(x => x.Results).HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany(s => s.ExamResults).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });

        // SQL Server rejects schemas where one row can be reached through more than one
        // ON DELETE CASCADE / SET NULL path (error 1785). Optional links are therefore nulled by
        // EF for tracked rows (ClientSetNull) and kept as NO ACTION in the database. Records with
        // financial history are never hard-deleted by the application, only deactivated.
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
        {
            if (fk.DeleteBehavior == DeleteBehavior.SetNull)
                fk.DeleteBehavior = DeleteBehavior.ClientSetNull;
        }
    }

    private static void ConfigurePhase1Finance(ModelBuilder b)
    {
        b.Entity<DocumentNumberCounter>(e =>
        {
            e.HasKey(x => new { x.Prefix, x.Year });
            e.Property(x => x.Prefix).HasMaxLength(10).ValueGeneratedNever();
            e.Property(x => x.Year).ValueGeneratedNever();
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        b.Entity<TuitionContract>(e =>
        {
            e.Property(x => x.ContractNo).HasMaxLength(32);
            e.Property(x => x.CreatedByUserId).HasMaxLength(450);
            e.Property(x => x.CreatedByName).HasMaxLength(150);
            e.Property(x => x.Notes).HasMaxLength(600);
            e.HasIndex(x => x.ContractNo).IsUnique();
            e.HasIndex(x => x.EnrollmentId).IsUnique();
            e.HasIndex(x => new { x.StudentId, x.Status });
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Enrollment).WithOne(x => x.TuitionContract)
                .HasForeignKey<TuitionContract>(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CashierShift>(e =>
        {
            e.Property(x => x.ShiftNo).HasMaxLength(32);
            e.Property(x => x.CashierUserId).HasMaxLength(450);
            e.Property(x => x.CashierName).HasMaxLength(150);
            e.Property(x => x.Notes).HasMaxLength(600);
            e.Property(x => x.ApprovedByName).HasMaxLength(150);
            e.HasIndex(x => x.ShiftNo).IsUnique();
            // A cashier can only have one drawer that is either open or awaiting variance approval.
            e.HasIndex(x => x.CashierUserId).IsUnique()
                .HasFilter("[Status] IN (0, 1)");
            e.HasIndex(x => x.OpenedAt);
        });

        b.Entity<CashierDenomination>(e =>
        {
            e.HasOne(x => x.Shift).WithMany(s => s.Denominations)
                .HasForeignKey(x => x.CashierShiftId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.CashierShiftId);
        });

        b.Entity<TeacherContract>(e =>
        {
            e.Property(x => x.ContractNo).HasMaxLength(32);
            e.Property(x => x.Notes).HasMaxLength(600);
            e.Property(x => x.CreatedByUserId).HasMaxLength(450);
            e.Property(x => x.CreatedByName).HasMaxLength(150);
            e.HasIndex(x => x.ContractNo).IsUnique();
            e.HasIndex(x => new { x.TeacherId, x.IsActive });
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TeacherSettlement>(e =>
        {
            e.Property(x => x.SettlementNo).HasMaxLength(32);
            e.Property(x => x.Notes).HasMaxLength(600);
            e.Property(x => x.GeneratedByUserId).HasMaxLength(450);
            e.Property(x => x.GeneratedByName).HasMaxLength(150);
            e.Property(x => x.ApprovedByName).HasMaxLength(150);
            e.Property(x => x.PaidByName).HasMaxLength(150);
            e.HasIndex(x => x.SettlementNo).IsUnique();
            e.HasIndex(x => new { x.TeacherId, x.PeriodStart, x.PeriodEnd }).IsUnique();
            e.HasIndex(x => new { x.TeacherId, x.Status });
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Contract).WithMany(c => c.Settlements)
                .HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.ClientSetNull);
            e.HasOne(x => x.Fund).WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.ClientSetNull);
        });

        b.Entity<TeacherSettlementLine>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(300);
            e.HasIndex(x => x.TeacherEarningId).IsUnique().HasFilter("[TeacherEarningId] IS NOT NULL");
            e.HasIndex(x => x.TeacherSettlementId);
            e.HasOne(x => x.Settlement).WithMany(s => s.Lines)
                .HasForeignKey(x => x.TeacherSettlementId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.TeacherEarning).WithMany()
                .HasForeignKey(x => x.TeacherEarningId).OnDelete(DeleteBehavior.ClientSetNull);
        });
    }
}
