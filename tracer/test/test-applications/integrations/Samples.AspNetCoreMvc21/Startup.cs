using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Samples.AspNetCoreMvc.Shared;

namespace Samples.AspNetCoreMvc
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .Build();

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            var includeRouteEdgeCases = Environment.GetEnvironmentVariable("ADD_ROUTE_EDGE_CASES") == "1";
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            if (Environment.GetEnvironmentVariable("ADD_ACTIVITY_MIDDLEWARE") == "1")
            {
                app.UseMiddleware<ActivityMiddleware>();
            }

            app.UseMiddleware<PingMiddleware>();
            app.Map("/branch", x => x.UseMiddleware<PingMiddleware>());

            app.Map("/shutdown", builder =>
            {
                builder.Run(async context =>
                {
                    await context.Response.WriteAsync("Shutting down");
                    _ = Task.Run(() => builder.ApplicationServices.GetService<IApplicationLifetime>().StopApplication());
                });
            });

            if (includeRouteEdgeCases)
            {
                // Turns an error status into an error page by re-running the pipeline against a
                // different path. Scoped to /re-execute so that every other test case keeps the
                // default pipeline.
                app.UseWhen(
                    context => context.Request.Path.StartsWithSegments("/re-execute"),
                    branch => branch.UseStatusCodePagesWithReExecute("/status-code/{0}"));

                // Rewrites the path the way a URL-rewriting middleware would, so that routing runs
                // against a different path than the one the request arrived on.
                app.Use(async (context, next) =>
                {
                    if (context.Request.Path == "/rewrite-me")
                    {
                        context.Request.Path = "/alive-check";
                    }

                    await next();
                });
            }

            app.UseMvc(routes =>
            {
                if (includeRouteEdgeCases)
                {
                    // A route template that matches the application root, which ASP.NET Core stores
                    // as the empty string. Registered before the default route so that it is the one
                    // that matches "/".
                    routes.MapRoute(
                        name: "empty-route-template",
                        template: string.Empty,
                        defaults: new { controller = "Home", action = "Index" });
                }

                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
