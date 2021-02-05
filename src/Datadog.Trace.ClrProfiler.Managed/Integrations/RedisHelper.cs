using System;
using Datadog.Trace.ClrProfiler.Integrations.StackExchange.Redis;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal static class RedisHelper
    {
        private const string OperationName = "redis.command";
        private const string ServiceName = "redis";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RedisHelper));

        internal static Scope CreateScope(Tracer tracer, IntegrationInfo integrationId, string host, string port, string rawCommand)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(integrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            string serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);
            Scope scope = null;

            try
            {
                var tags = new RedisTags();

                scope = tracer.StartActiveWithTags(OperationName, serviceName: serviceName, tags: tags);
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

                var span = scope.Span;
                span.Type = SpanTypes.Redis;
                span.ResourceName = command;
                tags.RawCommand = rawCommand;
                tags.Host = host;
                tags.Port = port;

                tags.SetAnalyticsSampleRate(integrationId, tracer.Settings, enabledWithGlobalSetting: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }
    }
}
