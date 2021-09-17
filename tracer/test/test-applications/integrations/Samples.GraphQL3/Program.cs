using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Samples.GraphQL3
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var directory = Directory.GetCurrentDirectory();

            var host = new WebHostBuilder()
                .UseKestrel(serverOptions =>
                    // Explicitly set AllowSynchronousIO to true since the default changes
                    // between AspNetCore 2.0 and 3.0
                    serverOptions.AllowSynchronousIO = true
                )
                .UseContentRoot(directory)
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            logger.LogInformation($"Instrumentation.ProfilerAttached = {IsProfilerAttached()}");

            var prefixes = new[] { "COR_", "CORECLR_", "DD_", "DATADOG_" };
            var envVars = from envVar in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                          from prefix in prefixes
                          let key = (envVar.Key as string)?.ToUpperInvariant()
                          let value = envVar.Value as string
                          where key.StartsWith(prefix)
                          orderby key
                          select new KeyValuePair<string, string>(key, value);

            foreach (var kvp in envVars)
            {
                logger.LogInformation($"{kvp.Key} = {kvp.Value}");
            }

            host.Run();
        }

        private static bool IsProfilerAttached()
        {
            var instrumentationType = Type.GetType("Datadog.Trace.ClrProfiler.Instrumentation", throwOnError: false);

            if (instrumentationType == null)
            {
                return false;
            }

            var property = instrumentationType.GetProperty("ProfilerAttached");

            var isAttached = property?.GetValue(null) as bool?;

            return isAttached ?? false;
        }
    }
}
