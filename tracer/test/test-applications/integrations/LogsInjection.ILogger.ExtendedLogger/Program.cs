using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace LogsInjection.ILogger
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                   .UseContentRoot(AppContext.BaseDirectory)
                   .UseSetting(WebHostDefaults.SuppressStatusMessagesKey, "True")
                   .ConfigureLogging((ctx, logging) =>
                    {
                        LogHelper.ConfigureCustomLogging(ctx, logging);
                        // Triggers using the ExtendedLoggerFactory
                        logging.EnableEnrichment();
                    })
                   .UseStartup<Startup>();

    }
}
