using System;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    internal static class AwsSqsCommon
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsSqsCommon));

        public static Scope CreateScope(Tracer tracer, string resourceName, out AwsSqsTags tags, ISpanContext parentContext = null)
        {
            tags = null;

            if (!tracer.Settings.IsIntegrationEnabled(AwsConstants.IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                Span parent = tracer.ActiveScope?.Span;

                tags = new AwsSqsTags();
                string serviceName = tracer.Settings.GetServiceName(tracer, AwsConstants.ServiceName);
                scope = tracer.StartActiveWithTags(AwsConstants.OperationName, parent: parentContext, tags: tags, serviceName: serviceName);
                var span = scope.Span;

                span.Type = SpanTypes.Http;
                span.ResourceName = resourceName;

                tags.SetAnalyticsSampleRate(AwsConstants.IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return scope;
        }
    }
}
