using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Samples.Ocelot.DistributedTracing;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

// Simulate a customer environment where the OpenTelemetry SDK is configured.
// The OTel SDK replaces DistributedContextPropagator.Current with its own propagator,
// which causes SocketsHttpHandler's internal DiagnosticsHandler to overwrite Datadog
// trace context headers on forwarded requests.
//
// No exporter is configured because the test only needs the propagator replacement
// that AddHttpClientInstrumentation() causes. An OTLP exporter would generate an
// additional http.request span for the export call, which is not relevant to the test.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("ocelot-sample"))
    .WithTracing(opt =>
    {
        opt
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    });

// Create a minimal in-memory configuration for Ocelot startup
// The actual routes will be configured at runtime by the Worker
var ocelotConfig = new Dictionary<string, string>
{
    { "Routes", "[]" },
    { "GlobalConfiguration:BaseUrl", "http://localhost:5000" }
};

var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(ocelotConfig)
    .Build();

builder.Services.AddOcelot(configuration);
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

// Explicit routing so that "/" is handled by endpoint routing before Ocelot middleware
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/", async context =>
    {
        await context.Response.WriteAsync("Hello World!");
    });
});
await app.UseOcelot();

app.Run();
