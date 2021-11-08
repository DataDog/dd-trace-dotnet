using System;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Samples.AzureFunctions.Version3.InProcess;
using Serilog;

[assembly: FunctionsStartup(typeof(Startup))]
namespace Samples.AzureFunctions.Version3.InProcess
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient();

            var apiKey = Environment.GetEnvironmentVariable("DD_API_KEY");
            var logsInjectionEnabled = Environment.GetEnvironmentVariable("DD_LOGS_INJECTION");

            if (!string.IsNullOrWhiteSpace(apiKey) && logsInjectionEnabled.Equals("1"))
            {
                builder.Services.AddLogging(
                    lb =>
                    {
                        lb.AddSerilog(new LoggerConfiguration()
                            .Enrich.FromLogContext()
                            .WriteTo.DatadogLogs(apiKey)
                            .CreateLogger());
                    });
            }
        }
    }
}
