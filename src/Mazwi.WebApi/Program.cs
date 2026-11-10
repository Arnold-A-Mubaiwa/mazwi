using Mazwi.Application;
using Mazwi.Domain;
using Mazwi.Repository;
using Mazwi.WebApi;
using Npgsql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
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
builder.Services.AddScoped<IStoryService, StoryService>();
builder.Services.AddScoped<IStoryDraftService, StoryDraftService>();
builder.Services.AddScoped<IStoryReviewService, StoryReviewService>();
builder.Services.AddScoped<IStoryFeedService, StoryFeedService>();
builder.Services.AddScoped<IStoryStudioService, StoryStudioService>();
builder.Services.AddSingleton<IStoryMediaStore>(_ => new LocalStoryMediaStore(
    builder.Configuration["MediaStorage:Root"] ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../.local/media"))));

WebApplication app = builder.Build();
app.UseExceptionHandler();
app.MapControllers();

if (args.Contains("--migrate", StringComparer.Ordinal))
{
    string connectionString = app.Configuration.GetConnectionString("Mazwi")
        ?? throw new InvalidOperationException("ConnectionStrings:Mazwi is required.");
    new DatabaseMigrator(connectionString).Migrate(app.Lifetime.ApplicationStopping);
}
else
{
    await app.RunAsync();
}

public partial class Program;
