// <copyright file="AspNetCoreTestApp.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
#if !NETCOREAPP2_1
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
#endif
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name should match first type

namespace Datadog.Trace.IntegrationTests.DiagnosticListeners
{
    /// <summary>
    /// A Startup file for testing RazorPages (without endpoint-routing enabled)
    /// </summary>
    public class RazorPagesStartup
    {
        public RazorPagesStartup(IConfiguration configuration)
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
            services.AddRazorPages(c => c.RootDirectory = "/AspNetCoreRazorPages");
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

    /// <summary>
    /// A Startup file for testing MVC (without endpoint-routing enabled)
    /// </summary>
    public class MvcStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services
               .AddMvc()
#if !NETCOREAPP2_1
               .AddMvcOptions(options => options.EnableEndpointRouting = false)
#endif
               .ConfigureApplicationPartManager(partManager =>
                {
                    // Ensure we only load controllers from this assembly
                    // (don't accidentally load Razor Pages etc)
                    var thisAssembly = typeof(MvcStartup).Assembly;
                    var assemblyPart = new AssemblyPart(thisAssembly);
                    partManager.ApplicationParts.Clear();
                    partManager.ApplicationParts.Add(assemblyPart);
                });
        }

        public void Configure(IApplicationBuilder builder, IConfiguration configuration)
        {
            builder.UseMultipleErrorHandlerPipelines(app =>
            {
                MapExtensions.Map(
                    app,
                    "/throws",
                    inner =>
                        RunExtensions.Run(
                            inner,
                            async ctx =>
                            {
                                await Task.Yield();
                                throw new Exception("Map exception");
                            }));

                MvcApplicationBuilderExtensions.UseMvc(
                    app,
                    routes =>
                    {
                        MapRouteRouteBuilderExtensions.MapRoute(routes, "custom", "Test/{action=Index}", new { Controller = "MyTest" });
                        MapRouteRouteBuilderExtensions.MapRoute(routes, "default", "{controller=Home}/{action=Index}/{id?}");
                    });
            });
        }
    }

#if !NETCOREAPP2_1

    /// <summary>
    /// A Startup file for testing endpoint-routing
    /// </summary>
    public class EndpointRoutingStartup
    {
        public static void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapControllers();
            endpoints.MapDefaultControllerRoute();
            endpoints.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false })
                     .WithDisplayName("Custom Health Check");
            endpoints.MapGet(
                "/echo/{value:int?}",
                context =>
                {
                    var value = context.GetRouteValue("value")?.ToString();
                    return context.Response.WriteAsync(value ?? "No value");
                });
            endpoints.MapGet(
                "/throws",
                async ctx =>
                {
                    await Task.Yield();
                    throw new Exception("Endpoint exception");
                });
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers()
                    .ConfigureApplicationPartManager(partManager =>
                     {
                         // Ensure we only load controllers from this assembly
                         // (don't accidentally load Razor Pages etc)
                         var thisAssembly = typeof(MvcStartup).Assembly;
                         var assemblyPart = new AssemblyPart(thisAssembly);
                         partManager.ApplicationParts.Clear();
                         partManager.ApplicationParts.Add(assemblyPart);
                     });
            services.AddHealthChecks();
            services.AddAuthorization();
        }

        // Used in ASP.NET Core 3.x/5
        public void Configure(IApplicationBuilder builder, IConfiguration configuration)
        {
            builder.UseMultipleErrorHandlerPipelines(app =>
            {
                app.UseRouting();
                app.UseAuthorization();

                app.UseEndpoints(ConfigureEndpoints);
            });
        }
    }
#endif

    /// <summary>
    /// Simple controller used for the aspnetcore test
    /// </summary>
    public class HomeController : Controller
    {
        public async Task<string> Index()
        {
            await Task.Yield();
            return "Hello world";
        }

        public string Echo(string id) => id ?? "Default value";

        public void Error()
        {
            throw new Exception();
        }

        public void UncaughtError()
        {
            throw new InvalidOperationException();
        }

        public void BadHttpRequest()
        {
            ErrorHandlingHelper.ThrowBadHttpRequestException();
        }
    }

    /// <summary>
    /// Simple controller used for the aspnetcore test
    /// </summary>
    public class MyTestController : Controller
    {
        public async Task<string> Index()
        {
            await Task.Yield();
            return "Hello world";
        }

        [HttpGet("/statuscode/{value=200}")]
        public ObjectResult SetStatusCode(int value)
            => StatusCode(value, value.ToString());
    }

    /// <summary>
    /// Simple API controller used for the aspnetcore tests
    /// </summary>
    [ApiController]
    [Route("api/[action]")]
    public class ApiController : Controller
    {
        [HttpGet]
        public async Task<string> Index()
        {
            await Task.Yield();
            return "Hello world";
        }

        [HttpGet("{value}")]
        public string Value([FromRoute] InputModel model) => model.Value.ToString();

        /// <summary>
        /// .NET Core 2.1 doesn't allow applying validation parameters directly to method params,
        /// so need input model as holder
        /// </summary>
        public class InputModel
        {
            [Range(minimum: 1, maximum: 10)]
            public int Value { get; set; }
        }
    }
}
#endif
