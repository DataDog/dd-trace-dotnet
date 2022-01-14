using Datadog.Trace.TestHelpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Samples.Transport.UnixDomainSocket
{
    public class Program
    {
        public static MockTracerAgent MockAgent;

        public static void Main(string[] args)
        {
            SetMockAgent();
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        private static void SetMockAgent()
        {
            MockAgent = new MockTracerAgent(
                new UnixDomainSocketConfig(
                    "/var/run/datadog/mockapm.socket",
                    "/var/run/datadog/mockdsd.socket")
                );
        }
    }
}
