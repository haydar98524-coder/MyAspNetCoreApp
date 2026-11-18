namespace KirkukInstitute.Models;

public class Stage
{
    public int Id { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public StageType Type { get; set; }
    public int Order { get; set; }
    public ICollection<Grade> Grades { get; set; } = new List<Grade>();
}

public class Grade
{
    public int Id { get; set; }
    public int StageId { get; set; }
    public Stage Stage { get; set; } = null!;
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public int Level { get; set; }
    public GradeBranch Branch { get; set; } = GradeBranch.General;
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}

public class Subject
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Color { get; set; } = "#0f766e";
    public bool IsActive { get; set; } = true;
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}

public class Course
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public int StageId { get; set; }
    public Stage Stage { get; set; } = null!;
    public int GradeId { get; set; }
    public Grade Grade { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public CourseType Type { get; set; } = CourseType.Group;
    public long Fee { get; set; }
    public decimal TeacherSharePercent { get; set; } = 55m;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Capacity { get; set; } = 30;
    public CourseStatus Status { get; set; } = CourseStatus.Active;
    public string DescriptionAr { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public int? BranchId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<CourseSection> Sections { get; set; } = new List<CourseSection>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
}

public class CourseSection
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string Name { get; set; } = "";
    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
    public int Capacity { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public ICollection<ClassSchedule> Schedules { get; set; } = new List<ClassSchedule>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}

public class ClassSchedule
{
    public int Id { get; set; }
    public int SectionId { get; set; }
    public CourseSection Section { get; set; } = null!;
    public int ClassroomId { get; set; }
    public Classroom Classroom { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public string StartTime { get; set; } = "09:00";
    public string EndTime { get; set; } = "11:00";
}

public class Exam
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public ExamType Type { get; set; } = ExamType.Monthly;
    public DateTime Date { get; set; }
    public int MaxMark { get; set; } = 100;
    public int PassMark { get; set; } = 50;
    public ICollection<ExamResult> Results { get; set; } = new List<ExamResult>();
}

public class ExamResult
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public Exam Exam { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public decimal Mark { get; set; }
    public string? Notes { get; set; }
}
