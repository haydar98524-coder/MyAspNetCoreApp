namespace KirkukInstitute.Ai;

/// <summary>Configuration bound from the "Ai" section of appsettings.json.</summary>
public sealed class AiOptions
{
    public const string Section = "Ai";

    /// <summary>Master switch. When false the /api/ai endpoints answer 503 and nothing is sent to Ollama.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Ollama base URL. Must stay on the loopback interface; the model is never exposed to the network.</summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:11434";

    /// <summary>Ollama model tag, e.g. "qwen2.5:7b" or "qwen2.5:14b".</summary>
    public string Model { get; set; } = "qwen2.5:7b";

    public double Temperature { get; set; } = 0.1;
    public int NumCtx { get; set; } = 8192;

    /// <summary>How long Ollama keeps the model (and its cached prompt prefix) in memory after the last request.</summary>
    public string KeepAlive { get; set; } = "60m";

    /// <summary>Per-question limit. CPU-only inference of a 7B model needs minutes for long answers.</summary>
    public int TimeoutSeconds { get; set; } = 600;

    /// <summary>Load the model and cache the prompt prefix in the background at start-up.</summary>
    public bool WarmUpOnStartup { get; set; } = true;

    /// <summary>Maximum model → tool → model round trips for one question.</summary>
    public int MaxToolRounds { get; set; } = 4;

    /// <summary>Maximum number of tool invocations for one question (across all rounds).</summary>
    public int MaxToolCalls { get; set; } = 6;

    /// <summary>Maximum characters accepted in one user question.</summary>
    public int MaxQuestionLength { get; set; } = 1500;

    /// <summary>Requests per minute allowed for each user / API key.</summary>
    public int RequestsPerMinute { get; set; } = 20;

    /// <summary>
    /// Connection string used exclusively by the AI data layer. It must point at the same database;
    /// every connection opened with it is switched to the <see cref="ReaderPrincipal"/> database user.
    /// When empty, the DefaultConnection string is reused with Application Name / Pooling overrides.
    /// </summary>
    public string? ReadOnlyConnection { get; set; }

    /// <summary>Database user (WITHOUT LOGIN) that holds SELECT-only permissions. See scripts/create-ai-reader.sql.</summary>
    public string ReaderPrincipal { get; set; } = "kirkuk_ai_reader";

    /// <summary>Create / repair the reader principal at start-up (requires db_owner on the app login).</summary>
    public bool ProvisionReaderOnStartup { get; set; } = true;

    /// <summary>Machine-to-machine clients that call /api/ai with an X-Api-Key header.</summary>
    public List<AiApiKey> SystemApiKeys { get; set; } = new();
}

public sealed class AiApiKey
{
    public string Name { get; set; } = "";

    /// <summary>SHA-256 of the key, lowercase hex. The plain key is never stored.</summary>
    public string KeyHash { get; set; } = "";

    /// <summary>Application roles the key acts as (only staff roles are honoured).</summary>
    public List<string> Roles { get; set; } = new();
}
