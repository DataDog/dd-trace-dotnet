using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace Samples.Yarp.DistributedTracing
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddSingleton<IProxyConfigProvider>(new CodeProxyConfigProvider())
                .AddReverseProxy();

            services.AddHostedService<Worker>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapReverseProxy();
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });
            });
        }

        internal RouteConfig[] GetRoutes() =>
            [
                new RouteConfig()
                {
                    RouteId = "route1",
                    ClusterId = "cluster1",
                    Match = new RouteMatch
                    {
                        // Path or Hosts are required for each route. This catch-all pattern matches all request paths.
                        Path = "{**catch-all}"
                    }
                }
            ];

        internal ClusterConfig[] GetClusters(string destinationUri)
        {
            return
            [
                new ClusterConfig()
                {
                    ClusterId = "cluster1",
                    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "destination1", new DestinationConfig() { Address = destinationUri } },
                    }
                }
            ];
        }
    }
}
