using KirkukInstitute.Data;
using KirkukInstitute.Models;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Services;

public record TeacherMatch(Teacher Teacher, int Score, bool SubjectMatch, bool GradeMatch, bool AreaMatch);

public class MatchingService
{
    private readonly AppDbContext _db;
    public MatchingService(AppDbContext db) => _db = db;

    public async Task<List<TeacherMatch>> FindAsync(int? gradeId, int? subjectId, TeachingMode? mode,
        string? area, int? preferredTeacherId = null, Gender? gender = null, int take = 12)
    {
        var teachers = await _db.Teachers
            .Include(t => t.Subjects).ThenInclude(s => s.Subject)
            .Include(t => t.Grades).ThenInclude(g => g.Grade).ThenInclude(g => g.Stage)
            .Include(t => t.Availabilities)
            .Where(t => t.IsActive && t.IsPublic)
            .ToListAsync();

        var areaKey = NormalizeArea(area);
        var matches = new List<TeacherMatch>();

        foreach (var teacher in teachers)
        {
            if (gender.HasValue && teacher.Gender != gender.Value) continue;
            if (mode.HasValue && !SupportsMode(teacher.TeachingMode, mode.Value)) continue;

            var subjectMatch = subjectId.HasValue && teacher.Subjects.Any(s => s.SubjectId == subjectId.Value);
            if (subjectId.HasValue && !subjectMatch) continue;

            var gradeMatch = gradeId.HasValue && teacher.Grades.Any(g => g.GradeId == gradeId.Value);
            if (gradeId.HasValue && !gradeMatch) continue;
            if (!gradeId.HasValue && subjectId.HasValue)
                gradeMatch = teacher.Grades.Count > 0;

            var areaMatch = !string.IsNullOrWhiteSpace(areaKey) && TeacherCoversArea(teacher.Areas, areaKey);

            var score = 0;
            if (subjectMatch) score += 40;
            if (gradeMatch) score += 20;
            if (preferredTeacherId == teacher.Id) score += 100;
            if (areaMatch) score += 15;
            if (mode is TeachingMode.Home or TeachingMode.Both && string.IsNullOrWhiteSpace(areaKey)) score -= 10;
            score += (int)Math.Round(teacher.RatingAvg * 2);
            score += Math.Min(10, teacher.ExperienceYears * 2);
            if (teacher.Availabilities.Count > 0) score += 5;
            if (mode.HasValue && teacher.TeachingMode == TeachingMode.Both) score += 3;

            matches.Add(new TeacherMatch(teacher, score, subjectMatch, gradeMatch, areaMatch));
        }

        return matches.OrderByDescending(m => m.Score).ThenByDescending(m => m.Teacher.RatingAvg).Take(take).ToList();
    }

    private static bool SupportsMode(TeachingMode supported, TeachingMode requested) =>
        requested == TeachingMode.Institute
            ? supported is TeachingMode.Institute or TeachingMode.Both
            : supported is TeachingMode.Home or TeachingMode.Both;

    private static string NormalizeArea(string? area) =>
        (area ?? "").Trim().ToLowerInvariant()
            .Replace("حي ", "").Replace("منطقة ", "").Replace("محلة ", "");

    private static bool TeacherCoversArea(string? areas, string areaKey)
    {
        if (string.IsNullOrWhiteSpace(areas) || string.IsNullOrWhiteSpace(areaKey)) return false;
        return areas.Split(new[] { ',', '،', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeArea)
            .Any(a => a.Contains(areaKey, StringComparison.OrdinalIgnoreCase) || areaKey.Contains(a, StringComparison.OrdinalIgnoreCase));
    }
}
