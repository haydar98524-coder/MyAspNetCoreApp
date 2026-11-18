using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Data;

/// <summary>
/// Additive schema for the student registration module (national ID, hall, subject and teacher links).
/// The application provisions its database with EnsureCreated, which never alters an existing database,
/// so this idempotent script upgrades already-installed databases in place. Each statement runs in its
/// own batch: SQL Server compiles a whole batch before executing it, so an index or foreign key cannot
/// reference a column/table created earlier in the same batch.
/// </summary>
public static class StudentRegistrationSchema
{
    private static readonly string[] Batches =
    [
        """
        IF COL_LENGTH(N'dbo.Students', N'NationalId') IS NULL
            ALTER TABLE dbo.Students ADD NationalId nvarchar(30) NULL;
        """,
        """
        IF COL_LENGTH(N'dbo.Students', N'HallId') IS NULL
            ALTER TABLE dbo.Students ADD HallId int NULL;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE name = N'IX_Students_NationalId' AND object_id = OBJECT_ID(N'dbo.Students'))
            CREATE UNIQUE INDEX IX_Students_NationalId ON dbo.Students(NationalId)
            WHERE NationalId IS NOT NULL;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE name = N'IX_Students_HallId' AND object_id = OBJECT_ID(N'dbo.Students'))
            CREATE INDEX IX_Students_HallId ON dbo.Students(HallId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Students_Classrooms_HallId')
            ALTER TABLE dbo.Students ADD CONSTRAINT FK_Students_Classrooms_HallId
                FOREIGN KEY (HallId) REFERENCES dbo.Classrooms(Id) ON DELETE NO ACTION;
        """,
        """
        IF OBJECT_ID(N'dbo.StudentSubjects', N'U') IS NULL
            CREATE TABLE dbo.StudentSubjects
            (
                Id        int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudentSubjects PRIMARY KEY,
                StudentId int NOT NULL,
                SubjectId int NOT NULL
            );
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE name = N'IX_StudentSubjects_StudentId_SubjectId' AND object_id = OBJECT_ID(N'dbo.StudentSubjects'))
            CREATE UNIQUE INDEX IX_StudentSubjects_StudentId_SubjectId
                ON dbo.StudentSubjects(StudentId, SubjectId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE name = N'IX_StudentSubjects_SubjectId' AND object_id = OBJECT_ID(N'dbo.StudentSubjects'))
            CREATE INDEX IX_StudentSubjects_SubjectId ON dbo.StudentSubjects(SubjectId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_StudentSubjects_Students_StudentId')
            ALTER TABLE dbo.StudentSubjects ADD CONSTRAINT FK_StudentSubjects_Students_StudentId
                FOREIGN KEY (StudentId) REFERENCES dbo.Students(Id) ON DELETE CASCADE;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_StudentSubjects_Subjects_SubjectId')
            ALTER TABLE dbo.StudentSubjects ADD CONSTRAINT FK_StudentSubjects_Subjects_SubjectId
                FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects(Id) ON DELETE NO ACTION;
        """,
        """
        IF OBJECT_ID(N'dbo.StudentTeachers', N'U') IS NULL
            CREATE TABLE dbo.StudentTeachers
            (
                Id        int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudentTeachers PRIMARY KEY,
                StudentId int NOT NULL,
                TeacherId int NOT NULL
            );
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE name = N'IX_StudentTeachers_StudentId_TeacherId' AND object_id = OBJECT_ID(N'dbo.StudentTeachers'))
            CREATE UNIQUE INDEX IX_StudentTeachers_StudentId_TeacherId
                ON dbo.StudentTeachers(StudentId, TeacherId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE name = N'IX_StudentTeachers_TeacherId' AND object_id = OBJECT_ID(N'dbo.StudentTeachers'))
            CREATE INDEX IX_StudentTeachers_TeacherId ON dbo.StudentTeachers(TeacherId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_StudentTeachers_Students_StudentId')
            ALTER TABLE dbo.StudentTeachers ADD CONSTRAINT FK_StudentTeachers_Students_StudentId
                FOREIGN KEY (StudentId) REFERENCES dbo.Students(Id) ON DELETE CASCADE;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_StudentTeachers_Teachers_TeacherId')
            ALTER TABLE dbo.StudentTeachers ADD CONSTRAINT FK_StudentTeachers_Teachers_TeacherId
                FOREIGN KEY (TeacherId) REFERENCES dbo.Teachers(Id) ON DELETE NO ACTION;
        """,
        // ── Permanent sequential student numbers ────────────────────────────────
        // One row per year; StudentNumberService increments LastNumber atomically.
        """
        IF OBJECT_ID(N'dbo.StudentNumberCounters', N'U') IS NULL
            CREATE TABLE dbo.StudentNumberCounters
            (
                [Year]     int          NOT NULL CONSTRAINT PK_StudentNumberCounters PRIMARY KEY,
                LastNumber int          NOT NULL,
                UpdatedAt  datetime2(0) NOT NULL CONSTRAINT DF_StudentNumberCounters_UpdatedAt DEFAULT (SYSUTCDATETIME())
            );
        """,
        // Seed counters for years that already contain students, so an upgrade continues after
        // the highest existing number instead of re-issuing one. Runs once per missing year.
        """
        INSERT INTO dbo.StudentNumberCounters ([Year], LastNumber)
        SELECT x.[Year], MAX(x.Seq)
        FROM (
            SELECT TRY_CONVERT(int, PARSENAME(REPLACE(StudentNo, N'-', N'.'), 2)) AS [Year],
                   TRY_CONVERT(int, PARSENAME(REPLACE(StudentNo, N'-', N'.'), 1)) AS Seq
            FROM dbo.Students
            WHERE StudentNo LIKE N'KRI-[0-9][0-9][0-9][0-9]-%'
        ) x
        WHERE x.[Year] IS NOT NULL AND x.Seq IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM dbo.StudentNumberCounters c WHERE c.[Year] = x.[Year])
        GROUP BY x.[Year];
        """,
        // The number is permanent: even a direct UPDATE that changes it is rejected and rolled back.
        // Dropped first so installs that already ran an older version receive the corrected body.
        """
        IF OBJECT_ID(N'dbo.TR_Students_StudentNo_Immutable', N'TR') IS NOT NULL
            EXEC(N'DROP TRIGGER dbo.TR_Students_StudentNo_Immutable;');
        """,
        """
        EXEC(N'
        CREATE TRIGGER dbo.TR_Students_StudentNo_Immutable
        ON dbo.Students
        AFTER UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (
                SELECT 1
                FROM inserted i
                INNER JOIN deleted d ON d.Id = i.Id
                WHERE i.StudentNo COLLATE Latin1_General_BIN2
                    <> d.StudentNo COLLATE Latin1_General_BIN2
            )
            BEGIN
                ROLLBACK TRANSACTION;
                RAISERROR(N''The student number is permanent and cannot be changed.'', 16, 1);
            END
        END');
        """
    ];

    public static async Task EnsureAsync(AppDbContext db)
    {
        foreach (var batch in Batches)
            await db.Database.ExecuteSqlRawAsync(batch);
    }
}
