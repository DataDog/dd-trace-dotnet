using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Samples.Security.AspNetCore5.Endpoints
{
    public static class Endpoints
    {
        public static void RegisterEndpointsRouting(this IEndpointRouteBuilder routeBuilder)
        {
            routeBuilder.MapGet("/params-endpoint/{s}", context =>
            {
                var routeValues = context.GetRouteData().Values;
                var s = routeValues["s"] as string;
                return context.Response.WriteAsync($"Hello world {s}!");
            });
        }
    }
}
