namespace KirkukInstitute.Models;

public enum Gender { Male, Female }

public enum StageType { Primary, Intermediate, Preparatory }

public enum GradeBranch { General, Scientific, Literary }

public enum CourseType { Group, Individual, SingleLesson, Private, Home }

public enum CourseStatus { Draft, Active, Completed, Cancelled }

public enum EnrollmentStatus { Active, Completed, Cancelled }

public enum StudentStatus { Active, Suspended, Graduated, Withdrawn }

public enum StaffStatus { Active, OnLeave, Inactive }

public enum TeachingMode { Institute, Home, Both }

public enum AttendanceStatus { Present, Absent, Late, Excused }

public enum ChangeRequestStatus { Pending, Approved, Rejected }

public enum DueStatus { Unpaid, Partial, Paid, Waived }

public enum PaymentMethod { Cash, BankTransfer, Card, Wallet }

public enum AccountType { Asset, Liability, Equity, Revenue, Expense }

public enum FundType { Cash, Bank }

public enum ExpenseStatus { Draft, Approved }

public enum PayrollStatus { Draft, Approved, Paid }

public enum BookingPaymentStatus { Unpaid, Paid }

public enum RequestStatus { Pending, Matched, Accepted, Rejected, Completed, Cancelled }

public enum PrivateLessonStatus { Scheduled, Completed, Cancelled, NoShow }

public enum TeacherEarningKind { CourseShare, PrivateLesson }

public enum ClassroomStatus { Available, Maintenance, Retired }

public enum AssetStatus { InUse, UnderMaintenance, Disposed }

public enum NotificationType { Info, Success, Warning, Danger }

public enum ExamType { Monthly, Midterm, Final, Quiz }

// ── Phase 1: financial governance ────────────────────────────────────────────
public enum TuitionContractStatus { Draft, Active, Completed, Cancelled }

public enum CashierShiftStatus { Open, PendingApproval, Closed }

public enum TeacherCompensationType { PercentageCollected, FixedMonthly }

public enum TeacherSettlementStatus { Draft, Approved, Paid }

public enum TeacherSettlementLineKind { Earning, FixedRetainer }

public static class Roles
{
    public const string Administrator = "Administrator";
    public const string Manager = "Manager";
    public const string Accountant = "Accountant";
    public const string Hr = "Hr";
    public const string Registrar = "Registrar";
    public const string Teacher = "Teacher";
    public const string Parent = "Parent";
    public const string Student = "Student";

    public const string Staff = Administrator + "," + Manager + "," + Accountant + "," + Hr + "," + Registrar;
    public const string Leadership = Administrator + "," + Manager;
    public const string Finance = Administrator + "," + Manager + "," + Accountant;
    public const string People = Administrator + "," + Manager + "," + Hr;
    public const string Admissions = Administrator + "," + Manager + "," + Registrar + "," + Accountant;
    public const string TeachingStaff = Administrator + "," + Manager + "," + Registrar;
    public const string Cashiers = Administrator + "," + Manager + "," + Accountant + "," + Registrar;
    public const string All = Administrator + "," + Manager + "," + Accountant + "," + Hr + "," + Registrar + "," + Teacher + "," + Parent + "," + Student;
}
