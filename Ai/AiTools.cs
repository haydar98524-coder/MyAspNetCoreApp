using KirkukInstitute.Models;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Ai;

/// <summary>The complete, fixed list of assistant tools. Every tool is a read-only query.</summary>
public static class AiToolCatalog
{
    public static IEnumerable<AiTool> All() => new AiTool[]
    {
        new InstituteOverviewTool(),
        new SearchStudentsTool(),
        new StudentProfileTool(),
        new AttendanceReportTool(),
        new CourseReportTool(),
        new ExamResultsSummaryTool(),
        new FinancialSummaryTool(),
        new OutstandingDuesTool(),
        new TeacherOverviewTool(),
        new HrSummaryTool(),
        new FindSystemPageTool()
    };
}

// ─────────────────────────────────────────────────────────────────────────────
public sealed class InstituteOverviewTool : AiTool
{
    public override string Name => "get_institute_overview";
    public override string Description =>
        "Read-only. Headline KPIs of the institute right now: student, teacher, course, enrollment, classroom and employee " +
        "counts, today's student attendance, pending requests and (finance roles only) this month's collections and outstanding dues.";
    public override string AllowedRoles => Roles.Staff;
    protected override string ParametersSchema => """{"type":"object","properties":{},"required":[]}""";

    public override async Task<object> ExecuteAsync(AiToolContext ctx, ToolArgs args, CancellationToken ct)
    {
        var db = ctx.Db;
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var attendance = await db.StudentAttendances.Where(a => a.Date >= today && a.Date < tomorrow)
            .GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int Att(AttendanceStatus s) => attendance.FirstOrDefault(a => a.Key == s)?.Count ?? 0;
        var attTotal = attendance.Sum(a => a.Count);

        object? finance = null;
        if (ctx.Caller.CanSeeFinance)
        {
            var collected = await db.Payments.Where(p => p.Date >= monthStart && p.Date < tomorrow).SumAsync(p => (long?)p.Amount, ct) ?? 0;
            var outstanding = await db.Dues.Where(d => d.Status == DueStatus.Unpaid || d.Status == DueStatus.Partial)
                .SumAsync(d => (long?)(d.Amount - d.PaidAmount), ct) ?? 0;
            finance = new { currency = "IQD", collected_this_month = collected, outstanding_dues = outstanding };
        }

        return new
        {
            as_of = D(today),
            students = new
            {
                total = await db.Students.CountAsync(ct),
                active = await db.Students.CountAsync(s => s.Status == StudentStatus.Active, ct),
                registered_this_month = await db.Students.CountAsync(s => s.RegistrationDate >= monthStart, ct)
            },
            teachers_active = await db.Teachers.CountAsync(t => t.IsActive, ct),
            courses_active = await db.Courses.CountAsync(c => c.Status == CourseStatus.Active, ct),
            enrollments_active = await db.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Active, ct),
            classrooms_available = await db.Classrooms.CountAsync(c => c.Status == ClassroomStatus.Available, ct),
            employees_active = await db.Employees.CountAsync(e => e.Status == StaffStatus.Active, ct),
            attendance_today = new
            {
                recorded = attTotal,
                present = Att(AttendanceStatus.Present),
                late = Att(AttendanceStatus.Late),
                absent = Att(AttendanceStatus.Absent),
                excused = Att(AttendanceStatus.Excused),
                attendance_rate_pct = Pct(Att(AttendanceStatus.Present) + Att(AttendanceStatus.Late), attTotal)
            },
            pending = new
            {
                private_lesson_requests = await db.PrivateLessonRequests.CountAsync(r => r.Status == RequestStatus.Pending, ct),
                attendance_change_requests = await db.AttendanceChangeRequests.CountAsync(r => r.Status == ChangeRequestStatus.Pending, ct)
            },
            finance
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
public sealed class SearchStudentsTool : AiTool
{
    public override string Name => "search_students";
    public override string Description =>
        "Read-only. Search students by name (Arabic or English) or student number, optionally filtered by grade and status. " +
        "Returns at most 25 rows plus the total number of matches.";
    public override string AllowedRoles => Roles.Admissions;
    protected override string ParametersSchema => """
        {"type":"object","properties":{
          "query":{"type":"string","description":"Part of the student's name or student number"},
          "grade":{"type":"string","description":"Part of the grade name, e.g. 'السادس' or 'Sixth'"},
          "status":{"type":"string","enum":["Active","Suspended","Graduated","Withdrawn"]},
          "limit":{"type":"integer","minimum":1,"maximum":25,"description":"Default 10"}
        },"required":[]}
        """;

    public override async Task<object> ExecuteAsync(AiToolContext ctx, ToolArgs args, CancellationToken ct)
    {
        var query = args.Str("query");
        var grade = args.Str("grade");
        var status = args.Enum<StudentStatus>("status");
        var limit = args.Int("limit", 1, 25) ?? 10;

        var q = ctx.Db.Students.AsQueryable();
        if (query is not null) q = q.Where(s => s.FullNameAr.Contains(query) || s.FullNameEn.Contains(query) || s.StudentNo.Contains(query));
        if (grade is not null) q = q.Where(s => s.Grade.NameAr.Contains(grade) || s.Grade.NameEn.Contains(grade));
        if (status is not null) q = q.Where(s => s.Status == status);

        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(s => s.FullNameAr).Take(limit).Select(s => new
        {
            s.Id, s.StudentNo, s.FullNameAr, s.FullNameEn, GradeAr = s.Grade.NameAr, GradeEn = s.Grade.NameEn, s.Status,
            ParentAr = s.Parent != null ? s.Parent.FullNameAr : null, ParentEn = s.Parent != null ? s.Parent.FullNameEn : null,
            s.RegistrationDate,
            ActiveEnrollments = s.Enrollments.Count(e => e.Status == EnrollmentStatus.Active)
        }).ToListAsync(ct);

        return new
        {
            total_matches = total,
            returned = rows.Count,
            students = rows.Select(s => new
            {
                id = s.Id, student_no = s.StudentNo, name = ctx.Pick(s.FullNameAr, s.FullNameEn),
                grade = ctx.Pick(s.GradeAr, s.GradeEn), status = s.Status.ToString(),
                parent = ctx.Pick(s.ParentAr, s.ParentEn), registered = D(s.RegistrationDate),
                active_enrollments = s.ActiveEnrollments
            })
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
public sealed class StudentProfileTool : AiTool
{
    public override string Name => "get_student_profile";
    public override string Description =>
        "Read-only. Full profile of ONE student: details, enrollments, attendance summary, latest exam results and fee " +
        "position (dues, paid, remaining, latest receipts). Identify the student by id, student_no or name.";
    public override string AllowedRoles => Roles.Admissions;
    protected override string ParametersSchema => """
        {"type":"object","properties":{
          "student_id":{"type":"integer","minimum":1},
          "student_no":{"type":"string","description":"Exact student number"},
          "name":{"type":"string","description":"Part of the student's name"}
        },"required":[]}
        """;

    public override async Task<object> ExecuteAsync(AiToolContext ctx, ToolArgs args, CancellationToken ct)
    {
        var db = ctx.Db;
        var id = args.Int("student_id", 1, int.MaxValue);
        var no = args.Str("student_no", 32);
        var name = args.Str("name");
        if (id is null && no is null && name is null)
            throw new ToolArgumentException("Provide student_id, student_no or name.");

        if (id is null)
        {
            var candidates = await db.Students
                .Where(s => (no != null && s.StudentNo == no) || (name != null && (s.FullNameAr.Contains(name) || s.FullNameEn.Contains(name))))
                .OrderBy(s => s.FullNameAr).Take(10)
                .Select(s => new { s.Id, s.StudentNo, s.FullNameAr, s.FullNameEn, GradeAr = s.Grade.NameAr, GradeEn = s.Grade.NameEn })
                .ToListAsync(ct);
            if (candidates.Count == 0) return new { found = false, message = "No student matches." };
            if (candidates.Count > 1)
                return new
                {
                    found = false,
                    message = "Several students match. Ask the user which one, or call again with student_id.",
                    candidates = candidates.Select(c => new { id = c.Id, student_no = c.StudentNo, name = ctx.Pick(c.FullNameAr, c.FullNameEn), grade = ctx.Pick(c.GradeAr, c.GradeEn) })
                };
            id = candidates[0].Id;
        }

        var s = await db.Students.Where(x => x.Id == id).Select(x => new
        {
            x.Id, x.StudentNo, x.FullNameAr, x.FullNameEn, x.Gender, x.BirthDate, x.SchoolName, x.Area,
            GradeAr = x.Grade.NameAr, GradeEn = x.Grade.NameEn, x.Status, x.RegistrationDate,
            ParentAr = x.Parent != null ? x.Parent.FullNameAr : null, ParentEn = x.Parent != null ? x.Parent.FullNameEn : null
        }).FirstOrDefaultAsync(ct);
        if (s is null) return new { found = false, message = "No student with this id." };

        var enrollments = await db.Enrollments.Where(e => e.StudentId == id).OrderByDescending(e => e.EnrollmentDate).Take(20)
            .Select(e => new
            {
                e.Course.Code, CourseAr = e.Course.NameAr, CourseEn = e.Course.NameEn,
                TeacherAr = e.Course.Teacher.FullNameAr, TeacherEn = e.Course.Teacher.FullNameEn,
                Section = e.Section != null ? e.Section.Name : null, e.Status, e.EnrollmentDate, e.NetFee
            }).ToListAsync(ct);

        var att = await db.StudentAttendances.Where(a => a.StudentId == id)
            .GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int A(AttendanceStatus st) => att.FirstOrDefault(a => a.Key == st)?.Count ?? 0;
        var attTotal = att.Sum(a => a.Count);

        var results = await db.ExamResults.Where(r => r.StudentId == id).OrderByDescending(r => r.Exam.Date).Take(10)
            .Select(r => new
            {
                ExamAr = r.Exam.NameAr, ExamEn = r.Exam.NameEn, CourseAr = r.Exam.Course.NameAr, CourseEn = r.Exam.Course.NameEn,
                r.Exam.Date, r.Mark, r.Exam.MaxMark, r.Exam.PassMark
            }).ToListAsync(ct);

        var today = DateTime.Today;
        var dues = db.Dues.Where(d => d.StudentId == id && d.Status != DueStatus.Waived);
        var billed = await dues.SumAsync(d => (long?)d.Amount, ct) ?? 0;
        var paid = await dues.SumAsync(d => (long?)d.PaidAmount, ct) ?? 0;
        var open = dues.Where(d => d.Status == DueStatus.Unpaid || d.Status == DueStatus.Partial);
        var overdueCount = await open.CountAsync(d => d.DueDate < today, ct);
        var overdueAmount = await open.Where(d => d.DueDate < today).SumAsync(d => (long?)(d.Amount - d.PaidAmount), ct) ?? 0;
        var payments = await db.Payments.Where(p => p.StudentId == id).OrderByDescending(p => p.Date).ThenByDescending(p => p.Id).Take(5)
            .Select(p => new { p.ReceiptNo, p.Date, p.Amount, p.Method }).ToListAsync(ct);

        return new
        {
            found = true,
            student = new
            {
                id = s.Id, student_no = s.StudentNo, name = ctx.Pick(s.FullNameAr, s.FullNameEn), gender = s.Gender.ToString(),
                age = s.BirthDate == default ? (int?)null : (int)((today - s.BirthDate).TotalDays / 365.25),
                school = s.SchoolName, area = s.Area, grade = ctx.Pick(s.GradeAr, s.GradeEn), status = s.Status.ToString(),
                registered = D(s.RegistrationDate), parent = ctx.Pick(s.ParentAr, s.ParentEn)
            },
            enrollments = enrollments.Select(e => new
            {
                course_code = e.Code, course = ctx.Pick(e.CourseAr, e.CourseEn), teacher = ctx.Pick(e.TeacherAr, e.TeacherEn),
                section = e.Section, status = e.Status.ToString(), enrolled = D(e.EnrollmentDate), net_fee = e.NetFee
            }),
            attendance = new
            {
                sessions = attTotal, present = A(AttendanceStatus.Present), late = A(AttendanceStatus.Late),
                absent = A(AttendanceStatus.Absent), excused = A(AttendanceStatus.Excused),
                attendance_rate_pct = Pct(A(AttendanceStatus.Present) + A(AttendanceStatus.Late), attTotal),
                absence_pct = Pct(A(AttendanceStatus.Absent), attTotal),
                warning_threshold_pct = ctx.Settings.AttendanceWarningPercent
            },
            latest_exam_results = results.Select(r => new
            {
                exam = ctx.Pick(r.ExamAr, r.ExamEn), course = ctx.Pick(r.CourseAr, r.CourseEn), date = D(r.Date),
                mark = r.Mark, max_mark = r.MaxMark, pass_mark = r.PassMark, passed = r.Mark >= r.PassMark
            }),
            fees = new
            {
                currency = "IQD", billed, paid, remaining = Math.Max(0, billed - paid),
                overdue_instalments = overdueCount, overdue_amount = overdueAmount,
                latest_payments = payments.Select(p => new { receipt_no = p.ReceiptNo, date = D(p.Date), amount = p.Amount, method = p.Method.ToString() })
            }
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
public sealed class AttendanceReportTool : AiTool
{
    public override string Name => "get_attendance_report";
    public override string Description =>
        "Read-only analytics. Student attendance for a date range (default: last 30 days, max 366): totals by status, " +
        "attendance rate, breakdown per course and the students whose absence exceeds the warning threshold.";
    public override string AllowedRoles => Roles.TeachingStaff;
    protected override string ParametersSchema => """
        {"type":"object","properties":{
          "date_from":{"type":"string","description":"YYYY-MM-DD"},
          "date_to":{"type":"string","description":"YYYY-MM-DD"},
          "course":{"type":"string","description":"Optional part of a course name or code"}
        },"required":[]}
        """;

    public override async Task<object> ExecuteAsync(AiToolContext ctx, ToolArgs args, CancellationToken ct)
    {
        var db = ctx.Db;
        var today = DateTime.Today;
        var (from, toEx) = args.Range("date_from", "date_to", today.AddDays(-29), today);
        var course = args.Str("course");

        var q = db.StudentAttendances.Where(a => a.Date >= from && a.Date < toEx);
        if (course is not null)
        {
            var ids = await db.Courses.Where(c => c.NameAr.Contains(course) || c.NameEn.Contains(course) || c.Code.Contains(course))
                .Select(c => c.Id).Take(50).ToListAsync(ct);
            q = q.Where(a => ids.Contains(a.CourseId));
        }

        var byStatus = await q.GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int S(AttendanceStatus st) => byStatus.FirstOrDefault(a => a.Key == st)?.Count ?? 0;
        var total = byStatus.Sum(a => a.Count);

        var byCourse = await q.GroupBy(a => a.CourseId).Select(g => new
        {
            CourseId = g.Key, Total = g.Count(),
            Present = g.Count(x => x.Status == AttendanceStatus.Present),
            Late = g.Count(x => x.Status == AttendanceStatus.Late),
            Absent = g.Count(x => x.Status == AttendanceStatus.Absent),
            Excused = g.Count(x => x.Status == AttendanceStatus.Excused)
        }).OrderByDescending(x => x.Total).Take(20).ToListAsync(ct);
        var courseIds = byCourse.Select(c => c.CourseId).ToList();
        var courses = await db.Courses.Where(c => courseIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Code, c.NameAr, c.NameEn }).ToDictionaryAsync(c => c.Id, ct);

        var warn = Math.Clamp(ctx.Settings.AttendanceWarningPercent, 1, 100);
        var atRisk = await q.GroupBy(a => a.StudentId).Select(g => new
        {
            StudentId = g.Key, Total = g.Count(), Absent = g.Count(x => x.Status == AttendanceStatus.Absent)
        }).Where(x => x.Total >= 3 && x.Absent * 100 >= warn * x.Total)
          .OrderByDescending(x => x.Absent).Take(15).ToListAsync(ct);
        var riskIds = atRisk.Select(r => r.StudentId).ToList();
        var students = await db.Students.Where(s => riskIds.Contains(s.Id))
            .Select(s => new { s.Id, s.StudentNo, s.FullNameAr, s.FullNameEn }).ToDictionaryAsync(s => s.Id, ct);

        return new
        {
            period = new { from = D(from), to = D(toEx.AddDays(-1)) },
            course_filter = course,
            totals = new
            {
                records = total, present = S(AttendanceStatus.Present), late = S(AttendanceStatus.Late),
                absent = S(AttendanceStatus.Absent), excused = S(AttendanceStatus.Excused),
                attendance_rate_pct = Pct(S(AttendanceStatus.Present) + S(AttendanceStatus.Late), total)
            },
            by_course = byCourse.Select(c => new
            {
                course_code = courses.TryGetValue(c.CourseId, out var cc) ? cc.Code : null,
                course = cc is null ? null : ctx.Pick(cc.NameAr, cc.NameEn),
                records = c.Total, present = c.Present, late = c.Late, absent = c.Absent, excused = c.Excused,
                attendance_rate_pct = Pct(c.Present + c.Late, c.Total)
            }),
            warning_threshold_pct = warn,
            students_over_absence_threshold = atRisk.Select(r => new
            {
                student_no = students.TryGetValue(r.StudentId, out var st) ? st.StudentNo : null,
                name = st is null ? null : ctx.Pick(st.FullNameAr, st.FullNameEn),
                sessions = r.Total, absent = r.Absent, absence_pct = Pct(r.Absent, r.Total)
            })
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
public sealed class CourseReportTool : AiTool
{
    public override string Name => "get_course_report";
    public override string Description =>
        "Read-only. Without 'course': list courses with teacher, grade, fee, capacity and active enrollments. " +
        "With 'course' matching exactly one course: a detailed report (enrollment, sections, exam averages, pass rate, " +
        "attendance rate and, for finance roles, billed vs collected).";
    public override string AllowedRoles => Roles.Admissions;
    protected override string ParametersSchema => """
        {"type":"object","properties":{
          "course":{"type":"string","description":"Part of the course name or its code"},
          "status":{"type":"string","enum":["Draft","Active","Completed","Cancelled"]},
          "limit":{"type":"integer","minimum":1,"maximum":50,"description":"Default 20"}
        },"required":[]}
        """;

    public override async Task<object> ExecuteAsync(AiToolContext ctx, ToolArgs args, CancellationToken ct)
    {
        var db = ctx.Db;
        var course = args.Str("course");
        var status = args.Enum<CourseStatus>("status");
        var limit = args.Int("limit", 1, 50) ?? 20;

        var q = db.Courses.AsQueryable();
        if (course is not null) q = q.Where(c => c.Code == course || c.NameAr.Contains(course) || c.NameEn.Contains(course) || c.Code.Contains(course));
        if (status is not null) q = q.Where(c => c.Status == status);

        var exact = course is null ? null : await q.Where(c => c.Code == course).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        var matchCount = await q.CountAsync(ct);
        if (course is not null && (exact is not null || matchCount == 1))
            return await DetailAsync(ctx, exact ?? await q.Select(c => c.Id).FirstAsync(ct), ct);

        var rows = await q.OrderByDescending(c => c.Enrollments.Count(e => e.Status == EnrollmentStatus.Active)).Take(limit).Select(c => new
        {
            c.Code, c.NameAr, c.NameEn, TeacherAr = c.Teacher.FullNameAr, TeacherEn = c.Teacher.FullNameEn,
            GradeAr = c.Grade.NameAr, GradeEn = c.Grade.NameEn, SubjectAr = c.Subject.NameAr, SubjectEn = c.Subject.NameEn,
            c.Type, c.Status, c.Fee, c.Capacity, Active = c.Enrollments.Count(e => e.Status == EnrollmentStatus.Active)
        }).ToListAsync(ct);

        return new
        {
            total_matches = matchCount,
            returned = rows.Count,
            currency = "IQD",
            courses = rows.Select(c => new
            {
                code = c.Code, name = ctx.Pick(c.NameAr, c.NameEn), teacher = ctx.Pick(c.TeacherAr, c.TeacherEn),
                grade = ctx.Pick(c.GradeAr, c.GradeEn), subject = ctx.Pick(c.SubjectAr, c.SubjectEn),
                type = c.Type.ToString(), status = c.Status.ToString(), fee = c.Fee, capacity = c.Capacity,
                active_enrollments = c.Active, fill_pct = Pct(c.Active, c.Capacity)
            })
        };
    }

    private static async Task<object> DetailAsync(AiToolContext ctx, int id, CancellationToken ct)
    {
        var db = ctx.Db;
        var c = await db.Courses.Where(x => x.Id == id).Select(x => new
        {
            x.Code, x.NameAr, x.NameEn, SubjectAr = x.Subject.NameAr, SubjectEn = x.Subject.NameEn,
            GradeAr = x.Grade.NameAr, GradeEn = x.Grade.NameEn, TeacherAr = x.Teacher.FullNameAr, TeacherEn = x.Teacher.FullNameEn,
            x.Type, x.Status, x.Fee, x.TeacherSharePercent, x.Capacity, x.StartDate, x.EndDate,
            Active = x.Enrollments.Count(e => e.Status == EnrollmentStatus.Active),
            Completed = x.Enrollments.Count(e => e.Status == EnrollmentStatus.Completed),
            Cancelled = x.Enrollments.Count(e => e.Status == EnrollmentStatus.Cancelled),
            Sections = x.Sections.Count(s => s.IsActive),
            Exams = x.Exams.Count()
        }).FirstAsync(ct);

        var results = db.ExamResults.Where(r => r.Exam.CourseId == id && r.Exam.MaxMark > 0);
        var resultCount = await results.CountAsync(ct);
        var avgPct = await results.AverageAsync(r => (decimal?)(r.Mark * 100m / r.Exam.MaxMark), ct);
        var passed = await results.CountAsync(r => r.Mark >= r.Exam.PassMark, ct);

        var att = db.StudentAttendances.Where(a => a.CourseId == id);
        var attTotal = await att.CountAsync(ct);
        var attPresent = await att.CountAsync(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late, ct);

        object? finance = null;
        if (ctx.Caller.CanSeeFinance)
        {
            var billed = await db.Enrollments.Where(e => e.CourseId == id && e.Status != EnrollmentStatus.Cancelled).SumAsync(e => (long?)e.NetFee, ct) ?? 0;
            var collected = await db.Payments.Where(p => p.Enrollment != null && p.Enrollment.CourseId == id).SumAsync(p => (long?)p.Amount, ct) ?? 0;
            var teacherShare = await db.TeacherEarnings.Where(t => t.CourseId == id).SumAsync(t => (long?)t.Amount, ct) ?? 0;
            finance = new { currency = "IQD", billed, collected, uncollected = Math.Max(0, billed - collected), teacher_share_accrued = teacherShare };
        }

        return new
        {
            course = new
            {
                code = c.Code, name = ctx.Pick(c.NameAr, c.NameEn), subject = ctx.Pick(c.SubjectAr, c.SubjectEn),
                grade = ctx.Pick(c.GradeAr, c.GradeEn), teacher = ctx.Pick(c.TeacherAr, c.TeacherEn),
                type = c.Type.ToString(), status = c.Status.ToString(), fee = c.Fee, teacher_share_pct = c.TeacherSharePercent,
                capacity = c.Capacity, start = D(c.StartDate), end = D(c.EndDate)
            },
            enrollments = new { active = c.Active, completed = c.Completed, cancelled = c.Cancelled, fill_pct = Pct(c.Active, c.Capacity) },
            active_sections = c.Sections,
            exams = new
            {
                count = c.Exams, graded_results = resultCount,
                average_pct = avgPct is null ? (decimal?)null : Math.Round(avgPct.Value, 1),
                pass_rate_pct = Pct(passed, resultCount)
            },
            attendance = new { records = attTotal, attendance_rate_pct = Pct(attPresent, attTotal) },
            finance
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
public sealed class ExamResultsSummaryTool : AiTool
{
    public override string Name => "get_exam_results_summary";
    public override string Description =>
        "Read-only analytics. Exam statistics (results count, average, min, max, pass rate) filtered by exam name, course " +
        "and/or date range. When exactly one exam matches, also returns its top and bottom five students.";
    public override string AllowedRoles => Roles.TeachingStaff;
    protected override string ParametersSchema => """
        {"type":"object","properties":{
          "exam":{"type":"string","description":"Part of the exam name"},
          "course":{"type":"string","description":"Part of the course name or code"},
          "date_from":{"type":"string","description":"YYYY-MM-DD"},
          "date_to":{"type":"string","description":"YYYY-MM-DD"},
          "limit":{"type":"integer","minimum":1,"maximum":25,"description":"Default 10"}
        },"required":[]}
        """;

    public override async Task<object> ExecuteAsync(AiToolContext ctx, ToolArgs args, CancellationToken ct)
    {
        var db = ctx.Db;
        var exam = args.Str("exam");
        var course = args.Str("course");
        var limit = args.Int("limit", 1, 25) ?? 10;
        var hasRange = args.Date("date_from") is not null || args.Date("date_to") is not null;

        var q = db.Exams.AsQueryable();
        if (exam is not null) q = q.Where(e => e.NameAr.Contains(exam) || e.NameEn.Contains(exam));
        if (course is not null) q = q.Where(e => e.Course.NameAr.Contains(course) || e.Course.NameEn.Contains(course) || e.Course.Code.Contains(course));
        if (hasRange)
        {
            var (from, toEx) = args.Range("date_from", "date_to", DateTime.Today.AddYears(-1), DateTime.Today);
            q = q.Where(e => e.Date >= from && e.Date < toEx);
        }

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(e => e.Date).Take(limit).Select(e => new
        {
            e.Id, e.NameAr, e.NameEn, CourseAr = e.Course.NameAr, CourseEn = e.Course.NameEn, e.Type, e.Date, e.MaxMark, e.PassMark,
            Count = e.Results.Count(),
            Avg = e.Results.Average(r => (decimal?)r.Mark),
            Min = e.Results.Min(r => (decimal?)r.Mark),
            Max = e.Results.Max(r => (decimal?)r.Mark),
            Passed = e.Results.Count(r => r.Mark >= e.PassMark)
        }).ToListAsync(ct);

        object? ranking = null;
        if (rows.Count == 1)
        {
            var examId = rows[0].Id;
            var marks = db.ExamResults.Where(r => r.ExamId == examId);
            var top = await marks.OrderByDescending(r => r.Mark).Take(5).Select(r => new { r.Student.StudentNo, r.Student.FullNameAr, r.Student.FullNameEn, r.Mark }).ToListAsync(ct);
            var bottom = await marks.OrderBy(r => r.Mark).Take(5).Select(r => new { r.Student.StudentNo, r.Student.FullNameAr, r.Student.FullNameEn, r.Mark }).ToListAsync(ct);
            ranking = new
            {
                top_five = top.Select(t => new { student_no = t.StudentNo, name = ctx.Pick(t.FullNameAr, t.FullNameEn), mark = t.Mark }),
                bottom_five = bottom.Select(t => new { student_no = t.StudentNo, name = ctx.Pick(t.FullNameAr, t.FullNameEn), mark = t.Mark })
            };
        }

        return new
        {
            total_matches = total,
            returned = rows.Count,
            exams = rows.Select(e => new
            {
                exam = ctx.Pick(e.NameAr, e.NameEn), course = ctx.Pick(e.CourseAr, e.CourseEn), type = e.Type.ToString(),
                date = D(e.Date), max_mark = e.MaxMark, pass_mark = e.PassMark, results = e.Count,
                average = e.Avg is null ? (decimal?)null : Math.Round(e.Avg.Value, 1), min = e.Min, max = e.Max,
                passed = e.Passed, pass_rate_pct = Pct(e.Passed, e.Count)
            }),
            ranking
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
public sealed class FinancialSummaryTool : AiTool
{
    public override string Name => "get_financial_summary";
    public override string Description =>
        "Read-only financial REPORT (never a transaction). Revenue by source, costs, net profit, payments by method, top " +
        "expense accounts, outstanding dues, unpaid teacher shares and fund balances for a date range " +
        "(default: this month to date, max 366 days). Amounts in IQD.";
    public override string AllowedRoles => Roles.Finance;
    protected override string ParametersSchema => """
        {"type":"object","properties":{
          "date_from":{"type":"string","description":"YYYY-MM-DD"},
          "date_to":{"type":"string","description":"YYYY-MM-DD"}
        },"required":[]}
        """;

    public override async Task<object> ExecuteAsync(AiToolContext ctx, ToolArgs args, CancellationToken ct)
    {
        var db = ctx.Db;
        var today = DateTime.Today;
        var (from, toEx) = args.Range("date_from", "date_to", new DateTime(today.Year, today.Month, 1), today);

        var tuition = await db.Payments.Where(x => x.Date >= from && x.Date < toEx).SumAsync(x => (long?)x.Amount, ct) ?? 0;
        var other = await db.Incomes.Where(x => x.Date >= from && x.Date < toEx).SumAsync(x => (long?)x.Amount, ct) ?? 0;
        var halls = await db.ClassroomBookings.Where(x => x.PaymentStatus == BookingPaymentStatus.Paid && x.Date >= from && x.Date < toEx).SumAsync(x => (long?)x.TotalAmount, ct) ?? 0;
        var cafeteria = await db.CafeteriaPayments.Where(x => x.IsPaid && x.PaidDate >= from && x.PaidDate < toEx).SumAsync(x => (long?)x.Amount, ct) ?? 0;
        var teacherRent = await db.TeacherRentPayments.Where(x => x.IsPaid && x.PaidDate >= from && x.PaidDate < toEx).SumAsync(x => (long?)x.Amount, ct) ?? 0;
        var privates = await db.PrivateLessons.Where(x => x.IsPaid && x.PaidDate >= from && x.PaidDate < toEx).SumAsync(x => (long?)x.Fee, ct) ?? 0;

        var teacherCost = await db.TeacherEarnings.Where(x => x.Date >= from && x.Date < toEx).SumAsync(x => (long?)x.Amount, ct) ?? 0;
        var payroll = await db.Payrolls.Where(x => x.Status == PayrollStatus.Paid && x.PaidAt >= from && x.PaidAt < toEx).SumAsync(x => (long?)x.Net, ct) ?? 0;
        var expensesQ = db.Expenses.Where(x => x.Status == ExpenseStatus.Approved && x.Date >= from && x.Date < toEx);
        var expenses = await expensesQ.SumAsync(x => (long?)x.Amount, ct) ?? 0;
        var topExpenses = await expensesQ.GroupBy(x => new { x.Account.NameAr, x.Account.NameEn })
            .Select(g => new { g.Key.NameAr, g.Key.NameEn, Amount = g.Sum(x => x.Amount) })
            .OrderByDescending(x => x.Amount).Take(8).ToListAsync(ct);

        var byMethod = await db.Payments.Where(x => x.Date >= from && x.Date < toEx).GroupBy(x => x.Method)
            .Select(g => new { g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount) }).ToListAsync(ct);

        var outstanding = await db.Dues.Where(d => d.Status == DueStatus.Unpaid || d.Status == DueStatus.Partial)
            .SumAsync(d => (long?)(d.Amount - d.PaidAmount), ct) ?? 0;
        var teacherUnpaid = await db.TeacherEarnings.Where(x => !x.IsPaid).SumAsync(x => (long?)x.Amount, ct) ?? 0;
        var funds = await db.Funds.Where(f => f.IsActive).OrderBy(f => f.Type)
            .Select(f => new { f.NameAr, f.NameEn, f.Type, f.CurrentBalance }).ToListAsync(ct);

        var revenue = tuition + other + halls + cafeteria + teacherRent + privates;
        var costs = teacherCost + payroll + expenses;

        return new
        {
            period = new { from = D(from), to = D(toEx.AddDays(-1)) },
            currency = "IQD",
            revenue = new
            {
                tuition_fees = tuition, private_lessons = privates, hall_rentals = halls, cafeteria, teacher_room_rent = teacherRent,
                other_income = other, total = revenue
            },
            costs = new { teacher_shares_accrued = teacherCost, payroll_paid = payroll, direct_expenses = expenses, total = costs },
            net_profit = revenue - costs,
            payments_by_method = byMethod.Select(m => new { method = m.Key.ToString(), count = m.Count, amount = m.Amount }),
            top_expense_accounts = topExpenses.Select(e => new { account = ctx.Pick(e.NameAr, e.NameEn), amount = e.Amount }),
            position_now = new
            {
                outstanding_student_dues = outstanding,
                unpaid_teacher_shares = teacherUnpaid,
                cash_balance = funds.Where(f => f.Type == FundType.Cash).Sum(f => f.CurrentBalance),
                bank_balance = funds.Where(f => f.Type == FundType.Bank).Sum(f => f.CurrentBalance),
                funds = funds.Select(f => new { name = ctx.Pick(f.NameAr, f.NameEn), type = f.Type.ToString(), balance = f.CurrentBalance })
            }
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
public sealed class OutstandingDuesTool : AiTool
{
    public override string Name => "list_outstanding_dues";
    public override string Description =>
        "Read-only report. Unpaid or partially paid student instalments ordered by due date, with totals. Optional filters: " +
        "overdue only, grade, student name/number. Max 50 rows.";
    public override string AllowedRoles => Roles.Admissions;
    protected override string ParametersSchema => """
        {"type":"object","properties":{
          "overdue_only":{"type":"boolean","description":"Only instalments whose due date has passed"},
          "grade":{"type":"string","description":"Part of the grade name"},
          "student":{"type":"string","description":"Part of the student's name or student number"},
          "limit":{"type":"integer","minimum":1,"maximum":50,"description":"Default 20"}
        },"required":[]}
        """;

    public override async Task<object> ExecuteAsync(AiToolContext ctx, ToolArgs args, CancellationToken ct)
    {
        var db = ctx.Db;
        var today = DateTime.Today;
        var overdueOnly = args.Bool("overdue_only", false);
        var grade = args.Str("grade");
        var student = args.Str("student");
        var limit = args.Int("limit", 1, 50) ?? 20;

        var q = db.Dues.Where(d => d.Status == DueStatus.Unpaid || d.Status == DueStatus.Partial);
        if (overdueOnly) q = q.Where(d => d.DueDate < today);
        if (grade is not null) q = q.Where(d => d.Student.Grade.NameAr.Contains(grade) || d.Student.Grade.NameEn.Contains(grade));
        if (student is not null) q = q.Where(d => d.Student.FullNameAr.Contains(student) || d.Student.FullNameEn.Contains(student) || d.Student.StudentNo.Contains(student));

        var count = await q.CountAsync(ct);
        var remaining = await q.SumAsync(d => (long?)(d.Amount - d.PaidAmount), ct) ?? 0;
        var overdueCount = await q.CountAsync(d => d.DueDate < today, ct);
        var overdueAmount = await q.Where(d => d.DueDate < today).SumAsync(d => (long?)(d.Amount - d.PaidAmount), ct) ?? 0;
        var students = await q.Select(d => d.StudentId).Distinct().CountAsync(ct);

        var rows = await q.OrderBy(d => d.DueDate).Take(limit).Select(d => new
        {
            d.Student.StudentNo, d.Student.FullNameAr, d.Student.FullNameEn, GradeAr = d.Student.Grade.NameAr, GradeEn = d.Student.Grade.NameEn,
            CourseAr = d.Enrollment.Course.NameAr, CourseEn = d.Enrollment.Course.NameEn,
            d.Description, d.Amount, d.PaidAmount, d.DueDate, d.Status
        }).ToListAsync(ct);

        return new
        {
            currency = "IQD",
            as_of = D(today),
            totals = new
            {
                instalments = count, students, remaining_amount = remaining,
                overdue_instalments = overdueCount, overdue_amount = overdueAmount
            },
            returned = rows.Count,
            dues = rows.Select(d => new
            {
                student_no = d.StudentNo, student = ctx.Pick(d.FullNameAr, d.FullNameEn), grade = ctx.Pick(d.GradeAr, d.GradeEn),
                course = ctx.Pick(d.CourseAr, d.CourseEn), description = d.Description, amount = d.Amount, paid = d.PaidAmount,
                remaining = Math.Max(0, d.Amount - d.PaidAmount), due_date = D(d.DueDate), status = d.Status.ToString(),
                days_overdue = d.DueDate < today ? (int)(today - d.DueDate).TotalDays : 0
            })
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
public sealed class TeacherOverviewTool : AiTool
{
    public override string Name => "get_teacher_overview";
    public override string Description =>
        "Read-only. Teachers with specialization, experience, rating, active courses, active students and completed " +
        "private lessons; finance roles also see accrued and unpaid earnings. Optional filter by name, code or specialization.";
    public override string AllowedRoles => Roles.Staff;
    protected override string ParametersSchema => """
        {"type":"object","properties":{
          "teacher":{"type":"string","description":"Part of the teacher's name, code or specialization"},
          "active_only":{"type":"boolean","description":"Default true"},
          "limit":{"type":"integer","minimum":1,"maximum":30,"description":"Default 15"}
        },"required":[]}
        """;

    public override async Task<object> ExecuteAsync(AiToolContext ctx, ToolArgs args, CancellationToken ct)
    {
        var db = ctx.Db;
        var teacher = args.Str("teacher");
        var activeOnly = args.Bool("active_only", true);
        var limit = args.Int("limit", 1, 30) ?? 15;

        var q = db.Teachers.AsQueryable();
        if (activeOnly) q = q.Where(t => t.IsActive);
        if (teacher is not null) q = q.Where(t => t.FullNameAr.Contains(teacher) || t.FullNameEn.Contains(teacher) || t.Code.Contains(teacher) || t.Specialization.Contains(teacher));

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(t => t.RatingAvg).ThenBy(t => t.FullNameAr).Take(limit).Select(t => new
        {
            t.Id, t.Code, t.FullNameAr, t.FullNameEn, t.Specialization, t.ExperienceYears, t.TeachingMode, t.IsActive, t.RatingAvg, t.RatingCount,
            ActiveCourses = t.Courses.Count(c => c.Status == CourseStatus.Active),
            ActiveStudents = t.Courses.SelectMany(c => c.Enrollments).Where(e => e.Status == EnrollmentStatus.Active).Select(e => e.StudentId).Distinct().Count()
        }).ToListAsync(ct);

        var ids = rows.Select(r => r.Id).ToList();
        var lessons = await db.PrivateLessons.Where(p => ids.Contains(p.TeacherId) && p.Status == PrivateLessonStatus.Completed)
            .GroupBy(p => p.TeacherId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        Dictionary<int, (long Accrued, long Unpaid)>? earnings = null;
        if (ctx.Caller.CanSeeFinance)
        {
            earnings = (await db.TeacherEarnings.Where(e => ids.Contains(e.TeacherId)).GroupBy(e => e.TeacherId)
                    .Select(g => new { g.Key, Accrued = g.Sum(x => x.Amount), Unpaid = g.Sum(x => x.IsPaid ? 0 : x.Amount) })
                    .ToListAsync(ct))
                .ToDictionary(x => x.Key, x => (x.Accrued, x.Unpaid));
        }

        return new
        {
            total_matches = total,
            returned = rows.Count,
            teachers = rows.Select(t => new
            {
                code = t.Code, name = ctx.Pick(t.FullNameAr, t.FullNameEn), specialization = t.Specialization,
                experience_years = t.ExperienceYears, teaching_mode = t.TeachingMode.ToString(), active = t.IsActive,
                rating_avg = Math.Round(t.RatingAvg, 2), ratings = t.RatingCount,
                active_courses = t.ActiveCourses, active_students = t.ActiveStudents,
                private_lessons_completed = lessons.GetValueOrDefault(t.Id),
                earnings_iqd = earnings is null ? null : (object)new
                {
                    accrued = earnings.GetValueOrDefault(t.Id).Accrued,
                    unpaid = earnings.GetValueOrDefault(t.Id).Unpaid
                }
            })
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
public sealed class HrSummaryTool : AiTool
{
    public override string Name => "get_hr_summary";
    public override string Description =>
        "Read-only HR report for one month (default: current month): employees by status and department, monthly salary " +
        "base, payroll totals by status and staff attendance with the most absent employees.";
    public override string AllowedRoles => Roles.People + "," + Roles.Accountant;
    protected override string ParametersSchema => """
        {"type":"object","properties":{
          "month":{"type":"string","description":"YYYY-MM"}
        },"required":[]}
        """;

    public override async Task<object> ExecuteAsync(AiToolContext ctx, ToolArgs args, CancellationToken ct)
    {
        var db = ctx.Db;
        var today = DateTime.Today;
        var month = args.Month("month") ?? new DateTime(today.Year, today.Month, 1);
        var next = month.AddMonths(1);

        var byStatus = await db.Employees.GroupBy(e => e.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var byDept = await db.Employees.Where(e => e.Status != StaffStatus.Inactive).GroupBy(e => e.Department)
            .Select(g => new { Department = g.Key, Count = g.Count(), Salary = g.Sum(x => x.BasicSalary + x.Allowances) })
            .OrderByDescending(x => x.Count).ToListAsync(ct);

        var payroll = await db.Payrolls.Where(p => p.Month >= month && p.Month < next).GroupBy(p => p.Status)
            .Select(g => new { g.Key, Count = g.Count(), Net = g.Sum(x => x.Net), Deductions = g.Sum(x => x.AbsenceDeduction + x.OtherDeductions + x.Advances) })
            .ToListAsync(ct);

        var att = db.EmployeeAttendances.Where(a => a.Date >= month && a.Date < next);
        var attByStatus = await att.GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var absentees = await att.Where(a => a.Status == AttendanceStatus.Absent).GroupBy(a => a.EmployeeId)
            .Select(g => new { g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).Take(5).ToListAsync(ct);
        var absIds = absentees.Select(a => a.Key).ToList();
        var names = await db.Employees.Where(e => absIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Code, e.FullNameAr, e.FullNameEn }).ToDictionaryAsync(e => e.Id, ct);

        return new
        {
            month = month.ToString("yyyy-MM"),
            currency = "IQD",
            employees_by_status = byStatus.Select(s => new { status = s.Key.ToString(), count = s.Count }),
            departments = byDept.Select(d => new { department = d.Department, employees = d.Count, monthly_salary_base = d.Salary }),
            payroll = new
            {
                records = payroll.Sum(p => p.Count),
                total_net = payroll.Sum(p => p.Net),
                total_deductions = payroll.Sum(p => p.Deductions),
                by_status = payroll.Select(p => new { status = p.Key.ToString(), count = p.Count, net = p.Net })
            },
            staff_attendance = attByStatus.Select(a => new { status = a.Key.ToString(), days = a.Count }),
            most_absent = absentees.Select(a => new
            {
                code = names.TryGetValue(a.Key, out var n) ? n.Code : null,
                name = n is null ? null : ctx.Pick(n.FullNameAr, n.FullNameEn),
                absent_days = a.Count
            })
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
public sealed class FindSystemPageTool : AiTool
{
    public override string Name => "find_system_page";
    public override string Description =>
        "Router. Finds the application page(s) where the user can view something or perform an action THEMSELVES " +
        "(e.g. register a student, collect a payment, record an expense). Use it whenever the user asks you to create, " +
        "change, delete or pay something: you cannot do it, so route them. Returns only pages their role can open.";
    public override string AllowedRoles => Roles.Staff;
    protected override string ParametersSchema => """
        {"type":"object","properties":{
          "topic":{"type":"string","description":"What the user wants to see or do, in their own words"}
        },"required":["topic"]}
        """;

    public override Task<object> ExecuteAsync(AiToolContext ctx, ToolArgs args, CancellationToken ct)
    {
        var topic = args.Str("topic", 200) ?? "";
        var allowed = AiRouteCatalog.ForCaller(ctx.Caller).ToList();
        var scored = AiRouteCatalog.Match(topic, ctx.Caller);

        var list = (scored.Count > 0 ? scored : allowed).Select(r => new
        {
            path = r.Path,
            title = ctx.Pick(r.TitleAr, r.TitleEn),
            kind = r.IsForm ? "form (the user performs the action here)" : "view"
        });

        return Task.FromResult<object>(new
        {
            matched = scored.Count > 0,
            note = "You cannot perform actions. Give the user the path; they complete any change themselves on that page.",
            pages = list
        });
    }
}
