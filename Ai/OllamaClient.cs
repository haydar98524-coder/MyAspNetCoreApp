using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace KirkukInstitute.Ai;

/// <summary>Thin typed client for the local Ollama HTTP API (/api/chat, /api/tags, /api/version).</summary>
public sealed class OllamaClient
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly HttpClient _http;
    private readonly AiOptions _options;

    public OllamaClient(HttpClient http, IOptions<AiOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public string Model => _options.Model;

    public async Task<OllamaChatResponse> ChatAsync(
        IReadOnlyList<OllamaMessage> messages,
        IReadOnlyList<JsonObject>? tools,
        JsonNode? format,
        CancellationToken ct,
        int? numPredict = null)
    {
        var request = new OllamaChatRequest
        {
            Model = _options.Model,
            Messages = messages,
            Tools = tools is { Count: > 0 } ? tools : null,
            Format = format,
            Stream = false,
            KeepAlive = _options.KeepAlive,
            Options = new OllamaModelOptions { Temperature = _options.Temperature, NumCtx = _options.NumCtx, NumPredict = numPredict }
        };

        using var response = await _http.PostAsJsonAsync("/api/chat", request, Json, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new OllamaException($"Ollama returned {(int)response.StatusCode}: {Truncate(body, 400)}");
        }

        return await response.Content.ReadFromJsonAsync<OllamaChatResponse>(Json, ct)
               ?? throw new OllamaException("Ollama returned an empty response.");
    }

    public async Task<string?> GetVersionAsync(CancellationToken ct)
    {
        var doc = await _http.GetFromJsonAsync<JsonElement>("/api/version", ct);
        return doc.TryGetProperty("version", out var v) ? v.GetString() : null;
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct)
    {
        var doc = await _http.GetFromJsonAsync<JsonElement>("/api/tags", ct);
        var names = new List<string>();
        if (doc.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in models.EnumerateArray())
                if (m.TryGetProperty("name", out var n) && n.GetString() is { } s) names.Add(s);
        }
        return names;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}

public sealed class OllamaException(string message) : Exception(message);

public sealed class OllamaChatRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = "";
    [JsonPropertyName("messages")] public IReadOnlyList<OllamaMessage> Messages { get; set; } = [];
    [JsonPropertyName("tools")] public IReadOnlyList<JsonObject>? Tools { get; set; }
    [JsonPropertyName("format")] public JsonNode? Format { get; set; }
    [JsonPropertyName("stream")] public bool Stream { get; set; }
    [JsonPropertyName("keep_alive")] public string? KeepAlive { get; set; }
    [JsonPropertyName("options")] public OllamaModelOptions? Options { get; set; }
}

public sealed class OllamaModelOptions
{
    [JsonPropertyName("temperature")] public double Temperature { get; set; }
    [JsonPropertyName("num_ctx")] public int NumCtx { get; set; }
    [JsonPropertyName("num_predict")] public int? NumPredict { get; set; }
}

public sealed class OllamaMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = "user";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
    [JsonPropertyName("tool_calls")] public List<OllamaToolCall>? ToolCalls { get; set; }
    [JsonPropertyName("tool_name")] public string? ToolName { get; set; }

    public static OllamaMessage System(string text) => new() { Role = "system", Content = text };
    public static OllamaMessage User(string text) => new() { Role = "user", Content = text };
    public static OllamaMessage Assistant(string text) => new() { Role = "assistant", Content = text };
    public static OllamaMessage Tool(string name, string json) => new() { Role = "tool", ToolName = name, Content = json };
}

public sealed class OllamaToolCall
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("function")] public OllamaFunctionCall Function { get; set; } = new();
}

public sealed class OllamaFunctionCall
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    /// <summary>Ollama normally sends an object; some models emit a JSON string instead. Both are accepted.</summary>
    [JsonPropertyName("arguments")] public JsonElement Arguments { get; set; }

    public JsonElement ArgumentsObject()
    {
        if (Arguments.ValueKind == JsonValueKind.Object) return Arguments;
        if (Arguments.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var doc = JsonDocument.Parse(Arguments.GetString() ?? "{}");
                if (doc.RootElement.ValueKind == JsonValueKind.Object) return doc.RootElement.Clone();
            }
            catch (JsonException) { }
        }
        using var empty = JsonDocument.Parse("{}");
        return empty.RootElement.Clone();
    }
}

public sealed class OllamaChatResponse
{
    [JsonPropertyName("model")] public string? Model { get; set; }
    [JsonPropertyName("message")] public OllamaMessage Message { get; set; } = new();
    [JsonPropertyName("done")] public bool Done { get; set; }
    [JsonPropertyName("done_reason")] public string? DoneReason { get; set; }
    [JsonPropertyName("total_duration")] public long TotalDuration { get; set; }
    [JsonPropertyName("prompt_eval_count")] public int PromptEvalCount { get; set; }
    [JsonPropertyName("prompt_eval_duration")] public long PromptEvalDuration { get; set; }
    [JsonPropertyName("eval_count")] public int EvalCount { get; set; }
    [JsonPropertyName("eval_duration")] public long EvalDuration { get; set; }
}
