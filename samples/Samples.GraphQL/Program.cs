using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Datadog.Trace.ClrProfiler;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;

namespace Samples.GraphQL
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var directory = Directory.GetCurrentDirectory();

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(directory)
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation($"Instrumentation.ProfilerAttached = {Instrumentation.ProfilerAttached}");

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
    }
}
