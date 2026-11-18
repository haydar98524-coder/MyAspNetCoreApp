using System.Data;
using System.Data.Common;
using System.Globalization;
using KirkukInstitute.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KirkukInstitute.Services;

/// <summary>
/// Issues the permanent electronic student number (format <c>KRI-YYYY-NNNN</c>).
///
/// The sequence is kept in <c>StudentNumberCounters</c> and advanced by a single atomic
/// T-SQL statement. Two registrations racing for the same year are serialised by the
/// <c>UPDLOCK, HOLDLOCK</c> range lock, so both can never read the same LastNumber.
/// The unique index on <c>Students.StudentNo</c> is the final database-level guarantee,
/// and an AFTER UPDATE trigger keeps the number immutable forever.
/// </summary>
public sealed class StudentNumberService
{
    private const string AllocateSql = """
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        DECLARE @next int;

        /* Atomic per-year increment: the range lock makes a concurrent caller wait for this
           transaction to commit instead of reading the same value. */
        UPDATE dbo.StudentNumberCounters WITH (UPDLOCK, HOLDLOCK)
        SET LastNumber = LastNumber + 1,
            UpdatedAt  = SYSUTCDATETIME()
        WHERE [Year] = @year;

        IF @@ROWCOUNT = 0
        BEGIN
            /* First number of the year (or an upgraded database): continue after the highest
               number already stored so no existing student number is ever re-issued. */
            DECLARE @seed int =
                ISNULL((
                    SELECT MAX(TRY_CONVERT(int, PARSENAME(REPLACE(StudentNo, N'-', N'.'), 1)))
                    FROM dbo.Students
                    WHERE StudentNo LIKE N'KRI-' + CAST(@year AS nvarchar(4)) + N'-%'
                ), 0);

            INSERT INTO dbo.StudentNumberCounters ([Year], LastNumber, UpdatedAt)
            VALUES (@year, @seed + 1, SYSUTCDATETIME());

            SET @next = @seed + 1;
        END
        ELSE
            SELECT @next = LastNumber FROM dbo.StudentNumberCounters WHERE [Year] = @year;

        SELECT @next AS Value;
        """;

    private readonly AppDbContext _db;

    public StudentNumberService(AppDbContext db) => _db = db;

    /// <summary>
    /// Reserves and returns the next permanent student number for <paramref name="year"/>
    /// (current year when omitted). Safe to call from concurrent requests.
    /// </summary>
    public async Task<string> AllocateAsync(int? year = null, CancellationToken ct = default)
    {
        var targetYear = year ?? DateTime.Today.Year;

        // Reuse an ambient transaction if the caller already opened one, otherwise own a
        // short transaction so the row/range lock is released before the student is saved.
        var ambient = _db.Database.CurrentTransaction;
        var ownsTransaction = ambient is null;
        var tx = ambient ?? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            await using var cmd = _db.Database.GetDbConnection().CreateCommand();
            cmd.Transaction = tx.GetDbTransaction();
            cmd.CommandText = AllocateSql;
            cmd.CommandTimeout = 15;

            var yearParam = cmd.CreateParameter();
            yearParam.ParameterName = "@year";
            yearParam.DbType = DbType.Int32;
            yearParam.Value = targetYear;
            cmd.Parameters.Add(yearParam);

            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is null or DBNull)
                throw new InvalidOperationException("The student number counter did not return a value.");

            var sequence = Convert.ToInt32(result, CultureInfo.InvariantCulture);
            if (ownsTransaction) await tx.CommitAsync(ct);

            return $"KRI-{targetYear:D4}-{sequence:D4}";
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
