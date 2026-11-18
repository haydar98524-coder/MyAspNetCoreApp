using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Data;

/// <summary>
/// Phase 1 (financial governance) additive schema. EnsureCreated provisions a new database from
/// the EF model but never alters an installed one, so every table, column, index and constraint
/// below is created only when missing. Batches stay one statement each because SQL Server
/// compiles a batch before running it: an index cannot reference a column created in the same
/// batch. <see cref="BackfillAsync"/> runs after seeding and converts legacy enrollments/dues
/// into tuition contracts without touching their financial history.
/// </summary>
public static class Phase1FinanceSchema
{
    private static readonly string[] StructureBatches =
    [
        """
        IF OBJECT_ID(N'dbo.DocumentNumberCounters', N'U') IS NULL
            CREATE TABLE dbo.DocumentNumberCounters
            (
                Prefix     nvarchar(10) NOT NULL,
                [Year]     int          NOT NULL,
                LastNumber int          NOT NULL,
                UpdatedAt  datetime2(0) NOT NULL CONSTRAINT DF_DocumentNumberCounters_UpdatedAt DEFAULT (SYSUTCDATETIME()),
                CONSTRAINT PK_DocumentNumberCounters PRIMARY KEY (Prefix, [Year])
            );
        """,
        """
        IF OBJECT_ID(N'dbo.TuitionContracts', N'U') IS NULL
            CREATE TABLE dbo.TuitionContracts
            (
                Id               int           IDENTITY(1,1) NOT NULL CONSTRAINT PK_TuitionContracts PRIMARY KEY,
                ContractNo       nvarchar(32)  NOT NULL,
                StudentId        int           NOT NULL,
                EnrollmentId     int           NOT NULL,
                CourseId         int           NOT NULL,
                ListFee          bigint        NOT NULL,
                Discount         bigint        NOT NULL,
                NetFee           bigint        NOT NULL,
                InstallmentCount int           NOT NULL,
                StartDate        datetime2(0)  NOT NULL,
                FirstDueDate     datetime2(0)  NOT NULL,
                Status           int           NOT NULL CONSTRAINT DF_TuitionContracts_Status DEFAULT (1),
                Notes            nvarchar(600) NULL,
                CreatedByUserId  nvarchar(450) NOT NULL CONSTRAINT DF_TuitionContracts_CreatedByUserId DEFAULT (N''),
                CreatedByName    nvarchar(150) NOT NULL CONSTRAINT DF_TuitionContracts_CreatedByName DEFAULT (N''),
                CreatedAt        datetime2(0)  NOT NULL CONSTRAINT DF_TuitionContracts_CreatedAt DEFAULT (SYSUTCDATETIME())
            );
        """,
        """
        IF OBJECT_ID(N'dbo.CashierShifts', N'U') IS NULL
            CREATE TABLE dbo.CashierShifts
            (
                Id             int           IDENTITY(1,1) NOT NULL CONSTRAINT PK_CashierShifts PRIMARY KEY,
                ShiftNo        nvarchar(32)  NOT NULL,
                CashierUserId  nvarchar(450) NOT NULL,
                CashierName    nvarchar(150) NOT NULL,
                OpenedAt       datetime2(0)  NOT NULL,
                OpeningFloat   bigint        NOT NULL,
                ClosedAt       datetime2(0)  NULL,
                ExpectedCash   bigint        NOT NULL CONSTRAINT DF_CashierShifts_ExpectedCash DEFAULT (0),
                CountedCash    bigint        NOT NULL CONSTRAINT DF_CashierShifts_CountedCash DEFAULT (0),
                Variance       bigint        NOT NULL CONSTRAINT DF_CashierShifts_Variance DEFAULT (0),
                Status         int           NOT NULL CONSTRAINT DF_CashierShifts_Status DEFAULT (0),
                Notes          nvarchar(600) NULL,
                ApprovedByName nvarchar(150) NULL,
                ApprovedAt     datetime2(0)  NULL
            );
        """,
        """
        IF OBJECT_ID(N'dbo.CashierDenominations', N'U') IS NULL
            CREATE TABLE dbo.CashierDenominations
            (
                Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CashierDenominations PRIMARY KEY,
                CashierShiftId  int NOT NULL,
                Denomination    int NOT NULL,
                [Count]         int NOT NULL
            );
        """,
        """
        IF OBJECT_ID(N'dbo.TeacherContracts', N'U') IS NULL
            CREATE TABLE dbo.TeacherContracts
            (
                Id                  int           IDENTITY(1,1) NOT NULL CONSTRAINT PK_TeacherContracts PRIMARY KEY,
                ContractNo          nvarchar(32)  NOT NULL,
                TeacherId           int           NOT NULL,
                CompensationType    int           NOT NULL CONSTRAINT DF_TeacherContracts_CompensationType DEFAULT (0),
                SharePercent        decimal(18,2) NOT NULL CONSTRAINT DF_TeacherContracts_SharePercent DEFAULT (55),
                FixedMonthlyAmount  bigint        NOT NULL CONSTRAINT DF_TeacherContracts_FixedMonthlyAmount DEFAULT (0),
                StartDate           datetime2(0)  NOT NULL,
                EndDate             datetime2(0)  NULL,
                IsActive            bit           NOT NULL CONSTRAINT DF_TeacherContracts_IsActive DEFAULT (1),
                Notes               nvarchar(600) NULL,
                CreatedByUserId     nvarchar(450) NOT NULL CONSTRAINT DF_TeacherContracts_CreatedByUserId DEFAULT (N''),
                CreatedByName       nvarchar(150) NOT NULL CONSTRAINT DF_TeacherContracts_CreatedByName DEFAULT (N''),
                CreatedAt           datetime2(0)  NOT NULL CONSTRAINT DF_TeacherContracts_CreatedAt DEFAULT (SYSUTCDATETIME())
            );
        """,
        """
        IF OBJECT_ID(N'dbo.TeacherSettlements', N'U') IS NULL
            CREATE TABLE dbo.TeacherSettlements
            (
                Id                int           IDENTITY(1,1) NOT NULL CONSTRAINT PK_TeacherSettlements PRIMARY KEY,
                SettlementNo      nvarchar(32)  NOT NULL,
                TeacherId         int           NOT NULL,
                ContractId        int           NULL,
                PeriodStart       datetime2(0)  NOT NULL,
                PeriodEnd         datetime2(0)  NOT NULL,
                Gross             bigint        NOT NULL CONSTRAINT DF_TeacherSettlements_Gross DEFAULT (0),
                Adjustments       bigint        NOT NULL CONSTRAINT DF_TeacherSettlements_Adjustments DEFAULT (0),
                Deductions        bigint        NOT NULL CONSTRAINT DF_TeacherSettlements_Deductions DEFAULT (0),
                Net               bigint        NOT NULL CONSTRAINT DF_TeacherSettlements_Net DEFAULT (0),
                Status            int           NOT NULL CONSTRAINT DF_TeacherSettlements_Status DEFAULT (0),
                Notes             nvarchar(600) NULL,
                GeneratedByUserId nvarchar(450) NOT NULL CONSTRAINT DF_TeacherSettlements_GeneratedByUserId DEFAULT (N''),
                GeneratedByName   nvarchar(150) NOT NULL CONSTRAINT DF_TeacherSettlements_GeneratedByName DEFAULT (N''),
                GeneratedAt       datetime2(0)  NOT NULL CONSTRAINT DF_TeacherSettlements_GeneratedAt DEFAULT (SYSUTCDATETIME()),
                ApprovedByName    nvarchar(150) NULL,
                ApprovedAt        datetime2(0)  NULL,
                PaidByName        nvarchar(150) NULL,
                PaidAt            datetime2(0)  NULL,
                FundId            int           NULL,
                Method            int           NOT NULL CONSTRAINT DF_TeacherSettlements_Method DEFAULT (0)
            );
        """,
        """
        IF OBJECT_ID(N'dbo.TeacherSettlementLines', N'U') IS NULL
            CREATE TABLE dbo.TeacherSettlementLines
            (
                Id                  int           IDENTITY(1,1) NOT NULL CONSTRAINT PK_TeacherSettlementLines PRIMARY KEY,
                TeacherSettlementId int           NOT NULL,
                TeacherEarningId    int           NULL,
                Kind                int           NOT NULL CONSTRAINT DF_TeacherSettlementLines_Kind DEFAULT (0),
                Description         nvarchar(300) NOT NULL CONSTRAINT DF_TeacherSettlementLines_Description DEFAULT (N''),
                Amount              bigint        NOT NULL CONSTRAINT DF_TeacherSettlementLines_Amount DEFAULT (0)
            );
        """,
        """
        IF COL_LENGTH(N'dbo.Dues', N'ContractId') IS NULL
            ALTER TABLE dbo.Dues ADD ContractId int NULL;
        """,
        """
        IF COL_LENGTH(N'dbo.Dues', N'InstallmentNo') IS NULL
            ALTER TABLE dbo.Dues ADD InstallmentNo int NULL;
        """,
        """
        IF COL_LENGTH(N'dbo.Payments', N'CashierShiftId') IS NULL
            ALTER TABLE dbo.Payments ADD CashierShiftId int NULL;
        """,
        """
        IF COL_LENGTH(N'dbo.TeacherEarnings', N'TeacherContractId') IS NULL
            ALTER TABLE dbo.TeacherEarnings ADD TeacherContractId int NULL;
        """,
        // ── Indexes ──────────────────────────────────────────────────────────
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TuitionContracts_ContractNo' AND object_id = OBJECT_ID(N'dbo.TuitionContracts'))
            CREATE UNIQUE INDEX IX_TuitionContracts_ContractNo ON dbo.TuitionContracts(ContractNo);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TuitionContracts_EnrollmentId' AND object_id = OBJECT_ID(N'dbo.TuitionContracts'))
            CREATE UNIQUE INDEX IX_TuitionContracts_EnrollmentId ON dbo.TuitionContracts(EnrollmentId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TuitionContracts_StudentId_Status' AND object_id = OBJECT_ID(N'dbo.TuitionContracts'))
            CREATE INDEX IX_TuitionContracts_StudentId_Status ON dbo.TuitionContracts(StudentId, Status);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Dues_ContractId_InstallmentNo' AND object_id = OBJECT_ID(N'dbo.Dues'))
            CREATE INDEX IX_Dues_ContractId_InstallmentNo ON dbo.Dues(ContractId, InstallmentNo);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_CashierShiftId' AND object_id = OBJECT_ID(N'dbo.Payments'))
            CREATE INDEX IX_Payments_CashierShiftId ON dbo.Payments(CashierShiftId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TeacherEarnings_TeacherContractId' AND object_id = OBJECT_ID(N'dbo.TeacherEarnings'))
            CREATE INDEX IX_TeacherEarnings_TeacherContractId ON dbo.TeacherEarnings(TeacherContractId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashierShifts_ShiftNo' AND object_id = OBJECT_ID(N'dbo.CashierShifts'))
            CREATE UNIQUE INDEX IX_CashierShifts_ShiftNo ON dbo.CashierShifts(ShiftNo);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashierShifts_CashierUserId' AND object_id = OBJECT_ID(N'dbo.CashierShifts'))
            CREATE UNIQUE INDEX IX_CashierShifts_CashierUserId ON dbo.CashierShifts(CashierUserId)
            WHERE [Status] IN (0, 1);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashierShifts_OpenedAt' AND object_id = OBJECT_ID(N'dbo.CashierShifts'))
            CREATE INDEX IX_CashierShifts_OpenedAt ON dbo.CashierShifts(OpenedAt);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashierDenominations_CashierShiftId' AND object_id = OBJECT_ID(N'dbo.CashierDenominations'))
            CREATE INDEX IX_CashierDenominations_CashierShiftId ON dbo.CashierDenominations(CashierShiftId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TeacherContracts_ContractNo' AND object_id = OBJECT_ID(N'dbo.TeacherContracts'))
            CREATE UNIQUE INDEX IX_TeacherContracts_ContractNo ON dbo.TeacherContracts(ContractNo);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TeacherContracts_TeacherId_IsActive' AND object_id = OBJECT_ID(N'dbo.TeacherContracts'))
            CREATE INDEX IX_TeacherContracts_TeacherId_IsActive ON dbo.TeacherContracts(TeacherId, IsActive);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TeacherSettlements_SettlementNo' AND object_id = OBJECT_ID(N'dbo.TeacherSettlements'))
            CREATE UNIQUE INDEX IX_TeacherSettlements_SettlementNo ON dbo.TeacherSettlements(SettlementNo);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TeacherSettlements_TeacherId_PeriodStart_PeriodEnd' AND object_id = OBJECT_ID(N'dbo.TeacherSettlements'))
            CREATE UNIQUE INDEX IX_TeacherSettlements_TeacherId_PeriodStart_PeriodEnd
                ON dbo.TeacherSettlements(TeacherId, PeriodStart, PeriodEnd);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TeacherSettlements_TeacherId_Status' AND object_id = OBJECT_ID(N'dbo.TeacherSettlements'))
            CREATE INDEX IX_TeacherSettlements_TeacherId_Status ON dbo.TeacherSettlements(TeacherId, Status);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TeacherSettlementLines_TeacherSettlementId' AND object_id = OBJECT_ID(N'dbo.TeacherSettlementLines'))
            CREATE INDEX IX_TeacherSettlementLines_TeacherSettlementId ON dbo.TeacherSettlementLines(TeacherSettlementId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TeacherSettlementLines_TeacherEarningId' AND object_id = OBJECT_ID(N'dbo.TeacherSettlementLines'))
            CREATE UNIQUE INDEX IX_TeacherSettlementLines_TeacherEarningId ON dbo.TeacherSettlementLines(TeacherEarningId)
            WHERE TeacherEarningId IS NOT NULL;
        """,
        // ── Foreign keys ─────────────────────────────────────────────────────
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TuitionContracts_Students_StudentId')
            ALTER TABLE dbo.TuitionContracts ADD CONSTRAINT FK_TuitionContracts_Students_StudentId
                FOREIGN KEY (StudentId) REFERENCES dbo.Students(Id) ON DELETE NO ACTION;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TuitionContracts_Enrollments_EnrollmentId')
            ALTER TABLE dbo.TuitionContracts ADD CONSTRAINT FK_TuitionContracts_Enrollments_EnrollmentId
                FOREIGN KEY (EnrollmentId) REFERENCES dbo.Enrollments(Id) ON DELETE NO ACTION;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TuitionContracts_Courses_CourseId')
            ALTER TABLE dbo.TuitionContracts ADD CONSTRAINT FK_TuitionContracts_Courses_CourseId
                FOREIGN KEY (CourseId) REFERENCES dbo.Courses(Id) ON DELETE NO ACTION;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Dues_TuitionContracts_ContractId')
            ALTER TABLE dbo.Dues ADD CONSTRAINT FK_Dues_TuitionContracts_ContractId
                FOREIGN KEY (ContractId) REFERENCES dbo.TuitionContracts(Id) ON DELETE NO ACTION;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Payments_CashierShifts_CashierShiftId')
            ALTER TABLE dbo.Payments ADD CONSTRAINT FK_Payments_CashierShifts_CashierShiftId
                FOREIGN KEY (CashierShiftId) REFERENCES dbo.CashierShifts(Id) ON DELETE NO ACTION;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CashierDenominations_CashierShifts_CashierShiftId')
            ALTER TABLE dbo.CashierDenominations ADD CONSTRAINT FK_CashierDenominations_CashierShifts_CashierShiftId
                FOREIGN KEY (CashierShiftId) REFERENCES dbo.CashierShifts(Id) ON DELETE CASCADE;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TeacherContracts_Teachers_TeacherId')
            ALTER TABLE dbo.TeacherContracts ADD CONSTRAINT FK_TeacherContracts_Teachers_TeacherId
                FOREIGN KEY (TeacherId) REFERENCES dbo.Teachers(Id) ON DELETE NO ACTION;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TeacherSettlements_Teachers_TeacherId')
            ALTER TABLE dbo.TeacherSettlements ADD CONSTRAINT FK_TeacherSettlements_Teachers_TeacherId
                FOREIGN KEY (TeacherId) REFERENCES dbo.Teachers(Id) ON DELETE NO ACTION;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TeacherSettlements_TeacherContracts_ContractId')
            ALTER TABLE dbo.TeacherSettlements ADD CONSTRAINT FK_TeacherSettlements_TeacherContracts_ContractId
                FOREIGN KEY (ContractId) REFERENCES dbo.TeacherContracts(Id) ON DELETE NO ACTION;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TeacherSettlements_Funds_FundId')
            ALTER TABLE dbo.TeacherSettlements ADD CONSTRAINT FK_TeacherSettlements_Funds_FundId
                FOREIGN KEY (FundId) REFERENCES dbo.Funds(Id) ON DELETE NO ACTION;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TeacherSettlementLines_TeacherSettlements_TeacherSettlementId')
            ALTER TABLE dbo.TeacherSettlementLines ADD CONSTRAINT FK_TeacherSettlementLines_TeacherSettlements_TeacherSettlementId
                FOREIGN KEY (TeacherSettlementId) REFERENCES dbo.TeacherSettlements(Id) ON DELETE CASCADE;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TeacherSettlementLines_TeacherEarnings_TeacherEarningId')
            ALTER TABLE dbo.TeacherSettlementLines ADD CONSTRAINT FK_TeacherSettlementLines_TeacherEarnings_TeacherEarningId
                FOREIGN KEY (TeacherEarningId) REFERENCES dbo.TeacherEarnings(Id) ON DELETE NO ACTION;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TeacherEarnings_TeacherContracts_TeacherContractId')
            ALTER TABLE dbo.TeacherEarnings ADD CONSTRAINT FK_TeacherEarnings_TeacherContracts_TeacherContractId
                FOREIGN KEY (TeacherContractId) REFERENCES dbo.TeacherContracts(Id) ON DELETE NO ACTION;
        """
    ];

    private static readonly string[] BackfillBatches =
    [
        // One contract per legacy enrollment; the number is flagged with L (legacy) and is
        // therefore ignored by the numeric counter seed. New contracts use TC-YYYY-NNNN.
        """
        ;WITH src AS
        (
            SELECT e.Id, e.StudentId, e.CourseId, e.Fee, e.Discount, e.NetFee, e.EnrollmentDate, e.Status,
                   (SELECT COUNT(*) FROM dbo.Dues d WHERE d.EnrollmentId = e.Id) AS Cnt,
                   (SELECT MIN(d.DueDate) FROM dbo.Dues d WHERE d.EnrollmentId = e.Id) AS FirstDue
            FROM dbo.Enrollments e
            WHERE NOT EXISTS (SELECT 1 FROM dbo.TuitionContracts c WHERE c.EnrollmentId = e.Id)
        )
        INSERT INTO dbo.TuitionContracts
            (ContractNo, StudentId, EnrollmentId, CourseId, ListFee, Discount, NetFee, InstallmentCount,
             StartDate, FirstDueDate, Status, Notes, CreatedByUserId, CreatedByName, CreatedAt)
        SELECT N'TC-' + CAST(YEAR(s.EnrollmentDate) AS nvarchar(4)) + N'-L' + RIGHT(N'000000' + CAST(s.Id AS nvarchar(6)), 6),
               s.StudentId, s.Id, s.CourseId, s.Fee, s.Discount, s.NetFee, s.Cnt,
               s.EnrollmentDate, ISNULL(s.FirstDue, s.EnrollmentDate),
               CASE s.Status WHEN 0 THEN 1 WHEN 1 THEN 2 WHEN 2 THEN 3 ELSE 1 END,
               N'Legacy enrollment migrated to a tuition contract.',
               N'system', N'Legacy backfill', SYSUTCDATETIME()
        FROM src s;
        """,
        """
        ;WITH ranked AS
        (
            SELECT Id, EnrollmentId,
                   ROW_NUMBER() OVER (PARTITION BY EnrollmentId ORDER BY DueDate, Id) AS rn
            FROM dbo.Dues
            WHERE ContractId IS NULL
        )
        UPDATE d
        SET d.ContractId = c.Id, d.InstallmentNo = r.rn
        FROM dbo.Dues d
        INNER JOIN ranked r ON r.Id = d.Id
        INNER JOIN dbo.TuitionContracts c ON c.EnrollmentId = d.EnrollmentId;
        """,
        """
        INSERT INTO dbo.DocumentNumberCounters (Prefix, [Year], LastNumber, UpdatedAt)
        SELECT N'RC', x.[Year], MAX(x.Seq), SYSUTCDATETIME()
        FROM (
            SELECT TRY_CONVERT(int, PARSENAME(REPLACE(ReceiptNo, N'-', N'.'), 2)) AS [Year],
                   TRY_CONVERT(int, PARSENAME(REPLACE(ReceiptNo, N'-', N'.'), 1)) AS Seq
            FROM dbo.Payments WHERE ReceiptNo LIKE N'RC-[0-9][0-9][0-9][0-9]-%'
        ) x
        WHERE x.[Year] IS NOT NULL AND x.Seq IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM dbo.DocumentNumberCounters c WHERE c.Prefix = N'RC' AND c.[Year] = x.[Year])
        GROUP BY x.[Year];
        """,
        """
        INSERT INTO dbo.DocumentNumberCounters (Prefix, [Year], LastNumber, UpdatedAt)
        SELECT N'JV', x.[Year], MAX(x.Seq), SYSUTCDATETIME()
        FROM (
            SELECT TRY_CONVERT(int, PARSENAME(REPLACE(EntryNo, N'-', N'.'), 2)) AS [Year],
                   TRY_CONVERT(int, PARSENAME(REPLACE(EntryNo, N'-', N'.'), 1)) AS Seq
            FROM dbo.JournalEntries WHERE EntryNo LIKE N'JV-[0-9][0-9][0-9][0-9]-%'
        ) x
        WHERE x.[Year] IS NOT NULL AND x.Seq IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM dbo.DocumentNumberCounters c WHERE c.Prefix = N'JV' AND c.[Year] = x.[Year])
        GROUP BY x.[Year];
        """,
        """
        INSERT INTO dbo.DocumentNumberCounters (Prefix, [Year], LastNumber, UpdatedAt)
        SELECT N'TC', x.[Year], MAX(x.Seq), SYSUTCDATETIME()
        FROM (
            SELECT TRY_CONVERT(int, PARSENAME(REPLACE(ContractNo, N'-', N'.'), 2)) AS [Year],
                   TRY_CONVERT(int, PARSENAME(REPLACE(ContractNo, N'-', N'.'), 1)) AS Seq
            FROM dbo.TuitionContracts WHERE ContractNo LIKE N'TC-[0-9][0-9][0-9][0-9]-%'
        ) x
        WHERE x.[Year] IS NOT NULL AND x.Seq IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM dbo.DocumentNumberCounters c WHERE c.Prefix = N'TC' AND c.[Year] = x.[Year])
        GROUP BY x.[Year];
        """,
        """
        INSERT INTO dbo.DocumentNumberCounters (Prefix, [Year], LastNumber, UpdatedAt)
        SELECT N'TCH', x.[Year], MAX(x.Seq), SYSUTCDATETIME()
        FROM (
            SELECT TRY_CONVERT(int, PARSENAME(REPLACE(ContractNo, N'-', N'.'), 2)) AS [Year],
                   TRY_CONVERT(int, PARSENAME(REPLACE(ContractNo, N'-', N'.'), 1)) AS Seq
            FROM dbo.TeacherContracts WHERE ContractNo LIKE N'TCH-[0-9][0-9][0-9][0-9]-%'
        ) x
        WHERE x.[Year] IS NOT NULL AND x.Seq IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM dbo.DocumentNumberCounters c WHERE c.Prefix = N'TCH' AND c.[Year] = x.[Year])
        GROUP BY x.[Year];
        """,
        """
        INSERT INTO dbo.DocumentNumberCounters (Prefix, [Year], LastNumber, UpdatedAt)
        SELECT N'CS', x.[Year], MAX(x.Seq), SYSUTCDATETIME()
        FROM (
            SELECT TRY_CONVERT(int, PARSENAME(REPLACE(ShiftNo, N'-', N'.'), 2)) AS [Year],
                   TRY_CONVERT(int, PARSENAME(REPLACE(ShiftNo, N'-', N'.'), 1)) AS Seq
            FROM dbo.CashierShifts WHERE ShiftNo LIKE N'CS-[0-9][0-9][0-9][0-9]-%'
        ) x
        WHERE x.[Year] IS NOT NULL AND x.Seq IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM dbo.DocumentNumberCounters c WHERE c.Prefix = N'CS' AND c.[Year] = x.[Year])
        GROUP BY x.[Year];
        """,
        """
        INSERT INTO dbo.DocumentNumberCounters (Prefix, [Year], LastNumber, UpdatedAt)
        SELECT N'TS', x.[Year], MAX(x.Seq), SYSUTCDATETIME()
        FROM (
            SELECT TRY_CONVERT(int, PARSENAME(REPLACE(SettlementNo, N'-', N'.'), 2)) AS [Year],
                   TRY_CONVERT(int, PARSENAME(REPLACE(SettlementNo, N'-', N'.'), 1)) AS Seq
            FROM dbo.TeacherSettlements WHERE SettlementNo LIKE N'TS-[0-9][0-9][0-9][0-9]-%'
        ) x
        WHERE x.[Year] IS NOT NULL AND x.Seq IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM dbo.DocumentNumberCounters c WHERE c.Prefix = N'TS' AND c.[Year] = x.[Year])
        GROUP BY x.[Year];
        """
    ];

    public static async Task EnsureAsync(AppDbContext db)
    {
        foreach (var batch in StructureBatches)
            await db.Database.ExecuteSqlRawAsync(batch);
    }

    /// <summary>Upgrades existing enrollments and seeds counters; safe to call on every start.</summary>
    public static async Task BackfillAsync(AppDbContext db)
    {
        foreach (var batch in BackfillBatches)
            await db.Database.ExecuteSqlRawAsync(batch);
    }
}
