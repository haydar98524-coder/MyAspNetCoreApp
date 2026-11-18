using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using KirkukInstitute.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KirkukInstitute.Ai;

/// <summary>
/// The only data access the AI assistant has. Layered read-only enforcement:
/// <list type="number">
/// <item>No change tracking and every SaveChanges overload throws (application layer).</item>
/// <item><see cref="ReadOnlyCommandGuard"/> rejects any SQL command that is not a single SELECT (EF layer).</item>
/// <item><see cref="ReaderImpersonationInterceptor"/> switches every connection to a SELECT-only database
///       user before any command runs; if that fails the connection is closed (database layer, fail-closed).</item>
/// </list>
/// </summary>
public sealed class AiReadOnlyDbContext : AppDbContext
{
    public AiReadOnlyDbContext(DbContextOptions<AiReadOnlyDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.AutoDetectChangesEnabled = false;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new ReadOnlyViolationException("SaveChanges is not permitted in the AI read-only context.");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        throw new ReadOnlyViolationException("SaveChangesAsync is not permitted in the AI read-only context.");
}

public sealed class ReadOnlyViolationException(string message) : InvalidOperationException(message);

/// <summary>
/// Runs <c>EXECUTE AS USER</c> on every physical connection the AI context opens and verifies the switch
/// with <c>USER_NAME()</c>. A connection that cannot be switched is closed and the query fails.
/// </summary>
public sealed partial class ReaderImpersonationInterceptor : DbConnectionInterceptor
{
    private readonly string _principal;

    public ReaderImpersonationInterceptor(string principal)
    {
        if (!SafeIdentifier().IsMatch(principal))
            throw new ArgumentException("Reader principal must be a plain identifier (letters, digits, underscore).", nameof(principal));
        _principal = principal;
    }

    public string Principal => _principal;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        Impersonate(connection);

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default) =>
        await ImpersonateAsync(connection, cancellationToken);

    public override InterceptionResult ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        Revert(connection);
        return result;
    }

    public override async ValueTask<InterceptionResult> ConnectionClosingAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result)
    {
        await RevertAsync(connection);
        return result;
    }

    private string ImpersonateSql => $"EXECUTE AS USER = N'{_principal}'; SELECT USER_NAME();";

    private void Impersonate(DbConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = ImpersonateSql;
            Verify(cmd.ExecuteScalar() as string);
        }
        catch
        {
            connection.Close();
            throw;
        }
    }

    private async Task ImpersonateAsync(DbConnection connection, CancellationToken ct)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ImpersonateSql;
            Verify(await cmd.ExecuteScalarAsync(ct) as string);
        }
        catch
        {
            await connection.CloseAsync();
            throw;
        }
    }

    private void Verify(string? actualUser)
    {
        if (!string.Equals(actualUser, _principal, StringComparison.OrdinalIgnoreCase))
            throw new ReadOnlyViolationException(
                $"AI connection is not running as the read-only principal (expected '{_principal}', got '{actualUser ?? "null"}').");
    }

    private static void Revert(DbConnection connection)
    {
        if (connection.State != ConnectionState.Open) return;
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "REVERT;";
            cmd.ExecuteNonQuery();
        }
        catch (DbException) { /* pooling is disabled for this connection string; the session ends on close */ }
    }

    private static async Task RevertAsync(DbConnection connection)
    {
        if (connection.State != ConnectionState.Open) return;
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "REVERT;";
            await cmd.ExecuteNonQueryAsync();
        }
        catch (DbException) { }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,63}$")]
    private static partial Regex SafeIdentifier();
}

/// <summary>
/// Rejects every EF command that is not a pure SELECT. Non-query commands (INSERT/UPDATE/DELETE,
/// ExecuteSql, ExecuteUpdate, ExecuteDelete) are refused outright.
/// </summary>
public sealed partial class ReadOnlyCommandGuard : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        EnsureSelect(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        EnsureSelect(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result)
    {
        EnsureSelect(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        EnsureSelect(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) =>
        throw new ReadOnlyViolationException("Non-query SQL commands are not permitted in the AI read-only context.");

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
        throw new ReadOnlyViolationException("Non-query SQL commands are not permitted in the AI read-only context.");

    internal static void EnsureSelect(DbCommand command)
    {
        if (command.CommandType != CommandType.Text)
            throw new ReadOnlyViolationException("Only text SELECT commands are permitted in the AI read-only context.");

        // Remove comments, string literals and [bracketed] identifiers, then inspect the remaining keywords.
        var sql = command.CommandText;
        sql = BlockComment().Replace(sql, " ");
        sql = LineComment().Replace(sql, " ");
        sql = StringLiteral().Replace(sql, "''");
        sql = BracketIdentifier().Replace(sql, "[x]");
        var trimmed = sql.TrimStart();

        if (!(trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
              trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)))
            throw new ReadOnlyViolationException("Only SELECT statements are permitted in the AI read-only context.");

        if (ForbiddenKeyword().IsMatch(sql))
            throw new ReadOnlyViolationException("The SQL command contains a forbidden data-modification keyword.");
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)] private static partial Regex BlockComment();
    [GeneratedRegex(@"--[^\r\n]*")] private static partial Regex LineComment();
    [GeneratedRegex(@"N?'(?:[^']|'')*'")] private static partial Regex StringLiteral();
    [GeneratedRegex(@"\[[^\]]*\]")] private static partial Regex BracketIdentifier();
    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|MERGE|TRUNCATE|DROP|ALTER|CREATE|GRANT|REVOKE|DENY|EXEC|EXECUTE|BACKUP|RESTORE|INTO|OPENROWSET|OPENQUERY|BULK|DBCC|SHUTDOWN|KILL|RECONFIGURE|WAITFOR)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ForbiddenKeyword();
}

/// <summary>Creates / repairs the SELECT-only database principal used by the AI context.</summary>
public static class AiReaderProvisioner
{
    public const string PrincipalToken = "{{PRINCIPAL}}";

    public static string LoadScript(string principal)
    {
        var asm = typeof(AiReaderProvisioner).Assembly;
        var name = asm.GetManifestResourceNames().Single(n => n.EndsWith("CreateAiReader.sql", StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace(PrincipalToken, principal, StringComparison.Ordinal);
    }

    /// <summary>Runs with the application's own (owner) connection, never through the AI context.</summary>
    public static async Task ProvisionAsync(AppDbContext ownerDb, string principal, ILogger logger)
    {
        _ = new ReaderImpersonationInterceptor(principal); // validates the identifier
        try
        {
            await ownerDb.Database.ExecuteSqlRawAsync(LoadScript(principal));
            logger.LogInformation("AI read-only principal '{Principal}' is provisioned (SELECT only, writes denied).", principal);
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex,
                "Could not provision the AI read-only principal '{Principal}'. Run src/KirkukInstitute/Ai/Sql/CreateAiReader.sql as db_owner. " +
                "Until then every AI data query fails closed.", principal);
        }
    }

    /// <summary>Builds the AI connection string: same database, separate app name, pooling off.</summary>
    public static string BuildConnectionString(string? configured, string fallback)
    {
        var csb = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(configured) ? fallback : configured)
        {
            ApplicationName = "KirkukInstitute.AI",
            // Impersonated sessions must never be returned to a shared pool.
            Pooling = false,
            MultipleActiveResultSets = false,
            ApplicationIntent = ApplicationIntent.ReadOnly
        };
        return csb.ConnectionString;
    }
}
