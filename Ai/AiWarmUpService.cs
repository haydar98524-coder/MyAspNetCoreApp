namespace KirkukInstitute.Ai;

/// <summary>
/// After start-up, loads the local model into memory and caches the assistant's prompt prefix in the background
/// (CPU inference can need a minute or two for this). Failures are logged and never affect the application.
/// </summary>
public sealed class AiWarmUpService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AiWarmUpService> _logger;

    public AiWarmUpService(IServiceScopeFactory scopes, ILogger<AiWarmUpService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            using var scope = _scopes.CreateScope();
            var assistant = scope.ServiceProvider.GetRequiredService<AssistantService>();
            var started = DateTime.UtcNow;
            var r = await assistant.WarmUpAsync(stoppingToken);
            _logger.LogInformation("AI model warmed up in {Seconds:0.0}s ({Tokens} prompt tokens cached).",
                (DateTime.UtcNow - started).TotalSeconds, r.PromptEvalCount);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning("AI warm-up skipped: {Message}", ex.Message);
        }
    }
}
