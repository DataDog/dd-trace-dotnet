using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Samples.Security.AspNetCoreBare
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
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();

            app.UseAuthorization();

            app.Map("/alive-check", builder =>
            {
                builder.Run(async context =>
                {
                    await context.Response.WriteAsync("Yes");
                });
            });
            
            // Hello world HTML
            app.Map("/hello-world", builder =>
            {
                builder.Run(async context =>
                {
                    // Set text/html content type
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync("<!DOCTYPE html><html><head><title>Test</title></head><body><h1>Test</h1></body></html>");
                });
            });

            app.Map("/shutdown", builder =>
            {
                builder.Run(async context =>
                {
                    await context.Response.WriteAsync("Shutting down");
                    _ = Task.Run(() => builder.ApplicationServices.GetService<IHostApplicationLifetime>().StopApplication());
                });
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
