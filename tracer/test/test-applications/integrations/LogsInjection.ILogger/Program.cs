using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace LogsInjection.ILogger
{
    public class Program
    {
        public static void Main(string[] args)
        {
#pragma warning disable ASPDEPR008 // Type or member is obsolete
            CreateHostBuilder(args).Build().Run();
#pragma warning restore ASPDEPR008 // Type or member is obsolete
        }

#pragma warning disable ASPDEPR008 // Type or member is obsolete
        public static IWebHostBuilder CreateHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                   .UseContentRoot(AppContext.BaseDirectory)
                   .UseSetting(WebHostDefaults.SuppressStatusMessagesKey, "True")
                   .ConfigureLogging(LogHelper.ConfigureCustomLogging)
                   .UseStartup<Startup>();
#pragma warning restore ASPDEPR008 // Type or member is obsolete
    }
}
