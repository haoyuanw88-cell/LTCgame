using LtcCognitive.Api.Data;
using LtcCognitive.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("LtcDatabase")
    ?? throw new InvalidOperationException("尚未設定 PostgreSQL 連線。請用 Secret Manager 設定 ConnectionStrings:LtcDatabase。");

builder.Services.AddOpenApi();
builder.Services.AddDbContext<LtcDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapGet("/health", async (LtcDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "healthy", database = "connected", utc = DateTimeOffset.UtcNow })
        : Results.Problem("無法連線 PostgreSQL。", statusCode: 503));

app.MapParticipantEndpoints();
app.MapAssessmentEndpoints();
app.Run();

public partial class Program;
