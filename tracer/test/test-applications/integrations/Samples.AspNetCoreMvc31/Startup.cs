using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Samples.AspNetCoreMvc.Shared;

namespace Samples.AspNetCoreMvc
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
            services.AddControllersWithViews();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            var includeExtraMiddleware = Environment.GetEnvironmentVariable("ADD_EXTRA_MIDDLEWARE") == "1";
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
                    _ = Task.Run(() => builder.ApplicationServices.GetService<IHostApplicationLifetime>().StopApplication());
                });
            });

            if (includeExtraMiddleware)
            {
                app.UseMiddleware<CustomSpanMiddleware>("custom_pre_routing");
            }

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

            app.UseRouting();

            if (includeExtraMiddleware)
            {
                app.UseMiddleware<CustomSpanMiddleware>("custom_post_routing");
            }

            app.UseEndpoints(endpoints =>
            {
                if (includeRouteEdgeCases)
                {
                    // A route template that matches the application root, which ASP.NET Core stores
                    // as the empty string. Registered before the default route so that it is the one
                    // that matches "/".
                    endpoints.MapControllerRoute(
                        name: "empty-route-template",
                        pattern: string.Empty,
                        defaults: new { controller = "Home", action = "Index" });
                }

                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
