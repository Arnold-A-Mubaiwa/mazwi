using System.Threading.RateLimiting;
using Mazwi.Ai;
using Mazwi.Application;
using Mazwi.Domain;
using Mazwi.Repository;
using Mazwi.WebApi;
using Microsoft.AspNetCore.Authentication.Cookies;
using Npgsql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
DatabaseSafety.EnsureAppDatabase(builder.Configuration.GetConnectionString("Mazwi"), builder.Environment);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "mazwi_session";
    options.Cookie.HttpOnly = true;
    // Lax keeps the cookie off cross-site POSTs, which is the CSRF defence for this JSON/multipart API.
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context => WriteStatus(context.Response, StatusCodes.Status401Unauthorized);
    options.Events.OnRedirectToAccessDenied = context => WriteStatus(context.Response, StatusCodes.Status403Forbidden);
    options.Events.OnValidatePrincipal = async context =>
    {
        // A session whose account no longer exists is signed out.
        Guid artistId = context.Principal?.ArtistId() ?? Guid.Empty;
        IArtistAccountService accounts = context.HttpContext.RequestServices.GetRequiredService<IArtistAccountService>();
        if (artistId == Guid.Empty || await accounts.GetAsync(artistId, context.HttpContext.RequestAborted) is null)
        {
            context.RejectPrincipal();
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AccountLimits.Policy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = context.RequestServices.GetRequiredService<IConfiguration>().GetValue("Mazwi:AccountRequestsPerMinute", 10),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    // Per story, so one busy story cannot block counting for the rest of the site.
    options.AddPolicy(PlayLimits.Policy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Request.RouteValues["id"]?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = context.RequestServices.GetRequiredService<IConfiguration>().GetValue("Mazwi:PlaysPerStoryPerMinute", 120),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});
builder.Services.AddSingleton(_ =>
{
    string? connectionString = builder.Configuration.GetConnectionString("Mazwi");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Configure ConnectionStrings:Mazwi through user-secrets or environment variables.");
    }

    return NpgsqlDataSource.Create(connectionString);
});
builder.Services.AddScoped<ITaskItemRepository, TaskItemRepository>();
builder.Services.AddScoped<ITaskItemService, TaskItemService>();
builder.Services.AddScoped<IStoryRepository, StoryRepository>();
builder.Services.AddScoped<IArtistRepository, ArtistRepository>();
builder.Services.AddSingleton<Mazwi.Domain.IPasswordHasher, AspNetPasswordHasher>();
builder.Services.AddScoped<IArtistAccountService, ArtistAccountService>();
builder.Services.AddScoped<IStoryAccessService, StoryAccessService>();
builder.Services.AddScoped<IStoryService, StoryService>();
builder.Services.AddScoped<IStoryDraftService, StoryDraftService>();
// A raw HttpClient has no request/response logging handlers or retry policies.
builder.Services.AddSingleton(_ => new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
{
    Timeout = Timeout.InfiniteTimeSpan
});
builder.Services.AddSingleton<ITranslationDrafter>(services => new OpenAiTranslationDrafter(
    services.GetRequiredService<HttpClient>(), builder.Configuration["OPENAI_API_KEY"],
    builder.Configuration["MAZWI_TRANSLATION_MODEL"] ?? "gpt-6-luna"));
builder.Services.AddSingleton<ISpeechGenerator>(_ => new OpenAiSpeechGenerator(builder.Configuration["OPENAI_API_KEY"],
    builder.Configuration["MAZWI_SPEECH_MODEL"] ?? "gpt-realtime-2.1-mini"));
builder.Services.AddScoped<IStoryVoiceService, StoryVoiceService>();
builder.Services.AddSingleton<ITranscriber>(services => new OpenAiTranscriber(
    services.GetRequiredService<HttpClient>(), builder.Configuration["OPENAI_API_KEY"],
    builder.Configuration["MAZWI_TRANSCRIPTION_MODEL"] ?? "gpt-transcribe"));
builder.Services.AddScoped<IStoryTranscriptService, StoryTranscriptService>();
builder.Services.AddScoped<IStoryReviewService, StoryReviewService>();
builder.Services.AddScoped<IStoryFeedService, StoryFeedService>();
builder.Services.AddScoped<IStoryStudioService, StoryStudioService>();
builder.Services.AddScoped<IStorySubtitleService, StorySubtitleService>();
builder.Services.AddScoped<IStoryPlayRepository, StoryPlayRepository>();
builder.Services.AddScoped<IStoryStatsService, StoryStatsService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IStoryMediaStore>(_ => new LocalStoryMediaStore(
    builder.Configuration["MediaStorage:Root"] ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../.local/media"))));

WebApplication app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

int claimIndex = Array.IndexOf(args, "--claim-stories");
int importIndex = Array.IndexOf(args, "--import-stories");
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    string connectionString = app.Configuration.GetConnectionString("Mazwi")
        ?? throw new InvalidOperationException("ConnectionStrings:Mazwi is required.");
    new DatabaseMigrator(connectionString).Migrate(app.Lifetime.ApplicationStopping);
}
else if (claimIndex >= 0)
{
    // Gives every story saved before accounts existed to the named account.
    if (claimIndex + 1 >= args.Length)
    {
        Console.Error.WriteLine("Usage: --claim-stories <email of an existing account>");
        Environment.ExitCode = 1;
    }
    else
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        try
        {
            int claimed = await scope.ServiceProvider.GetRequiredService<IArtistAccountService>().ClaimUnownedStoriesAsync(args[claimIndex + 1], CancellationToken.None);
            Console.WriteLine($"Claimed {claimed} unowned stories.");
        }
        catch (DomainException failure)
        {
            Console.Error.WriteLine(failure.Message);
            Environment.ExitCode = 1;
        }
    }
}
else if (importIndex >= 0)
{
    // Copies unowned stories by one artist from ConnectionStrings:MazwiImportSource into this app's database.
    string? sourceConnection = app.Configuration.GetConnectionString("MazwiImportSource");
    if (importIndex + 1 >= args.Length || string.IsNullOrWhiteSpace(sourceConnection))
    {
        Console.Error.WriteLine("Usage: --import-stories <artist name> [--apply], with ConnectionStrings:MazwiImportSource set to the old database.");
        Environment.ExitCode = 1;
    }
    else
    {
        bool apply = args.Contains("--apply", StringComparer.Ordinal);
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(sourceConnection);
        var importer = new StoryImporter(source, app.Services.GetRequiredService<NpgsqlDataSource>());
        IReadOnlyList<ImportedStory> stories = await importer.ImportUnownedAsync(args[importIndex + 1], apply, CancellationToken.None);
        foreach (ImportedStory story in stories)
        {
            Console.WriteLine($"{(story.AlreadyPresent ? "already here" : apply ? "copied" : "would copy")}: {story.Title} ({story.Id})");
        }

        Console.WriteLine(apply ? $"Done. {stories.Count(story => !story.AlreadyPresent)} copied." : $"Dry run: {stories.Count} found. Add --apply to copy them.");
    }
}
else
{
    await app.RunAsync();
}

static Task WriteStatus(HttpResponse response, int status)
{
    response.StatusCode = status;
    return Task.CompletedTask;
}

public partial class Program;
