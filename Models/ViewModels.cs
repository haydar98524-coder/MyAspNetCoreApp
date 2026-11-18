using System.ComponentModel.DataAnnotations;

namespace KirkukInstitute.Models;

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";
    public bool RememberMe { get; set; } = true;
    public string? ReturnUrl { get; set; }
}

public class ProfileViewModel
{
    [Required, MaxLength(120)] public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    [Phone, MaxLength(30)] public string? PhoneNumber { get; set; }
    public string PreferredLanguage { get; set; } = "ar";
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password)] public string CurrentPassword { get; set; } = "";
    [Required, DataType(DataType.Password), MinLength(8)] public string NewPassword { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = "";
}

public class PublicRegistrationViewModel
{
    [Required, MaxLength(120)] public string StudentNameAr { get; set; } = "";
    [MaxLength(120)] public string? StudentNameEn { get; set; }
    [Required] public Gender Gender { get; set; } = Gender.Male;
    [Required] public DateTime BirthDate { get; set; } = DateTime.Today.AddYears(-12);
    [Required] public int GradeId { get; set; }
    [Required, MaxLength(150)] public string SchoolName { get; set; } = "";
    [Required, MaxLength(100)] public string Area { get; set; } = "";
    [Required, MaxLength(250)] public string Address { get; set; } = "";
    [MaxLength(30)] public string? StudentPhone { get; set; }
    [Required, MaxLength(120)] public string GuardianName { get; set; } = "";
    [Required, MaxLength(30)] public string GuardianPhone { get; set; } = "";
    [EmailAddress, MaxLength(120)] public string? GuardianEmail { get; set; }
    public int? CourseId { get; set; }
    [MaxLength(600)] public string? Notes { get; set; }
    public bool Consent { get; set; }
}

public class LandingViewModel
{
    public InstituteSettings Settings { get; set; } = new();
    public int StudentCount { get; set; }
    public int TeacherCount { get; set; }
    public int CourseCount { get; set; }
    public int GraduateCount { get; set; }
    public int GradeCount { get; set; }
    public List<Course> FeaturedCourses { get; set; } = new();
    public List<Teacher> FeaturedTeachers { get; set; } = new();
    public List<Stage> Stages { get; set; } = new();
}

public class RegisterEnrollmentViewModel
{
    [Required] public int StudentId { get; set; }
    [Required] public int CourseId { get; set; }
    public int? SectionId { get; set; }
    [Range(0, 100_000_000)] public long Discount { get; set; }
    [Range(0, 100_000_000)] public long InitialPayment { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    [Range(1, 36)] public int InstallmentCount { get; set; } = 1;
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(30);
    public string? Notes { get; set; }
}

// ───────────────────────── Phase 1: cashier drawer ─────────────────────────

public class OpenCashierShiftViewModel
{
    [Range(0, 100_000_000)] public long OpeningFloat { get; set; }
    [MaxLength(600)] public string? Notes { get; set; }
}

public class CloseCashierShiftViewModel
{
    public int ShiftId { get; set; }
    public List<DenominationCountInput> Counts { get; set; } = new();
    [MaxLength(600)] public string? Notes { get; set; }
    public long CountedTotal => Counts.Sum(c => (long)c.Denomination * c.Count);
}

public class DenominationCountInput
{
    public int Denomination { get; set; }
    [Range(0, 100_000)] public int Count { get; set; }
}

// ───────────────────────── Phase 1: teacher settlements ─────────────────────────

public class TeacherContractFormViewModel
{
    [Required] public int TeacherId { get; set; }
    public TeacherCompensationType CompensationType { get; set; } = TeacherCompensationType.PercentageCollected;
    [Range(0, 100)] public decimal SharePercent { get; set; } = 55m;
    [Range(0, 100_000_000)] public long FixedMonthlyAmount { get; set; }
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(600)] public string? Notes { get; set; }
}

public class GenerateSettlementViewModel
{
    [Required] public int TeacherId { get; set; }
    public DateTime PeriodStart { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime PeriodEnd { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
        .AddMonths(1).AddDays(-1);
    [MaxLength(600)] public string? Notes { get; set; }
}

public class UpdateSettlementViewModel
{
    [Required] public int Id { get; set; }
    [Range(0, 100_000_000)] public long Adjustments { get; set; }
    [Range(0, 100_000_000)] public long Deductions { get; set; }
    [MaxLength(600)] public string? Notes { get; set; }
}

public class PaySettlementViewModel
{
    [Required] public int Id { get; set; }
    public int? FundId { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
}

public class CollectPaymentViewModel
{
    [Required] public int StudentId { get; set; }
    [Required] public int DueId { get; set; }
    [Range(1, 100_000_000)] public long Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public string? Notes { get; set; }
}

public class AttendanceMark
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = "";
    public int? AttendanceId { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? ExistingNote { get; set; }
}

public class TakeAttendanceViewModel
{
    public int SectionId { get; set; }
    public string SectionName { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string TeacherName { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Today;
    public bool AlreadyRecorded { get; set; }
    public List<AttendanceMark> Students { get; set; } = new();
}

public class AttendanceChangeRequestViewModel
{
    [Required] public long AttendanceId { get; set; }
    [Required] public AttendanceStatus NewStatus { get; set; }
    [Required, MinLength(5)] public string Reason { get; set; } = "";
}

public class PrivateLessonRequestViewModel
{
    public int? StudentId { get; set; }
    public int? PreferredTeacherId { get; set; }
    [Required] public int GradeId { get; set; }
    [Required] public int SubjectId { get; set; }
    [Required] public TeachingMode Mode { get; set; } = TeachingMode.Institute;
    [Required] public string GuardianName { get; set; } = "";
    [Required] public string GuardianPhone { get; set; } = "";
    [Required] public string Area { get; set; } = "";
    public string? Address { get; set; }
    [Range(1, 7)] public int DaysPerWeek { get; set; } = 2;
    public string PreferredTime { get; set; } = "16:00";
    [Range(30, 240)] public int DurationMinutes { get; set; } = 90;
    [Range(0, 10_000_000)] public long OfferedFee { get; set; } = 25_000;
    public string? Notes { get; set; }
}

public class RateTeacherViewModel
{
    [Required] public int TeacherId { get; set; }
    public int? LessonId { get; set; }
    [Range(1, 5)] public int Explanation { get; set; } = 5;
    [Range(1, 5)] public int Punctuality { get; set; } = 5;
    [Range(1, 5)] public int Interaction { get; set; } = 5;
    [Range(1, 5)] public int FollowUp { get; set; } = 5;
    [Range(1, 5)] public int Overall { get; set; } = 5;
    [MaxLength(600)] public string? Comment { get; set; }
}

public class ScheduleLessonViewModel
{
    [Required] public int RequestId { get; set; }
    [Required] public DateTime Date { get; set; } = DateTime.Today.AddDays(1);
    public string StartTime { get; set; } = "16:00";
    [Range(30, 240)] public int DurationMinutes { get; set; } = 90;
    [Range(0, 10_000_000)] public long Fee { get; set; }
    public string? Notes { get; set; }
}

public class ExamResultsViewModel
{
    public int ExamId { get; set; }
    public string ExamName { get; set; } = "";
    public string CourseName { get; set; } = "";
    public int MaxMark { get; set; }
    public int PassMark { get; set; }
    public List<ExamResultRow> Rows { get; set; } = new();
}

public class ExamResultRow
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = "";
    public string StudentNo { get; set; } = "";
    public decimal? Mark { get; set; }
}

public class DashboardViewModel
{
    public string PeriodLabel { get; set; } = "";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int TotalStudents { get; set; }
    public int NewStudentsThisMonth { get; set; }
    public int PresentToday { get; set; }
    public int AbsentToday { get; set; }
    public int TeacherCount { get; set; }
    public int EmployeeCount { get; set; }
    public int CourseCount { get; set; }
    public int SectionCount { get; set; }
    public int ClassroomCount { get; set; }
    public int OccupiedClassroomsToday { get; set; }
    public long RevenueToday { get; set; }
    public long RevenueMonth { get; set; }
    public long RevenueYear { get; set; }
    public long PeriodRevenue { get; set; }
    public long PeriodExpenses { get; set; }
    public long TeacherCostsPeriod { get; set; }
    public long PayrollPeriod { get; set; }
    public long TotalDues { get; set; }
    public long NetProfitPeriod { get; set; }
    public long HallRentRevenue { get; set; }
    public long CafeteriaRevenue { get; set; }
    public long TeacherRentRevenue { get; set; }
    public long PrivateLessonRevenue { get; set; }
    public int PendingPrivateRequests { get; set; }
    public int HomeLessonsCount { get; set; }
    public double AverageRating { get; set; }
    public int PendingChangeRequests { get; set; }
    public int UnreadNotifications { get; set; }
    public List<ChartPoint> Trend { get; set; } = new();
    public List<NamedAmount> RevenueSources { get; set; } = new();
    public List<AttendancePoint> AttendanceWeek { get; set; } = new();
    public List<CourseProfitRow> TopCourses { get; set; } = new();
    public List<TodaySession> TodaySessions { get; set; } = new();
    public List<RecentPayment> RecentPayments { get; set; } = new();
    public List<DashboardAlert> Alerts { get; set; } = new();
}

public record ChartPoint(string Label, long Revenue, long Expense);
public record NamedAmount(string Label, long Amount, string Color);
public record AttendancePoint(string Label, int Present, int Absent);
public record CourseProfitRow(string Name, long Revenue, long Cost, long Net);
public record TodaySession(string Time, string Course, string Teacher, string Room, string Section);
public record RecentPayment(string ReceiptNo, string Student, long Amount, DateTime Date);
public record DashboardAlert(string Title, string Detail, string Tone, string Url);

public class StudentFormViewModel
{
    public int Id { get; set; }

    /// <summary>
    /// Assigned once at registration. Displayed read-only on the edit form and never written
    /// back from the form; the controller and the database trigger keep it immutable.
    /// </summary>
    public string? StudentNo { get; set; }

    [Required, MaxLength(120)] public string FullNameAr { get; set; } = "";
    [Required, MaxLength(120)] public string FullNameEn { get; set; } = "";
    [Required, MaxLength(30)] public string NationalId { get; set; } = "";
    public Gender Gender { get; set; } = Gender.Male;
    [Required] public DateTime BirthDate { get; set; } = DateTime.Today.AddYears(-15);
    [Required] public int GradeId { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(150)] public string? SchoolName { get; set; }
    public int? HallId { get; set; }
    public List<int> SubjectIds { get; set; } = new();
    public List<int> TeacherIds { get; set; } = new();
    public int? ParentId { get; set; }
    [MaxLength(120)] public string? GuardianName { get; set; }
    [MaxLength(30)] public string? GuardianPhone { get; set; }
    [EmailAddress, MaxLength(120)] public string? GuardianEmail { get; set; }
    [MaxLength(250)] public string Address { get; set; } = "";
    [MaxLength(100)] public string Area { get; set; } = "";
    public StudentStatus Status { get; set; } = StudentStatus.Active;
    [MaxLength(600)] public string? Notes { get; set; }
    public int? BranchId { get; set; }
    public IFormFile? Photo { get; set; }
    public string? PhotoPath { get; set; }
}

public class ParentFormViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string FullNameAr { get; set; } = "";
    [MaxLength(120)] public string FullNameEn { get; set; } = "";
    [Required, MaxLength(30)] public string Phone { get; set; } = "";
    [EmailAddress, MaxLength(120)] public string? Email { get; set; }
    [MaxLength(250)] public string Address { get; set; } = "";
    [MaxLength(100)] public string Area { get; set; } = "";
    [MaxLength(100)] public string? Occupation { get; set; }
    [MaxLength(600)] public string? Notes { get; set; }
}

public class TeacherFormViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string FullNameAr { get; set; } = "";
    [Required, MaxLength(120)] public string FullNameEn { get; set; } = "";
    public Gender Gender { get; set; } = Gender.Male;
    [Required, MaxLength(30)] public string Phone { get; set; } = "";
    [EmailAddress, MaxLength(120)] public string? Email { get; set; }
    [Required, MaxLength(100)] public string Specialization { get; set; } = "";
    [MaxLength(120)] public string Certificate { get; set; } = "";
    [MaxLength(120)] public string University { get; set; } = "";
    [Range(1970, 2035)] public int GraduationYear { get; set; } = 2015;
    [Range(0, 60)] public int ExperienceYears { get; set; }
    public TeachingMode TeachingMode { get; set; } = TeachingMode.Institute;
    [MaxLength(120)] public string Areas { get; set; } = "";
    [MaxLength(1000)] public string? BioAr { get; set; }
    [MaxLength(1000)] public string? BioEn { get; set; }
    public bool IsPublic { get; set; } = true;
    public bool IsActive { get; set; } = true;
    [Range(0, 100_000_000)] public long? FixedMonthlySalary { get; set; }
    public DateTime HireDate { get; set; } = DateTime.Today;
    public List<int> SubjectIds { get; set; } = new();
    public List<int> GradeIds { get; set; } = new();
    public IFormFile? Photo { get; set; }
    public string? PhotoPath { get; set; }
}

public class CourseFormViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string NameAr { get; set; } = "";
    [Required, MaxLength(120)] public string NameEn { get; set; } = "";
    [Required] public int StageId { get; set; }
    [Required] public int GradeId { get; set; }
    [Required] public int SubjectId { get; set; }
    [Required] public int TeacherId { get; set; }
    public CourseType Type { get; set; } = CourseType.Group;
    [Range(0, 100_000_000)] public long Fee { get; set; }
    [Range(0, 100)] public decimal TeacherSharePercent { get; set; } = 55m;
    [Required] public DateTime StartDate { get; set; } = DateTime.Today;
    [Required] public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(3);
    [Range(1, 500)] public int Capacity { get; set; } = 30;
    public CourseStatus Status { get; set; } = CourseStatus.Active;
    [MaxLength(800)] public string DescriptionAr { get; set; } = "";
    [MaxLength(800)] public string DescriptionEn { get; set; } = "";
}

public class ClassroomFormViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(30)] public string Number { get; set; } = "";
    [Required, MaxLength(80)] public string NameAr { get; set; } = "";
    [Required, MaxLength(80)] public string NameEn { get; set; } = "";
    [MaxLength(30)] public string Floor { get; set; } = "";
    [Range(1, 500)] public int Capacity { get; set; } = 30;
    [MaxLength(60)] public string RoomType { get; set; } = "قاعة دراسية";
    [MaxLength(250)] public string? Equipment { get; set; }
    [Range(0, 10_000_000)] public long RentPerHour { get; set; } = 15_000;
    public ClassroomStatus Status { get; set; } = ClassroomStatus.Available;
}

public class ExamFormViewModel
{
    public int Id { get; set; }
    [Required] public int CourseId { get; set; }
    [Required, MaxLength(120)] public string NameAr { get; set; } = "";
    [Required, MaxLength(120)] public string NameEn { get; set; } = "";
    public ExamType Type { get; set; } = ExamType.Monthly;
    public DateTime Date { get; set; } = DateTime.Today.AddDays(7);
    [Range(1, 1000)] public int MaxMark { get; set; } = 100;
    [Range(0, 1000)] public int PassMark { get; set; } = 50;
}

public class ProfitReportViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string GroupBy { get; set; } = "course";
    public long TotalRevenue { get; set; }
    public long TotalTeacherCost { get; set; }
    public long TotalExpenses { get; set; }
    public decimal TotalProfit => TotalRevenue - TotalTeacherCost - TotalExpenses;
    public List<ProfitLine> Lines { get; set; } = new();
}

public class ProfitLine
{
    public string Name { get; set; } = "";
    public string Meta { get; set; } = "";
    public long Revenue { get; set; }
    public long TeacherCost { get; set; }
    public long Expenses { get; set; }
    public decimal Profit => Revenue - TeacherCost - Expenses;
    public decimal Margin => Revenue == 0 ? 0 : Math.Round((decimal)Profit / Revenue * 100, 1);
}

public class AttendanceIndexViewModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public int Present { get; set; }
    public int Absent { get; set; }
    public int Late { get; set; }
    public int Excused { get; set; }
    public int PendingRequests { get; set; }
    public List<AttendanceSectionRow> Sections { get; set; } = new();
}

public class AttendanceSectionRow
{
    public CourseSection Section { get; set; } = null!;
    public string CourseName { get; set; } = "";
    public string TeacherName { get; set; } = "";
    public string Room { get; set; } = "";
    public string Time { get; set; } = "";
    public int Enrolled { get; set; }
    public int Recorded { get; set; }
    public int Present { get; set; }
    public int Absent { get; set; }
    public bool IsScheduled { get; set; }
}

public class JournalLineInput
{
    public int AccountId { get; set; }
    public long Debit { get; set; }
    public long Credit { get; set; }
    public string? Description { get; set; }
}

public class ManualJournalViewModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    [Required, MaxLength(300)] public string Description { get; set; } = "";
    [MaxLength(80)] public string? Reference { get; set; }
    public List<JournalLineInput> Lines { get; set; } = new();
}

public class ExpenseFormViewModel
{
    public int? Id { get; set; }
    [Required] public int AccountId { get; set; }
    public int? FundId { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    [Range(1, 100_000_000)] public long Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    [Required, MaxLength(150)] public string Vendor { get; set; } = "";
    [Required, MaxLength(300)] public string Description { get; set; } = "";
    public int? CourseId { get; set; }
    public bool PostNow { get; set; } = true;
    public IFormFile? Attachment { get; set; }
}

public class IncomeFormViewModel
{
    public int? AccountId { get; set; }
    public int? FundId { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    [Range(1, 100_000_000)] public long Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    [Required, MaxLength(150)] public string Source { get; set; } = "";
    [Required, MaxLength(300)] public string Description { get; set; } = "";
}

public class BookingFormViewModel
{
    public int? Id { get; set; }
    [Required] public int ClassroomId { get; set; }
    [Required, MaxLength(150)] public string TenantName { get; set; } = "";
    [Required, MaxLength(200)] public string Purpose { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Today;
    public string StartTime { get; set; } = "16:00";
    public string EndTime { get; set; } = "18:00";
    [Range(0, 10_000_000)] public long HourlyRate { get; set; }
    public string? Notes { get; set; }
    public bool MarkPaid { get; set; }
}

public class RentContractFormViewModel
{
    [Required] public int TeacherId { get; set; }
    [Range(1, 100_000_000)] public long MonthlyAmount { get; set; } = 250_000;
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime? EndDate { get; set; } = new DateTime(DateTime.Today.Year + 1, 6, 30);
    public string? Notes { get; set; }
}

public class RentalPaymentFormViewModel
{
    [Required] public int ContractId { get; set; }
    public DateTime Month { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [Range(1, 100_000_000)] public long Amount { get; set; }
    public string? Reference { get; set; }
}

public class PayableSummary
{
    public Teacher Teacher { get; set; } = null!;
    public long Unpaid { get; set; }
    public long Paid { get; set; }
    public int Count { get; set; }
    public long CourseShare { get; set; }
    public long PrivateShare { get; set; }
}

public class FinanceOverviewViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public long TuitionRevenue { get; set; }
    public long OtherRevenue { get; set; }
    public long HallRevenue { get; set; }
    public long CafeteriaRevenue { get; set; }
    public long TeacherRentRevenue { get; set; }
    public long PrivateRevenue { get; set; }
    public long TotalRevenue => TuitionRevenue + OtherRevenue + HallRevenue + CafeteriaRevenue + TeacherRentRevenue + PrivateRevenue;
    public long TeacherCost { get; set; }
    public long PayrollCost { get; set; }
    public long DirectExpenses { get; set; }
    public long NetProfit => TotalRevenue - TeacherCost - PayrollCost - DirectExpenses;
    public long DuesOutstanding { get; set; }
    public long CashBalance { get; set; }
    public long BankBalance { get; set; }
    public List<Fund> Funds { get; set; } = new();
    public List<ChartPoint> Trend { get; set; } = new();
    public List<NamedAmount> RevenueSources { get; set; } = new();
    public List<NamedAmount> ExpenseBreakdown { get; set; } = new();
    public List<JournalEntry> RecentEntries { get; set; } = new();
    public List<AccountBalanceRow> Accounts { get; set; } = new();
}

public record AccountBalanceRow(string Code, string NameAr, string NameEn, AccountType Type, long Debit, long Credit)
{
    public long Net => Debit - Credit;
}

public class TeacherPayablesViewModel
{
    public List<PayableSummary> Rows { get; set; } = new();
    public List<TeacherEarning> RecentPaid { get; set; } = new();
    public long TotalUnpaid => Rows.Sum(r => r.Unpaid);
    public long TotalPaid => Rows.Sum(r => r.Paid);
    public decimal? Method { get; set; }
}

// ───────────────────────── Exams ─────────────────────────

public class ExamsIndexViewModel
{
    public int Total { get; set; }
    public int Upcoming { get; set; }
    public int Recorded { get; set; }
    public decimal AveragePassRate { get; set; }
    public List<ExamIndexRow> Rows { get; set; } = new();
}

public class ExamIndexRow
{
    public Exam Exam { get; set; } = null!;
    public string CourseName { get; set; } = "";
    public string TeacherName { get; set; } = "";
    public string GradeName { get; set; } = "";
    public int Enrolled { get; set; }
    public int Recorded { get; set; }
    public int Passed { get; set; }
    public decimal Average { get; set; }
    public decimal PassRate => Recorded == 0 ? 0 : Math.Round((decimal)Passed / Recorded * 100, 1);
}

// ───────────────────────── Human resources ─────────────────────────

public class EmployeesIndexViewModel
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int OnLeave { get; set; }
    public long MonthlyPayroll { get; set; }
    public List<Employee> Employees { get; set; } = new();
}

public class EmployeeFormViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string FullNameAr { get; set; } = "";
    [Required, MaxLength(120)] public string FullNameEn { get; set; } = "";
    [Required, MaxLength(80)] public string JobTitleAr { get; set; } = "";
    [Required, MaxLength(80)] public string JobTitleEn { get; set; } = "";
    [Required, MaxLength(80)] public string Department { get; set; } = "";
    [Required, MaxLength(30)] public string Phone { get; set; } = "";
    [EmailAddress, MaxLength(120)] public string? Email { get; set; }
    [Range(0, 100_000_000)] public long BasicSalary { get; set; }
    [Range(0, 100_000_000)] public long Allowances { get; set; }
    public DateTime HireDate { get; set; } = DateTime.Today;
    public StaffStatus Status { get; set; } = StaffStatus.Active;
    [MaxLength(600)] public string? Notes { get; set; }
}

public class StaffAttendanceViewModel
{
    public DateTime Date { get; set; } = DateTime.Today;
    public bool AlreadyRecorded { get; set; }
    public List<StaffAttendanceRow> Rows { get; set; } = new();
    public int Present => Rows.Count(r => r.Status == AttendanceStatus.Present);
    public int Absent => Rows.Count(r => r.Status == AttendanceStatus.Absent);
    public int Late => Rows.Count(r => r.Status == AttendanceStatus.Late);
    public int Excused => Rows.Count(r => r.Status == AttendanceStatus.Excused);
}

public class StaffAttendanceRow
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public EmployeeAttendance? Record { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? CheckIn { get; set; }
    public string? CheckOut { get; set; }
    public string? Notes { get; set; }
}

public class PayrollViewModel
{
    public DateTime Month { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public List<PayrollRow> Rows { get; set; } = new();
    public int Draft => Rows.Count(r => r.Payroll is null || r.Payroll.Status == PayrollStatus.Draft);
    public int Approved => Rows.Count(r => r.Payroll?.Status == PayrollStatus.Approved);
    public int Paid => Rows.Count(r => r.Payroll?.Status == PayrollStatus.Paid);
    public long TotalBasic => Rows.Sum(r => r.Payroll?.Basic ?? r.Employee.BasicSalary);
    public long TotalAllowances => Rows.Sum(r => r.Payroll?.Allowances ?? r.Employee.Allowances);
    public long TotalDeductions => Rows.Sum(r => (r.Payroll?.AbsenceDeduction ?? 0) + (r.Payroll?.OtherDeductions ?? 0) + (r.Payroll?.Advances ?? 0));
    public long TotalNet => Rows.Sum(r => r.Payroll?.Net ?? (r.Employee.BasicSalary + r.Employee.Allowances));
}

public class PayrollRow
{
    public Employee Employee { get; set; } = null!;
    public Payroll? Payroll { get; set; }
}

// ───────────────────────── Reports ─────────────────────────

public class AttendanceReportViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int Sessions { get; set; }
    public int Present { get; set; }
    public int Absent { get; set; }
    public int Late { get; set; }
    public int Excused { get; set; }
    public decimal Rate => Sessions == 0 ? 0 : Math.Round((decimal)(Present + Late) / Sessions * 100, 1);
    public List<AttendanceReportRow> Rows { get; set; } = new();
}

public class AttendanceReportRow
{
    public string Name { get; set; } = "";
    public string Meta { get; set; } = "";
    public int Enrolled { get; set; }
    public int Sessions { get; set; }
    public int Present { get; set; }
    public int Absent { get; set; }
    public int Late { get; set; }
    public int Excused { get; set; }
    public decimal Rate => Sessions == 0 ? 0 : Math.Round((decimal)(Present + Late) / Sessions * 100, 1);
}

public class DuesReportViewModel
{
    public DateTime AsOf { get; set; } = DateTime.Today;
    public long Total { get; set; }
    public long Overdue { get; set; }
    public int Students { get; set; }
    public List<DuesReportRow> Rows { get; set; } = new();
}

public class DuesReportRow
{
    public Student Student { get; set; } = null!;
    public string ParentName { get; set; } = "";
    public string GradeName { get; set; } = "";
    public long Amount { get; set; }
    public long Paid { get; set; }
    public long Remaining { get; set; }
    public DateTime DueDate { get; set; }
    public int DaysOverdue { get; set; }
    public int Bucket => DaysOverdue <= 0 ? 0 : DaysOverdue <= 30 ? 1 : DaysOverdue <= 60 ? 2 : 3;
}

public class PayrollReportViewModel
{
    public DateTime Month { get; set; }
    public List<PayrollReportRow> Rows { get; set; } = new();
    public long TotalBasic => Rows.Sum(r => r.Payroll?.Basic ?? 0);
    public long TotalAllowances => Rows.Sum(r => r.Payroll?.Allowances ?? 0);
    public long TotalDeductions => Rows.Sum(r => (r.Payroll?.AbsenceDeduction ?? 0) + (r.Payroll?.OtherDeductions ?? 0) + (r.Payroll?.Advances ?? 0));
    public long TotalNet => Rows.Sum(r => r.Payroll?.Net ?? 0);
    public int Paid => Rows.Count(r => r.Payroll?.Status == PayrollStatus.Paid);
    public int Approved => Rows.Count(r => r.Payroll?.Status == PayrollStatus.Approved);
    public int Draft => Rows.Count(r => r.Payroll is null || r.Payroll.Status == PayrollStatus.Draft);
}

public class PayrollReportRow
{
    public Employee Employee { get; set; } = null!;
    public Payroll? Payroll { get; set; }
}

public class EnrollmentReportViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public long Fees { get; set; }
    public long Collected { get; set; }
    public long Outstanding { get; set; }
    public int Enrollments { get; set; }
    public List<EnrollmentReportRow> Rows { get; set; } = new();
}

public class EnrollmentReportRow
{
    public Course Course { get; set; } = null!;
    public string TeacherName { get; set; } = "";
    public int NewEnrollments { get; set; }
    public int ActiveEnrollments { get; set; }
    public long Fees { get; set; }
    public long Collected { get; set; }
    public long Outstanding { get; set; }
}

// ───────────────────────── Admin ─────────────────────────

public class AdminSettingsViewModel
{
    [Required, MaxLength(160)] public string NameAr { get; set; } = "";
    [Required, MaxLength(160)] public string NameEn { get; set; } = "";
    [MaxLength(60)] public string LicenseNo { get; set; } = "";
    [MaxLength(60)] public string MinistryCode { get; set; } = "";
    [MaxLength(220)] public string AddressAr { get; set; } = "";
    [MaxLength(220)] public string AddressEn { get; set; } = "";
    [Phone, MaxLength(30)] public string Phone { get; set; } = "";
    [EmailAddress, MaxLength(120)] public string Email { get; set; } = "";
    [MaxLength(120)] public string PrincipalName { get; set; } = "";
    [Required, MaxLength(40)] public string AcademicYear { get; set; } = "";
    [MaxLength(160)] public string MottoAr { get; set; } = "";
    [MaxLength(160)] public string MottoEn { get; set; } = "";
    [Required, MaxLength(10)] public string CurrencyAr { get; set; } = "د.ع";
    [Required, MaxLength(10)] public string CurrencyEn { get; set; } = "IQD";
    public bool TuitionEnabled { get; set; } = true;
    [Range(0, 100)] public decimal DefaultTeacherShare { get; set; } = 55;
    [Range(0, 100)] public decimal PrivateLessonTeacherShare { get; set; } = 60;
    [Range(0, 100_000_000)] public long PrepTeacherMonthlyRent { get; set; }
    [Range(0, 1_000_000_000)] public long CafeteriaAnnualRent { get; set; }
    [Range(0, 100)] public int PassMark { get; set; } = 50;
    [Range(0, 100)] public int AttendanceWarningPercent { get; set; } = 20;
    public List<Branch> Branches { get; set; } = new();
    public DateTime? UpdatedAt { get; set; }
}

public class UserListRow
{
    public ApplicationUser User { get; set; } = null!;
    public IList<string> Roles { get; set; } = new List<string>();
    public string LinkedTo { get; set; } = "";
    public DateTime? LastLogin { get; set; }
}

public record LinkOption(int Id, string Name);

public class UsersIndexViewModel
{
    public List<UserListRow> Users { get; set; } = new();
    public int Total { get; set; }
    public int Active { get; set; }
    public int Staff { get; set; }
    public int Portal { get; set; }
    public string? Q { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }
    public List<string> RoleOptions { get; set; } = new();
}

public class AdminUserFormViewModel
{
    public string? Id { get; set; }
    [Required, EmailAddress, MaxLength(120)] public string Email { get; set; } = "";
    [Required, MaxLength(120)] public string FullName { get; set; } = "";
    [Required] public string Role { get; set; } = Roles.Registrar;
    [MinLength(8), MaxLength(80)] public string? Password { get; set; }
    public bool IsActive { get; set; } = true;
    public string PreferredLanguage { get; set; } = "ar";
    public int? TeacherId { get; set; }
    public int? ParentId { get; set; }
    public int? StudentId { get; set; }
    public bool IsEdit => !string.IsNullOrEmpty(Id);
}

public class AdminResetPasswordViewModel
{
    [Required] public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    [Required, MinLength(8), MaxLength(80), DataType(DataType.Password)] public string NewPassword { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = "";
}

public class AuditIndexViewModel
{
    public List<AuditLog> Entries { get; set; } = new();
    public int Total { get; set; }
    public int Today { get; set; }
    public int Users { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 30;
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(1, PageSize)));
    public string? Q { get; set; }
    public string? Action { get; set; }
    public string? Entity { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<string> Actions { get; set; } = new();
    public List<string> Entities { get; set; } = new();
}

public class AdminBackupViewModel
{
    public long DatabaseBytes { get; set; }
    public string DatabasePath { get; set; } = "";
    public DateTime? LastBackupAt { get; set; }
    public List<BackupFileInfo> Backups { get; set; } = new();
    public int Students { get; set; }
    public int Payments { get; set; }
    public int JournalEntries { get; set; }
    public int Users { get; set; }
    public int Tables { get; set; }
    public string ServerName { get; set; } = "";
    public string DatabaseName { get; set; } = "";
    public string ServerVersion { get; set; } = "";
    public string Integrity { get; set; } = "";
}

public record BackupFileInfo(string Name, DateTime CreatedAt, long Bytes);

public class ComplianceViewModel
{
    public InstituteSettings Settings { get; set; } = null!;
    public List<ComplianceCheck> Checks { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int Passed => Checks.Count(c => c.Status == "ok");
    public int Warnings => Checks.Count(c => c.Status == "warn");
    public int Failures => Checks.Count(c => c.Status == "fail");
}

public record ComplianceCheck(string TitleAr, string TitleEn, string DetailAr, string DetailEn,
    string Status, string? Url = null);

// ───────────────────────── Notifications ─────────────────────────

public class NotificationsViewModel
{
    public List<Notification> Items { get; set; } = new();
    public int Unread { get; set; }
    public int Total { get; set; }
    public string Filter { get; set; } = "all";
    public string? Type { get; set; }
}

public class AnnouncementFormViewModel
{
    [Required, MaxLength(120)] public string TitleAr { get; set; } = "";
    [Required, MaxLength(120)] public string TitleEn { get; set; } = "";
    [Required, MaxLength(600)] public string BodyAr { get; set; } = "";
    [Required, MaxLength(600)] public string BodyEn { get; set; } = "";
    public string Audience { get; set; } = "all";
    public NotificationType Type { get; set; } = NotificationType.Info;
    [MaxLength(200)] public string? Url { get; set; }
}

// ───────────────────────── Role portals ─────────────────────────

public class TeacherPortalViewModel
{
    public Teacher Teacher { get; set; } = null!;
    public List<Course> Courses { get; set; } = new();
    public List<ClassSchedule> Today { get; set; } = new();
    public List<ClassSchedule> Week { get; set; } = new();
    public List<TeacherEarning> RecentEarnings { get; set; } = new();
    public List<PrivateLesson> UpcomingLessons { get; set; } = new();
    public List<PrivateLessonRequest> PendingRequests { get; set; } = new();
    public long Unpaid { get; set; }
    public long Paid { get; set; }
    public int Students { get; set; }
    public decimal AttendanceRate { get; set; }
    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
}

public class ChildSummary
{
    public Student Student { get; set; } = null!;
    public int Enrollments { get; set; }
    public int Sessions { get; set; }
    public int Present { get; set; }
    public decimal AttendanceRate => Sessions == 0 ? 0 : Math.Round((decimal)Present / Sessions * 100, 1);
    public decimal AverageMark { get; set; }
    public long Outstanding { get; set; }
    public List<Enrollment> Courses { get; set; } = new();
}

public class ParentPortalViewModel
{
    public Parent Parent { get; set; } = null!;
    public List<ChildSummary> Children { get; set; } = new();
    public long Outstanding { get; set; }
    public long PaidYear { get; set; }
    public List<Due> Dues { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<PrivateLesson> UpcomingLessons { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
}

public class StudentPortalViewModel
{
    public Student Student { get; set; } = null!;
    public List<Enrollment> Enrollments { get; set; } = new();
    public List<ClassSchedule> Schedule { get; set; } = new();
    public List<StudentAttendance> Attendance { get; set; } = new();
    public List<ExamResult> Results { get; set; } = new();
    public List<Due> Dues { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<PrivateLesson> PrivateLessons { get; set; } = new();
    public int Sessions { get; set; }
    public int Present { get; set; }
    public int Absent { get; set; }
    public int Late { get; set; }
    public int Excused { get; set; }
    public decimal AttendanceRate => Sessions == 0 ? 0 : Math.Round((decimal)Present / Sessions * 100, 1);
    public decimal AverageMark { get; set; }
    public long Outstanding { get; set; }
}

public class TeacherEarningsViewModel
{
    public List<TeacherEarning> Earnings { get; set; } = new();
    public long Total { get; set; }
    public long Paid { get; set; }
    public long Unpaid { get; set; }
    public List<MonthlyEarningRow> Months { get; set; } = new();
}

public record MonthlyEarningRow(string Label, long Amount, int Count);

public class TeacherRequestsViewModel
{
    public List<PrivateLessonRequest> Requests { get; set; } = new();
    public List<PrivateLesson> Lessons { get; set; } = new();
    public int OpenRequests { get; set; }
    public int UpcomingLessons { get; set; }
    public int CompletedLessons { get; set; }
}

public class ParentAttendanceViewModel
{
    public List<StudentAttendance> Records { get; set; } = new();
    public List<Student> Children { get; set; } = new();
    public int? StudentId { get; set; }
    public string? Status { get; set; }
    public int Sessions { get; set; }
    public int Present { get; set; }
    public int Absent { get; set; }
    public int Late { get; set; }
    public int Excused { get; set; }
    public decimal Rate => Sessions == 0 ? 0 : Math.Round((decimal)Present / Sessions * 100, 1);
}

public class ParentResultsViewModel
{
    public List<ExamResult> Results { get; set; } = new();
    public List<Student> Children { get; set; } = new();
    public int? StudentId { get; set; }
    public ExamType? Type { get; set; }
    public decimal Average { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Total => Results.Count;
}
