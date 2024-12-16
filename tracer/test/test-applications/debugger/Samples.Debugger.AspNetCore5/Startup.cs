using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Samples.Debugger.AspNetCore5
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<IStartupFilter, CustomStartupFilter>();
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseMiddleware<FirstLastMiddleware>();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // todo, for now disable to test that the block exception isn't caught by the call target integrations. 
                // app.UseExceptionHandler("/Home/Error");
            }

            app.UseRouting();
            app.Map(
                "/alive-check",
                builder =>
                {
                    builder.Run(
                        async context =>
                        {
                            await context.Response.WriteAsync("Yes");
                        });
                });

            app.Map(
                "/shutdown",
                builder =>
                {
                    builder.Run(
                        async context =>
                        {
                            await context.Response.WriteAsync("Shutting down");
                            _ = Task.Run(() => builder.ApplicationServices.GetService<IHostApplicationLifetime>().StopApplication());
                        });
                });

            app.Use(async (context, next) =>
            {
                await next();
            });


            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }


    public class FirstLastMiddleware
    {
        private readonly RequestDelegate _next;

        public FirstLastMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Code to run before the next middleware in the pipeline
                Console.WriteLine("FirstLastMiddleware: Entering request pipeline");

                // Call the next middleware in the pipeline
                await _next(context);

                // Code to run after the next middleware in the pipeline
                Console.WriteLine("FirstLastMiddleware: Exiting request pipeline normally");
            }
            catch (Exception ex)
            {
                // Exception handling code
                Console.WriteLine($"FirstLastMiddleware caught an exception: {ex.Message}");
                throw; // Re-throw the exception to be handled by the global exception handler
            }
        }
    }

    public class CustomStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.UseMiddleware<FirstLastMiddleware>();
                next(app);
            };
        }
    }
}
