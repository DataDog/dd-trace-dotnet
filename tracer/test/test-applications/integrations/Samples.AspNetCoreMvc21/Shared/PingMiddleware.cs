using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Samples.AspNetCoreMvc.Shared
{
    public class PingMiddleware
    {
        private readonly RequestDelegate _next;

        public PingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public Task Invoke(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/ping"))
            {
                return context.Response.WriteAsync("pong");
            }

            return _next(context);
        }
    }
}
