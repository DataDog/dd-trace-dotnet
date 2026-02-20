using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Collections.Generic;

namespace Samples.Ocelot.DistributedTracing
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // Simulate a customer environment where the OpenTelemetry SDK is configured.
            // The OTel SDK replaces DistributedContextPropagator.Current with its own propagator,
            // which causes SocketsHttpHandler's internal DiagnosticsHandler to overwrite Datadog
            // trace context headers on forwarded requests.
            services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService("ocelot-sample"))
                .WithTracing(builder =>
                {
                    builder
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddOtlpExporter(o =>
                        {
                            // Use a non-routable endpoint so exports silently fail
                            o.Endpoint = new System.Uri("http://192.0.2.1:4317");
                        });
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

            services.AddOcelot(configuration);
            services.AddHostedService<Worker>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });
            });

            app.UseOcelot().Wait();
        }
    }
}
