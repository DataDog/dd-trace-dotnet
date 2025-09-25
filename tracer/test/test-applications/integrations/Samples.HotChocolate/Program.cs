using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using Microsoft.AspNetCore.Builder;

namespace Samples.HotChocolate
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var directory = Directory.GetCurrentDirectory();
#pragma warning disable ASPDEPR008 // Type or member is obsolete
#pragma warning disable ASPDEPR004 // Type or member is obsolete
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
#pragma warning restore ASPDEPR008 // Type or member is obsolete
#pragma warning restore ASPDEPR004 // Type or member is obsolete
            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            logger.LogInformation($"Instrumentation.ProfilerAttached = {SampleHelpers.IsProfilerAttached()}");

            var envVars = SampleHelpers.GetDatadogEnvironmentVariables();

            foreach (var kvp in envVars)
            {
                logger.LogInformation($"{kvp.Key} = {kvp.Value}");
            }

            host.Run();
        }
    }
}

