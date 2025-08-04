using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Samples.AspNetCoreMvc.Shared
{
    public class PingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public PingMiddleware(RequestDelegate next, ILogger<PingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public Task Invoke(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/ping"))
            {
                // force a sampling decision to be made, based on the current resource name etc
                _logger.LogInformation("Made sampling decision for ping request: {SamplingDecision}", SampleHelpers.GetOrMakeSamplingDecision());
                return context.Response.WriteAsync("pong");
            }

            return _next(context);
        }
    }
}
