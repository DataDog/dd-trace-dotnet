using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Threading.Tasks;

namespace Samples.HotChocolate
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddLogging(builder => builder.AddConsole());

            services.AddRouting();
            services.AddGraphQLServer().AddQueryType<Query>();
            services.AddGraphQLServer().AddMutationType<Mutation>();
        }

        public void Configure(IApplicationBuilder app,
#if NETCOREAPP2_1 || NET461
                              IHostingEnvironment env,
#else
                              IWebHostEnvironment env,
#endif
                              ILoggerFactory loggerFactory)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGraphQL();
            });            
            
            app.UseDeveloperExceptionPage();
            app.UseWelcomePage("/alive-check");

            app.Map("/shutdown", builder =>
            {
                builder.Run(async context =>
                {
                    await context.Response.WriteAsync("Shutting down");

#pragma warning disable CS0618 // Type or member is obsolete
                    _ = Task.Run(() => builder.ApplicationServices.GetService<IApplicationLifetime>().StopApplication());
#pragma warning restore CS0618 // Type or member is obsolete
                });
            });


        }
    }
}
