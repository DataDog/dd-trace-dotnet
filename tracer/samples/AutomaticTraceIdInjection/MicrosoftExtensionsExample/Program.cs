using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureServices(services => services.AddHostedService<Worker>())
#if NETCOREAPP3_0_OR_GREATER
                .ConfigureLogging(logging =>
                {
                    // The JsonFormatter used by NetEscapades.Extensions.Logging.RollingFile includes all properties
                    // if scopes are enabled - however, it requires .NET 3.0+
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
                });
#else
                .ConfigureLogging(logging =>
                {
                    // Using Serilog with Microsoft.Extensions.Logging is supported, but uses the Serilog log injection
                    // not Microsoft.Extensions.Logging logs injection.
                    // See the SerilogExample project for examples of valid Serilog configurations and output formats
                    logging.AddSerilog(new LoggerConfiguration()
                        .Enrich.FromLogContext()
                        .WriteTo.File(new JsonFormatter(), "Logs/log-Serilog-jsonFile.log")
                        .CreateLogger());
                });
#endif
        }
    }
}
