using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal static class Redis
    {
        internal const string OperationName = "redis.command";
        internal const string ServiceName = "redis";

        internal static Scope CreateScope(string host, string port, string rawCommand, bool finishOnClose = true)
        {
            var scope = Tracer.Instance.StartActive(OperationName, serviceName: ServiceName, finishOnClose: finishOnClose);
            int separatorIndex = rawCommand.IndexOf(' ');
            string command;

            if (separatorIndex >= 0)
            {
                command = rawCommand.Substring(0, separatorIndex);
            }
            else
            {
                command = rawCommand;
            }

            scope.Span.Type = SpanTypes.Redis;
            scope.Span.ResourceName = command;
            scope.Span.SetTag(Tags.RedisRawCommand, rawCommand);
            scope.Span.SetTag(Tags.OutHost, host);
            scope.Span.SetTag(Tags.OutPort, port);

            return scope;
        }
    }
}
