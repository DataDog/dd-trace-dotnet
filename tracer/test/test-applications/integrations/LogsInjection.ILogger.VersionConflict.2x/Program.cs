using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace LogsInjection.ILogger.VersionConflict_2x
{
    public class Program
    {
#pragma warning disable ASPDEPR008 // Type or member is obsolete
#pragma warning disable ASPDEPR004 // Type or member is obsolete
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
#pragma warning restore ASPDEPR008 // Type or member is obsolete
#pragma warning restore ASPDEPR004 // Type or member is obsolete
    }
}
