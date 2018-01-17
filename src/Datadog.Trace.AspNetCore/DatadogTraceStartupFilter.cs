using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Datadog.Trace.AspNetCore
{
    public class DatadogTraceStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                // Initialize tracer
                next(builder);
            };
        }
    }
}
