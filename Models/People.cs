namespace KirkukInstitute.Models;

public class Parent
{
    public int Id { get; set; }
    public string FullNameAr { get; set; } = "";
    public string FullNameEn { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string Address { get; set; } = "";
    public string Area { get; set; } = "";
    public string? Occupation { get; set; }
    public string? UserId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Student> Children { get; set; } = new List<Student>();
    public ICollection<PrivateLessonRequest> LessonRequests { get; set; } = new List<PrivateLessonRequest>();
}

public class Student
{
    public int Id { get; set; }
    public string StudentNo { get; set; } = "";
    public string FullNameAr { get; set; } = "";
    public string FullNameEn { get; set; } = "";
    public string? PhotoPath { get; set; }
    public string? NationalId { get; set; }
    public Gender Gender { get; set; }
    public DateTime BirthDate { get; set; }
    public string? Phone { get; set; }
    public string SchoolName { get; set; } = "";
    public int GradeId { get; set; }
    public Grade Grade { get; set; } = null!;
    public int? HallId { get; set; }
    public Classroom? Hall { get; set; }
    public int? ParentId { get; set; }
    public Parent? Parent { get; set; }
    public string Address { get; set; } = "";
    public string Area { get; set; } = "";
    public StudentStatus Status { get; set; } = StudentStatus.Active;
    public string QrToken { get; set; } = "";
    public DateTime RegistrationDate { get; set; } = DateTime.Today;
    public string? Notes { get; set; }
    public string? UserId { get; set; }
    public int? BranchId { get; set; }
    public ICollection<StudentSubject> Subjects { get; set; } = new List<StudentSubject>();
    public ICollection<StudentTeacher> Teachers { get; set; } = new List<StudentTeacher>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<StudentAttendance> Attendances { get; set; } = new List<StudentAttendance>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Due> Dues { get; set; } = new List<Due>();
    public ICollection<ExamResult> ExamResults { get; set; } = new List<ExamResult>();
    public ICollection<PrivateLesson> PrivateLessons { get; set; } = new List<PrivateLesson>();
}

/// <summary>Many-to-many link between a student and the subjects they take.</summary>
public class StudentSubject
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
}

/// <summary>Many-to-many link between a student and the teachers assigned to them.</summary>
public class StudentTeacher
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
}

public class Teacher
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string FullNameAr { get; set; } = "";
    public string FullNameEn { get; set; } = "";
    public string? PhotoPath { get; set; }
    public Gender Gender { get; set; }
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string Specialization { get; set; } = "";
    public string Certificate { get; set; } = "";
    public string University { get; set; } = "";
    public int GraduationYear { get; set; }
    public int ExperienceYears { get; set; }
    public TeachingMode TeachingMode { get; set; } = TeachingMode.Institute;
    public string? BioAr { get; set; }
    public string? BioEn { get; set; }
    public string Areas { get; set; } = "";
    public bool IsPublic { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public double RatingAvg { get; set; }
    public int RatingCount { get; set; }
    public DateTime HireDate { get; set; } = DateTime.Today;
    public long? FixedMonthlySalary { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
    public ICollection<Course> Courses { get; set; } = new List<Course>();
    public ICollection<TeacherSubject> Subjects { get; set; } = new List<TeacherSubject>();
    public ICollection<TeacherGrade> Grades { get; set; } = new List<TeacherGrade>();
    public ICollection<TeacherCertificate> Certificates { get; set; } = new List<TeacherCertificate>();
    public ICollection<TeacherAvailability> Availabilities { get; set; } = new List<TeacherAvailability>();
    public ICollection<TeacherRating> Ratings { get; set; } = new List<TeacherRating>();
    public ICollection<TeacherEarning> Earnings { get; set; } = new List<TeacherEarning>();
    public ICollection<TeacherRentContract> RentContracts { get; set; } = new List<TeacherRentContract>();
}

public class TeacherSubject
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
}

public class TeacherGrade
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public int GradeId { get; set; }
    public Grade Grade { get; set; } = null!;
}

public class TeacherCertificate
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Issuer { get; set; } = "";
    public int Year { get; set; }
}

public class TeacherAvailability
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public string StartTime { get; set; } = "16:00";
    public string EndTime { get; set; } = "20:00";
}

public class TeacherRating
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public int? ParentId { get; set; }
    public Parent? Parent { get; set; }
    public int? LessonId { get; set; }
    public PrivateLesson? Lesson { get; set; }
    public string RatedByUserId { get; set; } = "";
    public string RatedByName { get; set; } = "";
    public int Explanation { get; set; } = 5;
    public int Punctuality { get; set; } = 5;
    public int Interaction { get; set; } = 5;
    public int FollowUp { get; set; } = 5;
    public int Overall { get; set; } = 5;
    public string? Comment { get; set; }
    public bool IsVisible { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal Average => Math.Round((Explanation + Punctuality + Interaction + FollowUp + Overall) / 5m, 1);
}

public class TeacherEarning
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public int? CourseId { get; set; }
    public Course? Course { get; set; }
    public int? EnrollmentId { get; set; }
    public Enrollment? Enrollment { get; set; }
    public int? PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public int? TeacherContractId { get; set; }
    public TeacherContract? TeacherContract { get; set; }
    public int? PrivateLessonId { get; set; }
    public PrivateLesson? PrivateLesson { get; set; }
    public TeacherEarningKind Kind { get; set; }
    public long BaseAmount { get; set; }
    public decimal SharePercent { get; set; }
    public long Amount { get; set; }
    public DateTime Date { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaidReference { get; set; }
}

public class TeacherRentContract
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public long MonthlyAmount { get; set; } = 250_000;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public ICollection<TeacherRentPayment> Payments { get; set; } = new List<TeacherRentPayment>();
}

public class TeacherRentPayment
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public TeacherRentContract Contract { get; set; } = null!;
    public DateTime Month { get; set; }
    public long Amount { get; set; }
    public DateTime? PaidDate { get; set; }
    public bool IsPaid { get; set; }
    public string? Reference { get; set; }
}

public class Employee
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string FullNameAr { get; set; } = "";
    public string FullNameEn { get; set; } = "";
    public string JobTitleAr { get; set; } = "";
    public string JobTitleEn { get; set; } = "";
    public string Department { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public long BasicSalary { get; set; }
    public long Allowances { get; set; }
    public DateTime HireDate { get; set; }
    public StaffStatus Status { get; set; } = StaffStatus.Active;
    public string? UserId { get; set; }
    public string? Notes { get; set; }
    public ICollection<EmployeeAttendance> Attendances { get; set; } = new List<EmployeeAttendance>();
    public ICollection<Payroll> Payrolls { get; set; } = new List<Payroll>();
}

public class EmployeeAttendance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateTime Date { get; set; }
    public string? CheckIn { get; set; }
    public string? CheckOut { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? Notes { get; set; }
}

public class Payroll
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateTime Month { get; set; }
    public long Basic { get; set; }
    public long Allowances { get; set; }
    public long Overtime { get; set; }
    public long AbsenceDeduction { get; set; }
    public long OtherDeductions { get; set; }
    public long Advances { get; set; }
    public long Net { get; set; }
    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;
    public DateTime? PaidAt { get; set; }
}
