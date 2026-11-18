namespace KirkukInstitute.Models;

/// <summary>
/// One row per calendar year that remembers the last student-number sequence handed out.
/// <see cref="Services.StudentNumberService"/> advances a row with a single atomic
/// <c>UPDATE … WITH (UPDLOCK, HOLDLOCK)</c>, so two simultaneous registrations can never
/// receive the same number. The row is never edited by application screens.
/// </summary>
public class StudentNumberCounter
{
    /// <summary>Calendar year the sequence belongs to (primary key).</summary>
    public int Year { get; set; }

    /// <summary>Last sequence number that was issued for this year.</summary>
    public int LastNumber { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
