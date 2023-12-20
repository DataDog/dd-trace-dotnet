using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Samples;

namespace LogsInjection.ILogger
{
    public class Startup
    {
        public static volatile bool AppListening = false;
        public static volatile string ServerAddress = null;
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHostedService<Worker>();
            services.AddHttpClient();
            services.AddProcessLogEnricher(x =>
            {
                x.ProcessId = true;
                x.ThreadId = true;
            });
        }

#pragma warning disable 618 // ignore obsolete IApplicationLifetime
        public void Configure(IApplicationBuilder app, IApplicationLifetime lifetime, ILogger<Startup> logger)
#pragma warning restore 618
        {
            // Not injected as we won't have a traceId
            logger.UninjectedLog("Building pipeline");
            using (var scope = SampleHelpers.CreateScope("pipeline build"))
            {
                logger.LogInformation("Still building pipeline...");
            }

            // Register a callback to run after the app is fully configured
            lifetime.ApplicationStarted.Register(() =>
            {
                ServerAddress = app.ServerFeatures.Get<IServerAddressesFeature>().Addresses.First();
                AppListening = true;
            });

            app.Use((httpContext, next) =>
            {
                logger.ConditionalLog($"Visited {httpContext.Request.Path}");
                return next();
            });

            app.Run(context =>
            {
                logger.ConditionalLog("Received request, echoing");

                using var scope = SampleHelpers.CreateScope("middleware execution");
                logger.LogInformation("Sending response");
                return context.Response.WriteAsync("PONG");
            });
        }
    }
}
