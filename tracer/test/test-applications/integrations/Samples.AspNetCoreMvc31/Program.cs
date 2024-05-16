using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Samples.AspNetCoreMvc
{
    public class Program
    {
        public static void Main(string[] args)
        {
            while (!System.Diagnostics.Debugger.IsAttached)
            {
                System.Console.WriteLine("Waiting for debugger to attach...");
                System.Threading.Thread.Sleep(1000);
            }
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
