using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using KirkukInstitute.Models;

namespace KirkukInstitute.Ai;

/// <summary>Who is asking. Built from the signed-in staff user or from a configured system API key.</summary>
public sealed record AiCaller(string Id, string Name, IReadOnlySet<string> RoleSet, string Language, bool IsSystemKey)
{
    public bool InAny(string csvRoles) =>
        csvRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(RoleSet.Contains);

    public bool IsArabic => Language == "ar";

    /// <summary>Monetary detail is only ever returned to Administrator / Manager / Accountant.</summary>
    public bool CanSeeFinance => InAny(Roles.Finance);

    public string PrimaryRole =>
        new[] { Roles.Administrator, Roles.Manager, Roles.Accountant, Roles.Hr, Roles.Registrar }
            .FirstOrDefault(RoleSet.Contains) ?? "Staff";
}

public sealed class AiToolContext
{
    public required AiReadOnlyDbContext Db { get; init; }
    public required AiCaller Caller { get; init; }
    public required InstituteSettings Settings { get; init; }

    public string Pick(string? ar, string? en) =>
        Caller.IsArabic ? (string.IsNullOrWhiteSpace(ar) ? en ?? "" : ar) : (string.IsNullOrWhiteSpace(en) ? ar ?? "" : en);
}

public sealed class ToolArgumentException(string message) : Exception(message);

/// <summary>Strict, typed accessor for model-supplied tool arguments. Every value is bounds-checked.</summary>
public sealed class ToolArgs
{
    private static readonly JsonElement Empty = JsonDocument.Parse("{}").RootElement.Clone();
    private readonly JsonElement _root;

    public ToolArgs(JsonElement root) => _root = root.ValueKind == JsonValueKind.Object ? root : Empty;

    public JsonElement Raw => _root;

    private bool TryGet(string name, out JsonElement value)
    {
        if (_root.TryGetProperty(name, out value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            return true;
        value = default;
        return false;
    }

    public string? Str(string name, int maxLength = 80)
    {
        if (!TryGet(name, out var v)) return null;
        var s = v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => throw new ToolArgumentException($"'{name}' must be a string.")
        };
        s = s?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (s.Length > maxLength) throw new ToolArgumentException($"'{name}' is longer than {maxLength} characters.");
        return s;
    }

    public int? Int(string name, int min, int max)
    {
        if (!TryGet(name, out var v)) return null;
        int value;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) value = n;
        else if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p)) value = p;
        else throw new ToolArgumentException($"'{name}' must be an integer.");
        if (value < min || value > max) throw new ToolArgumentException($"'{name}' must be between {min} and {max}.");
        return value;
    }

    public bool Bool(string name, bool fallback)
    {
        if (!TryGet(name, out var v)) return fallback;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(v.GetString(), out var b) => b,
            _ => throw new ToolArgumentException($"'{name}' must be true or false.")
        };
    }

    public DateTime? Date(string name)
    {
        var s = Str(name, 10);
        if (s is null) return null;
        if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d.Date;
        throw new ToolArgumentException($"'{name}' must be a date in the format YYYY-MM-DD.");
    }

    public DateTime? Month(string name)
    {
        var s = Str(name, 10);
        if (s is null) return null;
        if (DateTime.TryParseExact(s, new[] { "yyyy-MM", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return new DateTime(d.Year, d.Month, 1);
        throw new ToolArgumentException($"'{name}' must be a month in the format YYYY-MM.");
    }

    public TEnum? Enum<TEnum>(string name) where TEnum : struct, System.Enum
    {
        var s = Str(name, 40);
        if (s is null || s.Equals("any", StringComparison.OrdinalIgnoreCase) || s.Equals("all", StringComparison.OrdinalIgnoreCase)) return null;
        if (System.Enum.TryParse<TEnum>(s, true, out var e) && System.Enum.IsDefined(e)) return e;
        throw new ToolArgumentException($"'{name}' must be one of: {string.Join(", ", System.Enum.GetNames<TEnum>())}.");
    }

    /// <summary>Validates an optional date range: at most 366 days, from ≤ to. Returns [from, toExclusive).</summary>
    public (DateTime From, DateTime ToExclusive) Range(string fromName, string toName, DateTime defaultFrom, DateTime defaultTo)
    {
        var from = Date(fromName) ?? defaultFrom;
        var to = Date(toName) ?? defaultTo;
        if (to < from) (from, to) = (to, from);
        if ((to - from).TotalDays > 366) throw new ToolArgumentException("The date range cannot exceed 366 days.");
        return (from, to.AddDays(1));
    }
}

/// <summary>
/// A read-only data tool the model may call. Tools can only query, aggregate and describe data —
/// the type intentionally offers no way to reach a writable context or any application service.
/// </summary>
public abstract class AiTool
{
    private JsonObject? _parameters;

    public abstract string Name { get; }
    public abstract string Description { get; }

    /// <summary>Comma-separated application roles allowed to use the tool.</summary>
    public abstract string AllowedRoles { get; }

    /// <summary>JSON-schema of the arguments (an object schema).</summary>
    protected abstract string ParametersSchema { get; }

    public JsonObject Parameters => (JsonObject)(_parameters ??= (JsonObject)JsonNode.Parse(ParametersSchema)!).DeepClone();

    public bool IsAllowedFor(AiCaller caller) => caller.InAny(AllowedRoles);

    public JsonObject ToOllamaTool() => new()
    {
        ["type"] = "function",
        ["function"] = new JsonObject
        {
            ["name"] = Name,
            ["description"] = Description,
            ["parameters"] = Parameters
        }
    };

    public abstract Task<object> ExecuteAsync(AiToolContext ctx, ToolArgs args, CancellationToken ct);

    protected static string D(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    protected static string? D(DateTime? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    protected static decimal Pct(long part, long total) => total == 0 ? 0 : Math.Round(part * 100m / total, 1);
}

/// <summary>
/// The fixed, compile-time set of tools. The constructor enforces the read-only policy as code: a tool whose
/// name or description suggests a write / financial action can never be registered.
/// </summary>
public sealed partial class AiToolRegistry
{
    private readonly Dictionary<string, AiTool> _tools;

    public AiToolRegistry() : this(AiToolCatalog.All()) { }

    internal AiToolRegistry(IEnumerable<AiTool> tools)
    {
        _tools = new Dictionary<string, AiTool>(StringComparer.Ordinal);
        foreach (var tool in tools)
        {
            if (!ReadOnlyName().IsMatch(tool.Name))
                throw new InvalidOperationException($"AI tool '{tool.Name}' violates the read-only naming policy (get_/search_/list_/find_ only).");
            if (WriteVerb().IsMatch(tool.Name))
                throw new InvalidOperationException($"AI tool '{tool.Name}' names a write or financial-transaction action and cannot be registered.");
            if (!_tools.TryAdd(tool.Name, tool))
                throw new InvalidOperationException($"Duplicate AI tool '{tool.Name}'.");
        }
    }

    public IReadOnlyCollection<AiTool> All => _tools.Values;

    public IReadOnlyList<AiTool> ForCaller(AiCaller caller) => _tools.Values.Where(t => t.IsAllowedFor(caller)).ToList();

    /// <summary>Returns the tool only if it exists AND the caller may use it.</summary>
    public AiTool? Resolve(string name, AiCaller caller) =>
        _tools.TryGetValue(name, out var tool) && tool.IsAllowedFor(caller) ? tool : null;

    [GeneratedRegex("^(get|search|list|find)_[a-z0-9_]{2,60}$")]
    private static partial Regex ReadOnlyName();

    [GeneratedRegex("(create|insert|add|update|modify|edit|set|delete|remove|drop|pay|post|approve|collect|transfer|refund|record|save|write|execute|run)", RegexOptions.IgnoreCase)]
    private static partial Regex WriteVerb();
}
