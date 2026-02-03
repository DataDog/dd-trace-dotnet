using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Collections.Generic;

namespace Samples.Ocelot.DistributedTracing
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // Create a minimal in-memory configuration for Ocelot startup
            // The actual routes will be configured at runtime by the Worker
            var ocelotConfig = new Dictionary<string, string>
            {
                { "Routes", "[]" },
                { "GlobalConfiguration:BaseUrl", "http://localhost:5000" }
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(ocelotConfig)
                .Build();

            services.AddOcelot(configuration);
            services.AddHostedService<Worker>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });
            });

            app.UseOcelot().Wait();
        }
    }
}
