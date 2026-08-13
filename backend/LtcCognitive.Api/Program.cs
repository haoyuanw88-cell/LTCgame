using LtcCognitive.Api.Data;
using LtcCognitive.Api.Endpoints;
using LtcCognitive.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("LtcDatabase")
    ?? throw new InvalidOperationException("尚未設定 PostgreSQL 連線。請用 Secret Manager 設定 ConnectionStrings:LtcDatabase。");

builder.Services.AddOpenApi();
builder.Services.AddDbContext<LtcDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<IdentityOptions>()
    .Bind(builder.Configuration.GetSection(IdentityOptions.SectionName))
    .Validate(x => !string.IsNullOrWhiteSpace(x.SubjectHashKeyBase64), "Identity subject hash key is required.")
    .ValidateOnStart();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddRateLimiter(options => options.AddPolicy("authentication", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        })));

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
}

app.MapGet("/health", async (LtcDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "healthy", database = "connected", utc = DateTimeOffset.UtcNow })
        : Results.Problem("無法連線 PostgreSQL。", statusCode: 503));

app.MapAssessmentEndpoints();
app.UseRateLimiter();
app.MapIdentityEndpoints();
app.Run();

public partial class Program;
