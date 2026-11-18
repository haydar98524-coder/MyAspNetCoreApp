using System.Security.Cryptography;
using System.Text;
using KirkukInstitute.Ai;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace KirkukInstitute.Controllers;

/// <summary>
/// HTTP endpoint of the read-only AI assistant (local Ollama model).
///
/// Authentication (one of):
///   * X-Api-Key header: a system integration key configured in Ai:SystemApiKeys (stored as SHA-256 only);
///   * the staff sign-in cookie plus the antiforgery token in the "RequestVerificationToken" header.
///
/// The endpoint can only answer questions. It exposes no operation that creates, changes, deletes or
/// moves money, and the model behind it is restricted to read-only tools.
/// </summary>
[ApiController]
[Route("api/ai")]
[AllowAnonymous] // authentication is resolved explicitly below so API clients get JSON 401/403, not a login redirect
[EnableRateLimiting(AiRateLimit.Policy)]
[Produces("application/json")]
public class AiController : ControllerBase
{
    public const string ApiKeyHeader = "X-Api-Key";

    private readonly AssistantService _assistant;
    private readonly AiToolRegistry _registry;
    private readonly IAntiforgery _antiforgery;
    private readonly AiOptions _options;

    public AiController(AssistantService assistant, AiToolRegistry registry, IAntiforgery antiforgery, IOptions<AiOptions> options)
    {
        _assistant = assistant;
        _registry = registry;
        _antiforgery = antiforgery;
        _options = options.Value;
    }

    /// <summary>Ask a question. Body: { "question": "...", "language": "ar|en" (optional), "history": [ {role, content} ] (optional) }.</summary>
    [HttpPost("query")]
    [Consumes("application/json")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> Query([FromBody] AiQueryRequest request, CancellationToken ct)
    {
        if (!_options.Enabled) return Disabled();
        var (caller, problem) = await ResolveCallerAsync(requireAntiforgery: true, request.Language);
        if (problem is not null) return problem;

        var result = await _assistant.AskAsync(caller!, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return StatusCode(result.StatusCode, result.Body);
    }

    /// <summary>Model, Ollama and read-only database principal status (including the live permission matrix).</summary>
    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var (_, problem) = await ResolveCallerAsync(requireAntiforgery: false, null);
        if (problem is not null) return problem;
        return Ok(await _assistant.GetHealthAsync(ct));
    }

    /// <summary>The read-only tools the caller's role may use, and the JSON schema every answer follows.</summary>
    [HttpGet("tools")]
    public async Task<IActionResult> Tools()
    {
        var (caller, problem) = await ResolveCallerAsync(requireAntiforgery: false, null);
        if (problem is not null) return problem;
        return Ok(new
        {
            mode = "read_only",
            roles = caller!.RoleSet,
            tools = _registry.ForCaller(caller).Select(t => new { name = t.Name, description = t.Description, parameters = t.Parameters }),
            answer_schema = AssistantService.AnswerSchema()
        });
    }

    private async Task<(AiCaller? Caller, IActionResult? Problem)> ResolveCallerAsync(bool requireAntiforgery, string? language)
    {
        if (Request.Headers.TryGetValue(ApiKeyHeader, out var supplied) && !string.IsNullOrEmpty(supplied.ToString()))
        {
            var key = MatchApiKey(supplied.ToString());
            if (key is null) return (null, Error(401, "invalid_api_key", "The API key is not valid."));
            var keyCaller = AssistantService.CallerFromApiKey(key);
            if (keyCaller.RoleSet.Count == 0) return (null, Error(403, "forbidden", "The API key has no staff role."));
            return (keyCaller, null);
        }

        if (User.Identity?.IsAuthenticated != true)
            return (null, Error(401, "unauthenticated", "Sign in as staff or send an X-Api-Key header."));

        var caller = AssistantService.CallerFromPrincipal(User, language ?? User.FindFirst("PreferredLanguage")?.Value);
        if (caller.RoleSet.Count == 0)
            return (null, Error(403, "forbidden", "The assistant is available to staff accounts only."));

        if (requireAntiforgery)
        {
            try { await _antiforgery.ValidateRequestAsync(HttpContext); }
            catch (AntiforgeryValidationException)
            {
                return (null, Error(400, "antiforgery", "Missing or invalid RequestVerificationToken header."));
            }
        }
        return (caller, null);
    }

    /// <summary>Compares SHA-256 hashes in constant time against every configured key.</summary>
    private AiApiKey? MatchApiKey(string plain)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plain.Trim()));
        AiApiKey? match = null;
        foreach (var key in _options.SystemApiKeys)
        {
            byte[] expected;
            try { expected = Convert.FromHexString(key.KeyHash.Trim()); }
            catch (FormatException) { continue; }
            if (expected.Length == hash.Length && CryptographicOperations.FixedTimeEquals(expected, hash)) match = key;
        }
        return match;
    }

    public static string HashApiKey(string plain) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plain.Trim()))).ToLowerInvariant();

    private IActionResult Disabled() => Error(503, "disabled", "The AI assistant is disabled (Ai:Enabled = false).");

    private ObjectResult Error(int status, string code, string message) =>
        StatusCode(status, new { ok = false, mode = "read_only", error = new { code, message } });
}

public static class AiRateLimit
{
    public const string Policy = "ai";
}
