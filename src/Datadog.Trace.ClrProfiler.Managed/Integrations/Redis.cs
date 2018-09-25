namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal static class Redis
    {
        internal const string OperationName = "redis.command";
        internal const string ServiceName = "redis";

        internal static Scope CreateScope(string host, string port, string rawCommand, bool finishOnClose = true)
        {
            Tracer tracer = Tracer.Instance;
            string serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);

            var scope = tracer.StartActive(OperationName, serviceName: serviceName, finishOnClose: finishOnClose);
            var command = rawCommand;
            if (command.Contains(" "))
            {
                command = command.Substring(0, command.IndexOf(' '));
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
