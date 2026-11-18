namespace KirkukInstitute.Models;

public class Classroom
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Floor { get; set; } = "";
    public int Capacity { get; set; }
    public string RoomType { get; set; } = "قاعة دراسية";
    public string? Equipment { get; set; }
    public long RentPerHour { get; set; }
    public ClassroomStatus Status { get; set; } = ClassroomStatus.Available;
    public int? BranchId { get; set; }
    public ICollection<ClassSchedule> Schedules { get; set; } = new List<ClassSchedule>();
    public ICollection<ClassroomBooking> Bookings { get; set; } = new List<ClassroomBooking>();
}

public class ClassroomBooking
{
    public int Id { get; set; }
    public int ClassroomId { get; set; }
    public Classroom Classroom { get; set; } = null!;
    public string TenantName { get; set; } = "";
    public string Purpose { get; set; } = "";
    public DateTime Date { get; set; }
    public string StartTime { get; set; } = "16:00";
    public string EndTime { get; set; } = "18:00";
    public long HourlyRate { get; set; }
    public long TotalAmount { get; set; }
    public BookingPaymentStatus PaymentStatus { get; set; } = BookingPaymentStatus.Unpaid;
    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CafeteriaContract
{
    public int Id { get; set; }
    public string TenantName { get; set; } = "";
    public string TenantPhone { get; set; } = "";
    public long AnnualAmount { get; set; } = 8_400_000;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public ICollection<CafeteriaPayment> Payments { get; set; } = new List<CafeteriaPayment>();
}

public class CafeteriaPayment
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public CafeteriaContract Contract { get; set; } = null!;
    public DateTime Month { get; set; }
    public long Amount { get; set; }
    public DateTime? PaidDate { get; set; }
    public bool IsPaid { get; set; }
    public string? Reference { get; set; }
}

public class Enrollment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int? SectionId { get; set; }
    public CourseSection? Section { get; set; }
    public DateTime EnrollmentDate { get; set; } = DateTime.Today;
    public long Fee { get; set; }
    public long Discount { get; set; }
    public long NetFee { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public string? Notes { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Due> Dues { get; set; } = new List<Due>();
    public ICollection<StudentAttendance> Attendances { get; set; } = new List<StudentAttendance>();
    public TuitionContract? TuitionContract { get; set; }
}

public class StudentAttendance
{
    public long Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int SectionId { get; set; }
    public CourseSection Section { get; set; } = null!;
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public string RecordedByUserId { get; set; } = "";
    public string RecordedByName { get; set; } = "";
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public bool IsEdited { get; set; }
    public string? LastEditedByName { get; set; }
    public DateTime? LastEditedAt { get; set; }
    public ICollection<AttendanceChangeRequest> ChangeRequests { get; set; } = new List<AttendanceChangeRequest>();
}

public class AttendanceChangeRequest
{
    public int Id { get; set; }
    public long AttendanceId { get; set; }
    public StudentAttendance Attendance { get; set; } = null!;
    public string RequestedByUserId { get; set; } = "";
    public string RequestedByName { get; set; } = "";
    public AttendanceStatus NewStatus { get; set; }
    public string Reason { get; set; } = "";
    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Pending;
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PrivateLessonRequest
{
    public int Id { get; set; }
    public int? StudentId { get; set; }
    public Student? Student { get; set; }
    public int? ParentId { get; set; }
    public Parent? Parent { get; set; }
    public string GuardianName { get; set; } = "";
    public string GuardianPhone { get; set; } = "";
    public int GradeId { get; set; }
    public Grade Grade { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public int? PreferredTeacherId { get; set; }
    public Teacher? PreferredTeacher { get; set; }
    public TeachingMode Mode { get; set; } = TeachingMode.Institute;
    public string Area { get; set; } = "";
    public string? Address { get; set; }
    public int DaysPerWeek { get; set; } = 1;
    public string PreferredTime { get; set; } = "16:00";
    public int DurationMinutes { get; set; } = 90;
    public long OfferedFee { get; set; }
    public string? Notes { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public int? MatchedTeacherId { get; set; }
    public Teacher? MatchedTeacher { get; set; }
    public string? AdminNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<PrivateLesson> Lessons { get; set; } = new List<PrivateLesson>();
}

public class PrivateLesson
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public PrivateLessonRequest Request { get; set; } = null!;
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public int? StudentId { get; set; }
    public Student? Student { get; set; }
    public int? ParentId { get; set; }
    public Parent? Parent { get; set; }
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public int GradeId { get; set; }
    public Grade Grade { get; set; } = null!;
    public TeachingMode Mode { get; set; }
    public string Area { get; set; } = "";
    public string? Address { get; set; }
    public DateTime Date { get; set; }
    public string StartTime { get; set; } = "16:00";
    public int DurationMinutes { get; set; } = 90;
    public long Fee { get; set; }
    public decimal TeacherSharePercent { get; set; } = 60m;
    public PrivateLessonStatus Status { get; set; } = PrivateLessonStatus.Scheduled;
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<TeacherRating> Ratings { get; set; } = new List<TeacherRating>();
}
