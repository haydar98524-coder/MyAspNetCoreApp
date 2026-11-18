using System.Data;
using System.Data.Common;
using System.Globalization;
using KirkukInstitute.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KirkukInstitute.Services;

/// <summary>
/// Issues gap-free sequential documents such as <c>RC-2026-0042</c>, <c>JV-2026-0117</c>,
/// <c>TC-2026-0009</c>, <c>CS-2026-0003</c> and <c>TS-2026-0002</c>.
///
/// The previous implementation counted existing rows and retried on collision, which could
/// issue duplicate or gap-riddled numbers under concurrent cashiers. This service keeps one
/// counter per prefix and year and advances it in a single T-SQL statement under a
/// <c>UPDLOCK, HOLDLOCK</c> range lock. A missing counter self-seeds from existing documents,
/// so upgrading an installed database continues after the highest number already issued.
/// </summary>
public sealed class DocumentNumberService
{
    public const string Receipt = "RC";
    public const string Journal = "JV";
    public const string TuitionContract = "TC";
    public const string TeacherContract = "TCH";
    public const string CashierShift = "CS";
    public const string TeacherSettlement = "TS";

    private const string AllocateSql = """
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        DECLARE @next int;
        DECLARE @seed int = 0;

        UPDATE dbo.DocumentNumberCounters WITH (UPDLOCK, HOLDLOCK)
        SET LastNumber = LastNumber + 1,
            UpdatedAt  = SYSUTCDATETIME()
        WHERE Prefix = @prefix AND [Year] = @year;

        IF @@ROWCOUNT = 0
        BEGIN
            IF @prefix = N'RC'
                SELECT @seed = ISNULL(MAX(TRY_CONVERT(int, PARSENAME(REPLACE(ReceiptNo, N'-', N'.'), 1))), 0)
                FROM dbo.Payments
                WHERE ReceiptNo LIKE N'RC-' + CAST(@year AS nvarchar(4)) + N'-%';
            ELSE IF @prefix = N'JV'
                SELECT @seed = ISNULL(MAX(TRY_CONVERT(int, PARSENAME(REPLACE(EntryNo, N'-', N'.'), 1))), 0)
                FROM dbo.JournalEntries
                WHERE EntryNo LIKE N'JV-' + CAST(@year AS nvarchar(4)) + N'-%';
            ELSE IF @prefix = N'TC'
                SELECT @seed = ISNULL(MAX(TRY_CONVERT(int, PARSENAME(REPLACE(ContractNo, N'-', N'.'), 1))), 0)
                FROM dbo.TuitionContracts
                WHERE ContractNo LIKE N'TC-' + CAST(@year AS nvarchar(4)) + N'-%';
            ELSE IF @prefix = N'TCH'
                SELECT @seed = ISNULL(MAX(TRY_CONVERT(int, PARSENAME(REPLACE(ContractNo, N'-', N'.'), 1))), 0)
                FROM dbo.TeacherContracts
                WHERE ContractNo LIKE N'TCH-' + CAST(@year AS nvarchar(4)) + N'-%';
            ELSE IF @prefix = N'CS'
                SELECT @seed = ISNULL(MAX(TRY_CONVERT(int, PARSENAME(REPLACE(ShiftNo, N'-', N'.'), 1))), 0)
                FROM dbo.CashierShifts
                WHERE ShiftNo LIKE N'CS-' + CAST(@year AS nvarchar(4)) + N'-%';
            ELSE IF @prefix = N'TS'
                SELECT @seed = ISNULL(MAX(TRY_CONVERT(int, PARSENAME(REPLACE(SettlementNo, N'-', N'.'), 1))), 0)
                FROM dbo.TeacherSettlements
                WHERE SettlementNo LIKE N'TS-' + CAST(@year AS nvarchar(4)) + N'-%';

            INSERT INTO dbo.DocumentNumberCounters (Prefix, [Year], LastNumber, UpdatedAt)
            VALUES (@prefix, @year, @seed + 1, SYSUTCDATETIME());

            SET @next = @seed + 1;
        END
        ELSE
            SELECT @next = LastNumber FROM dbo.DocumentNumberCounters
            WHERE Prefix = @prefix AND [Year] = @year;

        SELECT @next AS Value;
        """;

    private readonly AppDbContext _db;

    public DocumentNumberService(AppDbContext db) => _db = db;

    /// <summary>Reserves the next document number for the prefix and year.</summary>
    public async Task<string> AllocateAsync(string prefix, int? year = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > 8)
            throw new ArgumentException("A short document prefix is required.", nameof(prefix));
        var normalized = prefix.Trim().ToUpperInvariant();
        var targetYear = year ?? DateTime.Today.Year;

        var ambient = _db.Database.CurrentTransaction;
        var ownsTransaction = ambient is null;
        var tx = ambient ?? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            await using var cmd = _db.Database.GetDbConnection().CreateCommand();
            cmd.Transaction = tx.GetDbTransaction();
            cmd.CommandText = AllocateSql;
            cmd.CommandTimeout = 15;

            var prefixParam = cmd.CreateParameter();
            prefixParam.ParameterName = "@prefix";
            prefixParam.DbType = DbType.String;
            prefixParam.Size = 10;
            prefixParam.Value = normalized;
            cmd.Parameters.Add(prefixParam);

            var yearParam = cmd.CreateParameter();
            yearParam.ParameterName = "@year";
            yearParam.DbType = DbType.Int32;
            yearParam.Value = targetYear;
            cmd.Parameters.Add(yearParam);

            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is null or DBNull)
                throw new InvalidOperationException("The document number counter did not return a value.");

            var sequence = Convert.ToInt32(result, CultureInfo.InvariantCulture);
            if (ownsTransaction) await tx.CommitAsync(ct);

            return $"{normalized}-{targetYear:D4}-{sequence:D4}";
        }
        catch
        {
            if (ownsTransaction) await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            if (ownsTransaction) await tx.DisposeAsync();
        }
    }
}
