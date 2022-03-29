using Datadog.Trace.IntegrationTests.DiagnosticListeners;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Samples.AspNetCoreRazorPages
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
#if NETCOREAPP2_1
            services.AddMvc();
#else
            services.AddRazorPages();
#endif
        }

        public void Configure(IApplicationBuilder builder)
        {
            builder.UseMultipleErrorHandlerPipelines(app =>
            {
#if NETCOREAPP2_1
                app.UseMvc();
#else
                app.UseRouting();

                app.UseAuthorization();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapRazorPages();
                });
#endif
            });
        }
    }
}
