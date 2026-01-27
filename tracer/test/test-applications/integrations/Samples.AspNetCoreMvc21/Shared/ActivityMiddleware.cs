using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Samples.AspNetCoreMvc.Shared
{
    public class ActivityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public ActivityMiddleware(RequestDelegate next, ILogger<ActivityMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            if (System.Diagnostics.Activity.Current is { } activity1)
            {
                _logger.LogInformation("Setting pre_invoke tag on activity {Name}", activity1.OperationName);
                activity1.AddTag("pre_invoke", "value1");
            }

            try
            {
                await _next(context);
            }
            finally
            {
                if (System.Diagnostics.Activity.Current is { } activity2)
                {
                    _logger.LogInformation("Setting post_invoke tag on activity {Name}", activity2.OperationName);
                    activity2.AddTag("post_invoke", "value2");
                }
            }
        }
    }
}
