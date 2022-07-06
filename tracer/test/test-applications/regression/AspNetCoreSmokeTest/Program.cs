using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCoreSmokeTest
{
    public class Program
    {
        public static volatile int ExitCode = 0;
        public static async Task<int> Main(string[] args)
        {
            if (!IsProfilerAttached())
            {
                Console.WriteLine("Error: Profiler is required and is not loaded.");
                return 1;
            }
            
            Console.WriteLine("Process details: ");
            Console.WriteLine($"Framework: {RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"Process arch: {RuntimeInformation.ProcessArchitecture}");
            Console.WriteLine($"OS arch: {RuntimeInformation.OSArchitecture}");
            Console.WriteLine($"OS description: {RuntimeInformation.OSDescription}");

            await CreateHostBuilder(args).Build().RunAsync();

            return ExitCode;
        }

#if NETCOREAPP3_0_OR_GREATER
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices(ConfigureServices)
                .ConfigureWebHostDefaults(webBuilder => webBuilder.Configure(Configure));

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers()
                    .AddApplicationPart(typeof(ValuesController).Assembly);
            services.AddHostedService<Worker>();
        }

        private static void Configure(IApplicationBuilder app)
        {
            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
#else
        public static IWebHostBuilder CreateHostBuilder(string[] args) =>
            Microsoft.AspNetCore.WebHost.CreateDefaultBuilder(args)
                     .ConfigureServices(ConfigureServices)
                     .Configure(Configure);

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddHostedService<Worker>();
        }

        private static void Configure(IApplicationBuilder app)
        {
            app.UseMvc();
        }
#endif

        private static readonly Type NativeMethodsType = Type.GetType("Datadog.Trace.ClrProfiler.NativeMethods, Datadog.Trace");

        public static bool IsProfilerAttached()
        {
            if(NativeMethodsType is null)
            {
                return false;
            }

            try
            {
                MethodInfo profilerAttachedMethodInfo = NativeMethodsType.GetMethod("IsProfilerAttached");
                return (bool)profilerAttachedMethodInfo.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return false;
        }

        public static string GetTracerAssemblyLocation()
        {
            return NativeMethodsType?.Assembly.Location ?? "(none)";
        }
    }
}
