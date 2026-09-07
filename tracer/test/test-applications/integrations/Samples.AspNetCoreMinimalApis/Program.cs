using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Samples.AspNetCoreMvc.Shared;
using static Microsoft.AspNetCore.Http.Results;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
var app = builder.Build();

var includeRouteEdgeCases = Environment.GetEnvironmentVariable("ADD_ROUTE_EDGE_CASES") == "1";

if (Environment.GetEnvironmentVariable("ADD_ACTIVITY_MIDDLEWARE") == "1")
{
    app.UseMiddleware<ActivityMiddleware>();
}

app.UseMiddleware<PingMiddleware>();

if (includeRouteEdgeCases)
{
    // Turns an error status into an error page by re-running the pipeline against a different path.
    // Scoped to /re-execute so that every other test case keeps the default pipeline.
    app.UseWhen(
        context => context.Request.Path.StartsWithSegments("/re-execute"),
        branch => branch.UseStatusCodePagesWithReExecute("/status-code/{0}"));

    // Rewrites the path the way a URL-rewriting middleware would, so that routing runs against
    // a different path than the one the request arrived on. UseRouting has to be called
    // explicitly here, otherwise WebApplication adds it ahead of this middleware.
    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/rewrite-me")
        {
            context.Request.Path = "/alive-check";
        }

        await next();
    });

    // Strips the path base from requests mounted under it, the way an app hosted behind a
    // reverse proxy or in a sub-application would be. Routing then runs against the remaining
    // path.
    app.UsePathBase("/path-base");

    app.UseRouting();

    // A route template that matches the application root, which ASP.NET Core stores as the
    // empty string. Registered before the default route so that it is the one that matches "/".
    app.MapControllerRoute("empty-route-template", string.Empty, new { controller = "Home", action = "Index" });
}

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Map("/branch", x => x.UseMiddleware<PingMiddleware>());

app.Map("/shutdown", x =>
{
    x.Run(async context =>
    {
        var hostApplicationLifetime = context.RequestServices.GetRequiredService<IHostApplicationLifetime>();
        await context.Response.WriteAsync("Shutting down");
        _ = Task.Run(() => hostApplicationLifetime.StopApplication());
    });
});

app.MapGet("/api/delay/{seconds}", (int seconds, HttpContext context) =>
{
    Thread.Sleep(TimeSpan.FromSeconds(seconds));
    AddCorrelationIdentifierToResponse(context);
    return Ok(seconds);
});
app.MapGet("/api/delay-async/{seconds}", async (int seconds, HttpContext context) =>
{
    await Task.Delay(TimeSpan.FromSeconds(seconds));
    AddCorrelationIdentifierToResponse(context);
    return Ok(seconds);
});

app.Run();

void AddCorrelationIdentifierToResponse(HttpContext context)
{
    const string CorrelationIdentifierHeaderName = "sample.correlation.identifier";

    if (context.Request.Headers.ContainsKey(CorrelationIdentifierHeaderName))
    {
        context.Response.Headers[CorrelationIdentifierHeaderName] = context.Request.Headers[CorrelationIdentifierHeaderName];
    }
}
