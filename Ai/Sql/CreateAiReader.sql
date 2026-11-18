/*
    Kirkuk National Institute - AI read-only database principal
    -----------------------------------------------------------
    Creates (or repairs) a database user WITHOUT LOGIN that the AI assistant impersonates
    (EXECUTE AS USER) on every connection. The user can only SELECT business data:

      * member of db_datareader (SELECT on every table);
      * INSERT / UPDATE / DELETE / EXECUTE / ALTER / REFERENCES explicitly DENIED on schema dbo;
      * DDL, user/role management and backup explicitly DENIED at database level;
      * SELECT DENIED on identity / security tables (password hashes, tokens, claims) and the audit trail.

    DENY always wins over GRANT in SQL Server, so even a future accidental GRANT cannot re-enable writes.
    Idempotent: safe to run any number of times. The application runs it automatically at start-up
    (Ai:ProvisionReaderOnStartup); a DBA can also run it manually:

      sqlcmd -S localhost -E -C -d KirkukInstitute -i CreateAiReader.sql   (after replacing the token)

    {{PRINCIPAL}} is replaced with the configured principal name (default kirkuk_ai_reader).
*/
SET NOCOUNT ON;

IF DATABASE_PRINCIPAL_ID(N'{{PRINCIPAL}}') IS NULL
    CREATE USER [{{PRINCIPAL}}] WITHOUT LOGIN WITH DEFAULT_SCHEMA = [dbo];

IF IS_ROLEMEMBER(N'db_datareader', N'{{PRINCIPAL}}') = 0
    ALTER ROLE [db_datareader] ADD MEMBER [{{PRINCIPAL}}];

-- Never a member of any role that can write.
IF IS_ROLEMEMBER(N'db_datawriter', N'{{PRINCIPAL}}') = 1 ALTER ROLE [db_datawriter] DROP MEMBER [{{PRINCIPAL}}];
IF IS_ROLEMEMBER(N'db_ddladmin',   N'{{PRINCIPAL}}') = 1 ALTER ROLE [db_ddladmin]   DROP MEMBER [{{PRINCIPAL}}];
IF IS_ROLEMEMBER(N'db_owner',      N'{{PRINCIPAL}}') = 1 ALTER ROLE [db_owner]      DROP MEMBER [{{PRINCIPAL}}];

-- Object-level write denial on the whole schema (covers current and future tables).
DENY INSERT, UPDATE, DELETE, EXECUTE, ALTER, REFERENCES ON SCHEMA::[dbo] TO [{{PRINCIPAL}}];

-- Database-level denial of DDL, security management and backup.
DENY CREATE TABLE, CREATE VIEW, CREATE PROCEDURE, CREATE FUNCTION, CREATE SCHEMA, CREATE TYPE,
     ALTER ANY SCHEMA, ALTER ANY USER, ALTER ANY ROLE, ALTER ANY APPLICATION ROLE,
     BACKUP DATABASE, BACKUP LOG, CHECKPOINT
     TO [{{PRINCIPAL}}];

-- Identity, security and audit data are invisible to the assistant.
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'DENY SELECT ON [dbo].' + QUOTENAME(t.name) + N' TO [{{PRINCIPAL}}];'
FROM sys.tables AS t
WHERE t.schema_id = SCHEMA_ID(N'dbo')
  AND (t.name LIKE N'AspNet%' OR t.name IN (N'AuditLogs', N'__EFMigrationsHistory'));
EXEC sys.sp_executesql @sql;
