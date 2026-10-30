using System.Security.Cryptography;
using System.Text;
using Flix.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Flix.Api
{
  internal class Program
  {
    private static void Main(string[] args)
    {
      WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
      string configurationFile = Path.GetFullPath(
        builder.Configuration["Flix:ParametersFile"] ?? "flix.yaml",
        builder.Environment.ContentRootPath);
      FilterParameters parameters = FilterParametersFile.Read(configurationFile);
      string root = Path.GetFullPath(parameters.Root, builder.Environment.ContentRootPath);
      SnapshotManifest manifest = SnapshotFiles.Read(root);
      FilterSnapshot snapshot = new FilterSnapshot(
        manifest.Version, SnapshotFiles.Resolve(root, manifest));
      if (snapshot.Normal.DocumentCount == 0)
      {
        snapshot.Dispose();
        throw new InvalidOperationException("Empty screening index.");
      }
      string? apiKey = builder.Configuration["Flix:ApiKey"];
      if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(apiKey))
      {
        throw new InvalidOperationException("Configure Flix:ApiKey securely for non-development environments.");
      }
      builder.Services.AddSingleton(new FilterManager(
        new ScreeningFilter(snapshot, parameters)));
      builder.Services.AddSingleton<LuceneNameMatcher>();
      builder.Services.AddSingleton<LuceneFieldMatcher>();
      builder.Services.AddSingleton<IScreeningStrategy, IbanScreeningStrategy>();
      builder.Services.AddSingleton<IScreeningStrategy, AccountNumberScreeningStrategy>();
      builder.Services.AddSingleton<IScreeningStrategy, BicScreeningStrategy>();
      builder.Services.AddSingleton<IScreeningStrategy, CountryScreeningStrategy>();
      builder.Services.AddSingleton<IScreeningStrategy, AddressScreeningStrategy>();
      builder.Services.AddSingleton<IScreeningStrategy, PersonNameScreeningStrategy>();
      builder.Services.AddSingleton<IScreeningStrategy, OrganizationNameScreeningStrategy>();
      builder.Services.AddSingleton<IScreeningStrategy, LeiScreeningStrategy>();
      builder.Services.AddSingleton<ScreeningStrategyRegistry>();
      builder.Services.AddSingleton<DecisionService>();
      builder.Services.AddSingleton(new AuditJournal(root));
      // One shared writer for review hits; concurrent REVIEW results must not
      // open hits.jsonl in parallel (sharing violation -> 503 under load).
      builder.Services.AddSingleton(new HitCaseJournal(Path.Combine(root, "review", "hits.jsonl")));
      builder.Services.AddHostedService(sp => new SnapshotManagementService(
        sp.GetRequiredService<FilterManager>(), root, configurationFile,
        builder.Environment.ContentRootPath,
        sp.GetRequiredService<ILogger<SnapshotManagementService>>()));

      WebApplication app = builder.Build();
      app.Use(async (context, next) =>
      {
        if (context.Request.Path.StartsWithSegments("/screen") && !string.IsNullOrEmpty(apiKey))
        {
          string provided = context.Request.Headers["X-Flix-Key"].ToString();
          byte[] expectedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
          byte[] actualBytes = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
          if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
          {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
          }
        }
        await next(context);
      });
      app.MapGet("/health/live", () => Results.Ok(new { Status = "UP" }));
      app.MapGet("/health/ready", (FilterManager manager) =>
      {
        var state = manager.GetStatus();
        return Results.Ok(new { state.ActiveVersion, state.ActiveUsers, state.RetiredCount });
      });
      app.MapPost("/screen", Screen);
      app.Run();
    }

    private static IResult Screen(ScreeningRequest request,
      DecisionService service, AuditJournal journal, HitCaseJournal hits,
      ILogger<Program> logger)
    {
      try
      {
        ScreeningDecision decision = service.Screen(request);
        journal.Append(decision);
        if (!decision.ReleaseAllowed)
        {
          hits.Append(new HitCase { Message = request, Decision = decision });
        }
        return Results.Ok(decision);
      }
      catch (ArgumentException)
      {
        return Results.BadRequest(new { Decision = "ERROR", ReleaseAllowed = false,
          Reason = "Invalid request" });
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Sanctions screening failed; payment must be held");
        return Results.Json(new { Decision = "ERROR", ReleaseAllowed = false,
          Reason = "Screening unavailable; hold payment" }, statusCode: 503);
      }
    }
  }
}
