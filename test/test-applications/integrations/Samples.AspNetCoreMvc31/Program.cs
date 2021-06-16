using System;
using System.Diagnostics;
using Karambolo.Extensions.Logging.File;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Samples.AspNetCoreMvc
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                       .UseContentRoot(AppContext.BaseDirectory)
                       .ConfigureLogging(LogHelpers.ConfigureCustomLogging)
                        .UseStartup<Startup>();
                });
    }
}
