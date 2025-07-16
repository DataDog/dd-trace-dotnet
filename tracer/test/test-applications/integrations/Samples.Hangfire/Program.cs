using System;
using System.Net.Http;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

#if NET5_0_OR_GREATER
using Microsoft.Extensions.Hosting; // generic host
#endif

namespace Samples.Hangfire
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
#if NET5_0_OR_GREATER
            using var host = CreateGenericHost(args).Build();
#elif NETCOREAPP
            using var host = CreateWebHost(args);
#endif
            await host.StartAsync();

            await Should_Create_Distributed_Trace();
            await Should_Create_Span();
            await Should_Create_Span_With_Status_Error_When_Job_Failed();

            Console.WriteLine("App started. Press Enter to exit...");
            Console.ReadLine();
        }

        // --------------------------------------------------
        // Demo jobs & helpers
        // --------------------------------------------------

        public static void ExecuteTracedJob(string msg) =>
            Console.WriteLine($"Hello from the Hangfire job! {msg}");

        public static async Task Should_Create_Span()
        {
            BackgroundJob.Enqueue<TestJob>(x => x.Execute());
            await Task.Delay(1000);
        }

        public static async Task Should_Create_Span_With_Status_Error_When_Job_Failed()
        {
            BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());
            await Task.Delay(1000);
        }

        public static async Task Should_Create_Distributed_Trace()
        {
            using var client = new HttpClient();
            var r = await client.GetAsync("http://localhost:5000/trace");
            Console.WriteLine(await r.Content.ReadAsStringAsync());
            await Task.Delay(1000);
        }

        // --------------------------------------------------
        // Host builders
        // --------------------------------------------------

#if NET5_0_OR_GREATER
        private static IHostBuilder CreateGenericHost(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(wb =>
                {
                    wb.UseUrls("http://localhost:5000");
                    ConfigureCommon(wb);
                });
#endif

#if NETCOREAPP   // netcoreapp2.1 branch
        private static IWebHost CreateWebHost(string[] args)
        {
            var builder = WebHost.CreateDefaultBuilder(args)
                                 .UseUrls("http://localhost:5000");
            ConfigureCommon(builder);
            return builder.Build();
        }
#endif

        // --------------------------------------------------
        // Shared service & middleware setup
        // --------------------------------------------------

        private static void ConfigureCommon(IWebHostBuilder wb)
        {
            wb.ConfigureServices(services =>
            {
                services.AddHangfire(cfg =>
                {
                    cfg.UseMemoryStorage();
                    cfg.UseFilter(new AutomaticRetryAttribute { Attempts = 0 });
                });
                services.AddHangfireServer();

#if NET5_0_OR_GREATER
                services.AddControllers();
#else
                services.AddMvc();
#endif
            });

            wb.Configure(app =>
            {
#if NET5_0_OR_GREATER
                app.UseRouting();
                app.UseEndpoints(e => e.MapControllers());
#else
                app.UseMvc();
#endif
            });
        }
    }

    // --------------------------------------------------
    // Controllers (per-TFM)
    // --------------------------------------------------

#if NET5_0_OR_GREATER
    [ApiController]
    [Route("/")]
    public class TraceController : ControllerBase
    {
        [HttpGet("trace")]
        public IActionResult Trace()
        {
            BackgroundJob.Enqueue(() => Program.ExecuteTracedJob("from distributed trace"));
            return Content("Job enqueued");
        }
    }
#else   // netcoreapp2.1
    [Route("")]
    public class TraceController : Controller
    {
        [HttpGet("trace")]
        public IActionResult Trace()
        {
            BackgroundJob.Enqueue(() => Program.ExecuteTracedJob("from distributed trace"));
            return Content("Job enqueued");
        }
    }
#endif
}
