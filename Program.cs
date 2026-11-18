using System.Globalization;
using System.Threading.RateLimiting;
using KirkukInstitute.Ai;
using KirkukInstitute.Controllers;
using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data"));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured in appsettings.json.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
        sql.CommandTimeout(60);
        // Pages that include several collections (course → sections → enrollments, ...) are
        // loaded with one query per collection instead of one large cartesian join.
        sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    }));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
        options.Lockout.MaxFailedAccessAttempts = 6;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AppClaimsPrincipalFactory>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "KirkukInstitute.Auth";
    options.ExpireTimeSpan = TimeSpan.FromHours(10);
    options.SlidingExpiration = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<Localizer>();
builder.Services.AddScoped<DocumentNumberService>();
builder.Services.AddScoped<AccountingService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<TuitionService>();
builder.Services.AddScoped<CashierShiftService>();
builder.Services.AddScoped<TeacherSettlementService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<FileStorageService>();
builder.Services.AddScoped<MatchingService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<StudentNumberService>();
builder.Services.AddScoped<StudentExportService>();

// ── Local AI assistant (Ollama, read-only) ───────────────────────────────────
var aiSection = builder.Configuration.GetSection(AiOptions.Section);
builder.Services.Configure<AiOptions>(aiSection);
var aiOptions = aiSection.Get<AiOptions>() ?? new AiOptions();

// The model endpoint must stay on this machine: prompts and institute data never leave the server.
if (!Uri.TryCreate(aiOptions.BaseUrl, UriKind.Absolute, out var ollamaUri) || !ollamaUri.IsLoopback)
    throw new InvalidOperationException($"Ai:BaseUrl must be a loopback address (e.g. http://127.0.0.1:11434). Got '{aiOptions.BaseUrl}'.");

// Separate EF context for the assistant: no tracking, SaveChanges throws, only SELECT commands pass,
// and every connection is switched to the SELECT-only database user before it is used.
builder.Services.AddDbContext<AiReadOnlyDbContext>(options =>
    options.UseSqlServer(
            AiReaderProvisioner.BuildConnectionString(aiOptions.ReadOnlyConnection, connectionString),
            sql =>
            {
                sql.CommandTimeout(30);
                sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        .AddInterceptors(new ReaderImpersonationInterceptor(aiOptions.ReaderPrincipal), new ReadOnlyCommandGuard()));

builder.Services.AddHttpClient<OllamaClient>(client =>
{
    client.BaseAddress = ollamaUri;
    client.Timeout = TimeSpan.FromSeconds(Math.Max(10, aiOptions.TimeoutSeconds) + 10);
});
// Explicit factory: the registry is always the fixed compile-time catalog, never whatever DI could inject.
builder.Services.AddSingleton(_ => new AiToolRegistry());
builder.Services.AddScoped<AssistantService>();
if (aiOptions.Enabled && aiOptions.WarmUpOnStartup)
    builder.Services.AddHostedService<AiWarmUpService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AiRateLimit.Policy, http =>
    {
        // Partition per API key, else per signed-in user, else per client IP.
        var key = http.Request.Headers[AiController.ApiKeyHeader].ToString() is { Length: > 0 } k
            ? "key:" + AiController.HashApiKey(k)
            : http.User.Identity?.IsAuthenticated == true
                ? "user:" + http.User.CurrentUserId()
                : "ip:" + http.Connection.RemoteIpAddress;
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, aiOptions.RequestsPerMinute),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            """{"ok":false,"mode":"read_only","error":{"code":"rate_limited","message":"Too many requests. Try again in a minute."}}""", ct);
    };
});

var supportedCultures = new[]
{
    new CultureInfo("ar"),
    new CultureInfo("en")
};
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("ar");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await StudentRegistrationSchema.EnsureAsync(db);
    await Phase1FinanceSchema.EnsureAsync(db);
    await DbSeeder.SeedAsync(scope.ServiceProvider);
    // After seeding, so demo/legacy enrollments created by the seeder also receive contracts.
    await Phase1FinanceSchema.BackfillAsync(db);

    if (aiOptions.Enabled && aiOptions.ProvisionReaderOnStartup)
        await AiReaderProvisioner.ProvisionAsync(db, aiOptions.ReaderPrincipal,
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("KirkukInstitute.Ai"));

    // Fail fast if a tool that violates the read-only naming policy was ever added.
    _ = scope.ServiceProvider.GetRequiredService<AiToolRegistry>();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");
app.UseStaticFiles();
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
