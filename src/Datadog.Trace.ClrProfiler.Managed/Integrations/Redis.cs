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
            var command = rawCommand;
            if (command.Contains(" "))
            {
                command = command.Substring(0, command.IndexOf(' '));
            }

            scope.Span.Type = SpanTypes.Redis;
            scope.Span.ResourceName = command;
            scope.Span.SetTag(Tags.RedisRawCommand, rawCommand);
            scope.Span.SetTag(Tags.RedisHost, host);
            scope.Span.SetTag(Tags.RedisPort, port);

            return scope;
        }
    }
}
