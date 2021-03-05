#if !NETFRAMEWORK
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
#if !NETCOREAPP2_1
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
#endif
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name should match first type

namespace Datadog.Trace.Tests.DiagnosticListeners
{
    /// <summary>
    /// A Startup file for testing MVC (without endpoint-routing enabled)
    /// </summary>
    public class MvcStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
#if NETCOREAPP2_1
            services.AddMvc();
#else
            services.AddMvc(options => options.EnableEndpointRouting = false);
#endif
        }

        public void Configure(IApplicationBuilder builder)
        {
            builder.UseMvc(routes =>
            {
                routes.MapRoute("custom", "Test/{action=Index}", new { Controller = "MyTest" });
                routes.MapRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }

#if !NETCOREAPP2_1

    /// <summary>
    /// A Startup file for testing endpoint-routing
    /// </summary>
    public class EndpointRoutingStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddHealthChecks();
            services.AddAuthorization();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
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

        public void Error()
        {
            throw new Exception();
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
        public string SetStatusCode(int value) => value.ToString();
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
