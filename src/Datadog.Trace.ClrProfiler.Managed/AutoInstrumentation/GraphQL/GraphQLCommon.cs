using System;
using System.Text;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL
{
    internal class GraphQLCommon
    {
        private const string ServiceName = "graphql";
        private const string ParseOperationName = "graphql.parse"; // Instrumentation not yet implemented
        private const string ValidateOperationName = "graphql.validate";
        private const string ExecuteOperationName = "graphql.execute";
        private const string ResolveOperationName = "graphql.resolve"; // Instrumentation not yet implemented

        internal const string IntegrationName = nameof(IntegrationIds.GraphQL);
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GraphQLCommon));

        internal static Scope CreateScopeFromValidate(Tracer tracer, IDocument document)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                var tags = new GraphQLTags();
                string serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);
                scope = tracer.StartActiveWithTags(ValidateOperationName, serviceName: serviceName, tags: tags);

                var span = scope.Span;
                span.Type = SpanTypes.GraphQL;
                tags.Source = document.OriginalQuery;

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static Scope CreateScopeFromExecuteAsync(Tracer tracer, IExecutionContext executionContext)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                string source = executionContext.Document.OriginalQuery;
                string operationName = executionContext.Operation.Name;
                string operationType = executionContext.Operation.OperationType.ToString();

                var tags = new GraphQLTags();
                string serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);
                scope = tracer.StartActiveWithTags(ExecuteOperationName, serviceName: serviceName, tags: tags);

                var span = scope.Span;
                span.Type = SpanTypes.GraphQL;
                span.ResourceName = $"{operationType} {operationName ?? "operation"}";

                tags.Source = source;
                tags.OperationName = operationName;
                tags.OperationType = operationType;

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static void RecordExecutionErrorsIfPresent(Span span, string errorType, IExecutionErrors executionErrors)
        {
            var errorCount = executionErrors.Count;

            if (errorCount > 0)
            {
                span.Error = true;

                span.SetTag(Trace.Tags.ErrorMsg, $"{errorCount} error(s)");
                span.SetTag(Trace.Tags.ErrorType, errorType);
                // span.SetTag(Trace.Tags.ErrorStack, ConstructErrorMessage(executionErrors)); // TODO: Implement. Hold off on creating the error message for now, I just want to see it work!
            }
        }
    }
}
