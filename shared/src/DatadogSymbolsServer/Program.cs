using System.Text.RegularExpressions;
using DatadogSymbolsServer;
using Microsoft.AspNetCore.Http.HttpResults;
using Polly;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSystemd();

var services = builder.Services;
services.AddSingleton<ISymbolsCache, DotnetApmSymbolsCache>();
services.AddAuthorization();
services.AddCors();
// Add named httpclient, with resiliency policy
services.AddHttpClient("github", client =>
    {
        client.BaseAddress = new Uri("https://github.com/");
        client.Timeout = TimeSpan.FromMinutes(3);
    })
    .AddTransientHttpErrorPolicy(policyBuilder =>
        policyBuilder.RetryAsync(3))
    .AddTransientHttpErrorPolicy(policyBuilder =>
        policyBuilder.CircuitBreakerAsync(5, TimeSpan.FromSeconds(20)));

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseRouting();
app.UseAuthorization();

// Configure endpoints
app.MapGet("/debuginfod/buildid/{guid}/{kind}", Endpoints.GetByGuid);
app.MapGet("/ms/{file}/{guid}/{ignored}", Endpoints.GetByFile);
app.MapGet("/ingest", Endpoints.IngestNewVersion);

// prep the cache
using (var scope = app.Services.CreateScope())
{
    var cache = scope.ServiceProvider.GetRequiredService<ISymbolsCache>();
    await cache.Initialize(app.Lifetime.ApplicationStopping);
}

await app.RunAsync();


public partial class Endpoints
{
    private static readonly Regex _fileRegex = GetFileRegex();

    [GeneratedRegex(@"elf-buildid-(sym-)?(?<guid>[0-9a-f]+)", RegexOptions.IgnoreCase)]
    private static partial Regex GetFileRegex();

    // kind can be symbol file or binary file.
    // Did not find yet how it's used by gdb. For now just use it as the download filename
    public static Results<FileStreamHttpResult, NotFound> GetByGuid(
        string guid, string kind, ILogger<Endpoints> logger, ISymbolsCache symbolsCache)
    {
        logger.LogInformation("Getting file {Guid}", guid);
        var symbolFromCache = symbolsCache.Get(guid, SymbolKind.Linux);
        if (symbolFromCache != null)
        {
            logger.LogInformation("Found {Guid} in cache", guid);
            return TypedResults.File(symbolFromCache, "application/octet-stream", fileDownloadName: kind);
        }

        return TypedResults.NotFound();
    }

    public static Results<FileStreamHttpResult, NotFound> GetByFile(
        string file, string guid, string ignored, ILogger<Endpoints> logger, ISymbolsCache symbolsCache)
    {
        _ = ignored; // to stop it complaining about unused;

        var match = _fileRegex.Match(guid);
        var (guidd, kind) = match.Success switch
        {
            true => (match.Groups["guid"].Value, SymbolKind.Linux),
            _ => (guid, SymbolKind.Windows)
        };

        logger.LogInformation("Getting file {File}", file);
        var symbolsFile = symbolsCache.Get(guidd, kind);
        if (symbolsFile != null)
        {
            logger.LogInformation("Found {File} in cache", file);
            return TypedResults.File(symbolsFile, "application/octet-stream", fileDownloadName: file);
        }

        return TypedResults.NotFound();
    }

    public static async Task IngestNewVersion(string version, ISymbolsCache symbolsCache, CancellationToken cancellationToken)
    {
        await symbolsCache.Ingest(version, cancellationToken);
    }
}