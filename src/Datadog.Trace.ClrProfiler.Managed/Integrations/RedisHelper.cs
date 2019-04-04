using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal static class RedisHelper
    {
        private const string OperationName = "redis.command";
        private const string ServiceName = "redis";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(RedisHelper));

        internal static Scope CreateScope(string host, string port, string rawCommand)
        {
            Tracer tracer = Tracer.Instance;
            string serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);
            Scope scope = null;

            try
            {
                scope = tracer.StartActive(OperationName, serviceName: serviceName);
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
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error creating or populating scope.", ex);
            }

            return scope;
        }
    }
}
