using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Formatting.Json;

namespace MicrosoftExtensionsExample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices(services => services.AddHostedService<Worker>())
                .ConfigureLogging(logging => 
                {
                    // The JsonFormatter used by NetEscapades.Extensions.Logging.RollingFile includes all properties
                    // if scopes are enabled
                    //
                    // Additions to configuration:
                    // - used json format
                    // - enabled scopes
                    logging.AddFile(opts =>
                    {
                        opts.IncludeScopes = true; // must include scopes so that correlation identifiers are added
                        opts.FileName = "log-MicrosoftExtensions-jsonFile";
                        opts.Extension = "log";
                        opts.FormatterName = "json";
                    });

                    // Using Serilog with Microsoft.Extensions.Logging is supported, but uses the Serilog log injection
                    // not Microsoft.Extensions.Logging logs injection.
                    // See the SerilogExample project for examples of valid Serilog configurations and output formats
                    logging.AddSerilog(new LoggerConfiguration()
                        .Enrich.FromLogContext()
                        .WriteTo.File(new JsonFormatter(), "Logs/log-Serilog-jsonFile.log")
                        .CreateLogger());
                });
    }
}
