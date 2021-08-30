using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

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
                   .ConfigureLogging(LogHelper.ConfigureCustomLogging)
                   .UseStartup<Startup>();

    }
}
