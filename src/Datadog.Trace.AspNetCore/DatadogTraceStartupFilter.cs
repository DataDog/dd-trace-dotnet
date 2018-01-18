using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Datadog.Trace.AspNetCore
{
    internal class DatadogTraceStartupFilter : IStartupFilter
    {
        private AspNetCoreListener _listener;

        public DatadogTraceStartupFilter(AspNetCoreListener listener)
        {
            _listener = listener;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                _listener.Listen();
                next(builder);
            };
        }
    }
}
