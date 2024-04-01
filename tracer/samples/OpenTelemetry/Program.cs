using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace OpenTelemetry.AspNetCoreApplication;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure important OpenTelemetry settings
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.ConfigureResource(resource =>
                        resource.AddService(
                            serviceName: Telemetry.ServiceName,
                            serviceVersion: Telemetry.ServiceVersion))
                    .AddSource(Telemetry.ServiceName)
                    //
                    // Instrumentation Libraries
                    // Note: Datadog automatic instrumentation has integrations for several supported libraries,
                    //       so instrumentation libraries with a corresponding integration MAY be disabled.
                    //
                    .AddAspNetCoreInstrumentation() // Datadog Integration Name: AspNetCore
                    .AddHttpClientInstrumentation() // Datadog Integration Name: HttpMessageHandler
                    //
                    // Exporters
                    // Note: Datadog automatic instrumentation will generate and export Datadog spans,
                    //       so OTEL exporters may not have accurate information and SHOULD be disabled.
                    //       If any exporter is sending traces that get forwarded to the Datadog backend,
                    //       they MUST be disabled to prevent the backend from receiving duplicate traces.
                    //
                    // .AddOtlpExporter()
                    ;
            });

        var app = builder.Build();

        app.MapGet("/", () =>
        {
            using (Activity? activity = Telemetry.ActivitySource.StartActivity("SayHello"))
            {
                // Do work and then return a result
                activity?.SetTag("operation.value", 1);
                activity?.SetTag("operation.name", "Saying hello!");
                activity?.SetTag("operation.other-stuff", new int[] { 1, 2, 3 });

                return "Hello World!";
            }
        });

        app.Run();
    }
}
