using Datadog.Trace.Attach;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Samples.AspNetCoreMvc
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AttachProfilerCore.LoadProfiler();
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
