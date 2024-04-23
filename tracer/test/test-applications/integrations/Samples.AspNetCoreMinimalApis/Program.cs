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

app.UseMiddleware<PingMiddleware>();

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
