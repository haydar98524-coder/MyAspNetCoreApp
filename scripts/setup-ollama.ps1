<#
.SYNOPSIS
    Sets up the local Qwen2.5 model (Ollama) for the Kirkuk Institute read-only AI assistant.

.DESCRIPTION
    1. Installs Ollama with winget if it is missing.
    2. Pins Ollama to the loopback interface (OLLAMA_HOST=127.0.0.1:11434), so the model is never reachable from the network.
    3. Pulls the model (qwen2.5:7b by default, or qwen2.5:14b) and runs a tool-calling smoke test.
    4. Optionally (-ProvisionDatabase) creates or repairs the SELECT-only database user kirkuk_ai_reader via sqlcmd.
       The web application also does this automatically at start-up.
    5. Optionally (-NewApiKey) generates a system-integration API key and prints the SHA-256 hash to put in
       appsettings.json -> Ai:SystemApiKeys. The plain key is shown once and never stored.
    6. Optionally (-UpdateAppSettings) writes the chosen model into appsettings.json.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\setup-ollama.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\setup-ollama.ps1 -Model qwen2.5:14b -UpdateAppSettings
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\setup-ollama.ps1 -NewApiKey -ApiKeyName "bi-dashboard" -ApiKeyRoles Manager
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\setup-ollama.ps1 -ProvisionDatabase -SqlServer localhost -Database KirkukInstitute
#>
[CmdletBinding()]
param(
    [ValidateSet("qwen2.5:7b", "qwen2.5:14b", "qwen2.5:7b-instruct", "qwen2.5:14b-instruct")]
    [string]$Model = "qwen2.5:7b",
    [string]$OllamaUrl = "http://127.0.0.1:11434",
    [switch]$SkipPull,
    [switch]$SkipSmokeTest,
    [switch]$UpdateAppSettings,
    [switch]$ProvisionDatabase,
    [string]$SqlServer = "localhost",
    [string]$Database = "KirkukInstitute",
    [string]$ReaderPrincipal = "kirkuk_ai_reader",
    [switch]$NewApiKey,
    [string]$ApiKeyName = "integration",
    [ValidateSet("Administrator", "Manager", "Accountant", "Hr", "Registrar")]
    [string[]]$ApiKeyRoles = @("Manager")
)

$ErrorActionPreference = "Stop"
$RepoRoot   = Split-Path -Parent $PSScriptRoot
$AppDir     = Join-Path $RepoRoot "src\KirkukInstitute"
$AppSettings = Join-Path $AppDir "appsettings.json"
$ReaderSql  = Join-Path $AppDir "Ai\Sql\CreateAiReader.sql"

function Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }
function Ok($text)   { Write-Host "    [ok] $text" -ForegroundColor Green }
function Warn($text) { Write-Host "    [!]  $text" -ForegroundColor Yellow }

# ── Safety: the model endpoint must be local ─────────────────────────────────
$uri = [Uri]$OllamaUrl
if (-not $uri.IsLoopback) { throw "OllamaUrl must be a loopback address (127.0.0.1 / localhost). Got '$OllamaUrl'." }

# ── 1. Ollama installed? ─────────────────────────────────────────────────────
Step "Checking Ollama"
$ollama = Get-Command ollama -ErrorAction SilentlyContinue
if (-not $ollama) {
    $candidate = Join-Path $env:LOCALAPPDATA "Programs\Ollama\ollama.exe"
    if (Test-Path $candidate) { $ollama = Get-Item $candidate }
}
if (-not $ollama) {
    Warn "Ollama not found. Installing with winget..."
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw "winget is not available. Install Ollama manually from https://ollama.com/download/windows and re-run."
    }
    winget install --id Ollama.Ollama -e --accept-source-agreements --accept-package-agreements
    $ollama = Get-Item (Join-Path $env:LOCALAPPDATA "Programs\Ollama\ollama.exe")
}
$OllamaExe = if ($ollama -is [System.Management.Automation.CommandInfo]) { $ollama.Source } else { $ollama.FullName }
Ok "Ollama: $OllamaExe ($(& $OllamaExe --version 2>$null | Select-Object -Last 1))"

# ── 2. Bind to loopback only ─────────────────────────────────────────────────
Step "Restricting Ollama to the loopback interface"
$hostValue = "$($uri.Host):$($uri.Port)"
if ([Environment]::GetEnvironmentVariable("OLLAMA_HOST", "User") -ne $hostValue) {
    [Environment]::SetEnvironmentVariable("OLLAMA_HOST", $hostValue, "User")
    Ok "OLLAMA_HOST set to $hostValue for the current user (restart the Ollama tray app to apply)."
} else { Ok "OLLAMA_HOST already $hostValue" }
$env:OLLAMA_HOST = $hostValue

# ── 3. Server reachable? ─────────────────────────────────────────────────────
Step "Checking the Ollama API at $OllamaUrl"
function Test-Ollama { try { Invoke-RestMethod "$OllamaUrl/api/version" -TimeoutSec 5 } catch { $null } }
$version = Test-Ollama
if (-not $version) {
    Warn "Ollama is not running. Starting 'ollama serve' in the background..."
    Start-Process -FilePath $OllamaExe -ArgumentList "serve" -WindowStyle Hidden
    for ($i = 0; $i -lt 30 -and -not $version; $i++) { Start-Sleep -Seconds 1; $version = Test-Ollama }
    if (-not $version) { throw "Ollama did not start. Run 'ollama serve' manually and check the log." }
}
Ok "Ollama API version $($version.version)"

# ── 4. Pull the model ────────────────────────────────────────────────────────
if (-not $SkipPull) {
    Step "Pulling $Model (about 4.7 GB for 7b, 9 GB for 14b)"
    & $OllamaExe pull $Model
    if ($LASTEXITCODE -ne 0) { throw "ollama pull $Model failed." }
}
$tags = (Invoke-RestMethod "$OllamaUrl/api/tags").models.name
if ($tags -notcontains $Model) { throw "Model $Model is not installed. Installed: $($tags -join ', ')" }
Ok "Model installed: $Model"

# ── 5. Smoke test: JSON tool calling ─────────────────────────────────────────
if (-not $SkipSmokeTest) {
    Step "Smoke test: read-only tool call (this can take a minute on CPU)"
    $body = @{
        model    = $Model
        stream   = $false
        options  = @{ temperature = 0 }
        messages = @(
            @{ role = "system"; content = "You are a read-only analyst. Use the tool to answer." },
            @{ role = "user";   content = "How many active students are there?" }
        )
        tools = @(@{
            type = "function"
            function = @{
                name = "get_institute_overview"
                description = "Read-only. Headline counts of the institute."
                parameters = @{ type = "object"; properties = @{}; required = @() }
            }
        })
    } | ConvertTo-Json -Depth 10
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $r = Invoke-RestMethod "$OllamaUrl/api/chat" -Method Post -ContentType "application/json; charset=utf-8" `
        -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 600
    $sw.Stop()
    $call = $r.message.tool_calls | Select-Object -First 1
    if ($call.function.name -eq "get_institute_overview") { Ok "Tool calling works ($([math]::Round($sw.Elapsed.TotalSeconds,1)) s)." }
    else { Warn "The model answered without calling the tool: $($r.message.content)" }

    $ps = & $OllamaExe ps 2>$null
    if ($ps -match "100% CPU") {
        Warn "The model is running on the CPU only. Answers take about 30 to 120 s. A supported GPU, or the smaller 7b model, gives faster replies."
    }
}

# ── 6. appsettings.json ──────────────────────────────────────────────────────
if ($UpdateAppSettings) {
    Step "Writing Ai:Model = $Model to appsettings.json"
    $json = Get-Content $AppSettings -Raw -Encoding UTF8 | ConvertFrom-Json
    $json.Ai.Model = $Model
    $json.Ai.BaseUrl = $OllamaUrl
    ($json | ConvertTo-Json -Depth 20) | Set-Content $AppSettings -Encoding UTF8
    Ok "Updated $AppSettings (restart the web application)."
}

# ── 7. Database reader principal ─────────────────────────────────────────────
if ($ProvisionDatabase) {
    Step "Provisioning the SELECT-only database user [$ReaderPrincipal] on $SqlServer/$Database"
    if ($ReaderPrincipal -notmatch '^[A-Za-z_][A-Za-z0-9_]{0,63}$') { throw "Invalid principal name." }
    $sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
    if (-not $sqlcmd) {
        $sqlcmd = Get-ChildItem "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\*\Tools\Binn\SQLCMD.EXE" -ErrorAction SilentlyContinue | Select-Object -First 1
    }
    if (-not $sqlcmd) { throw "sqlcmd not found. Install the SQL Server command-line tools." }
    $sqlcmdExe = if ($sqlcmd -is [System.Management.Automation.CommandInfo]) { $sqlcmd.Source } else { $sqlcmd.FullName }
    $tmp = Join-Path $env:TEMP "CreateAiReader.$([guid]::NewGuid().ToString('N')).sql"
    (Get-Content $ReaderSql -Raw -Encoding UTF8).Replace("{{PRINCIPAL}}", $ReaderPrincipal) | Set-Content $tmp -Encoding UTF8
    try {
        & $sqlcmdExe -S $SqlServer -E -C -d $Database -b -i $tmp
        if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed ($LASTEXITCODE)." }
        $check = "EXECUTE AS USER = N'$ReaderPrincipal'; SET NOCOUNT ON; " +
                 "SELECT USER_NAME() AS db_user, HAS_PERMS_BY_NAME('dbo.Students','OBJECT','SELECT') AS can_select, " +
                 "HAS_PERMS_BY_NAME('dbo.Payments','OBJECT','INSERT') AS can_insert_payments, " +
                 "HAS_PERMS_BY_NAME('dbo.Payments','OBJECT','DELETE') AS can_delete_payments, " +
                 "HAS_PERMS_BY_NAME('dbo.AspNetUsers','OBJECT','SELECT') AS can_read_users; REVERT;"
        & $sqlcmdExe -S $SqlServer -E -C -d $Database -W -Q $check
        Ok "Reader principal ready (expected: can_select=1, everything else 0)."
    } finally { Remove-Item $tmp -ErrorAction SilentlyContinue }
}

# ── 8. API key for system integrations ───────────────────────────────────────
if ($NewApiKey) {
    Step "Generating a system-integration API key"
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)   # works on Windows PowerShell 5.1 and 7
    $key  = "kai_" + ([Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+','-').Replace('/','_'))
    $hash = ([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($key)) |
             ForEach-Object { $_.ToString("x2") }) -join ""
    Write-Host ""
    Write-Host "    API key (shown ONCE, give it to the client):" -ForegroundColor Yellow
    Write-Host "      $key"
    Write-Host ""
    Write-Host "    Add this to appsettings.json -> Ai:SystemApiKeys (only the hash is stored):"
    $entry = [ordered]@{ Name = $ApiKeyName; KeyHash = $hash; Roles = $ApiKeyRoles } | ConvertTo-Json -Compress
    Write-Host "      $entry"
    Write-Host ""
    Write-Host "    Call:  curl -X POST http://localhost:5088/api/ai/query -H `"X-Api-Key: <key>`" -H `"Content-Type: application/json`" -d `"{\`"question\`":\`"...\`"}`""
}

Step "Done"
Write-Host "    Endpoints (read-only):  POST /api/ai/query   GET /api/ai/health   GET /api/ai/tools"
Write-Host "    Staff UI:               http://localhost:5088/Assistant"
