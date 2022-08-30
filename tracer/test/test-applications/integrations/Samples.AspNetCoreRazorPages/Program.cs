using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
#if !NETCOREAPP2_1
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
#endif

namespace Samples.AspNetCoreRazorPages
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

#if NETCOREAPP2_1
        public static IWebHostBuilder CreateHostBuilder(string[] args) =>
            // based on https://github.com/dotnet/aspnetcore/blob/main/src/DefaultBuilder/src/WebHost.cs#L154
            // simplified to remove file reloading etc
            WebHost.CreateDefaultBuilder()
                   .ConfigureAppConfiguration(
                        config =>
                        {
                            config.Sources.Clear();
                            config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                                  .AddEnvironmentVariables()
                                  .AddCommandLine(args);
                        })
                   .UseStartup<Startup>();
#else
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(
                     webBuilder =>
                     {
                         webBuilder.UseStartup<Startup>();
                     });
#endif

    }
}
