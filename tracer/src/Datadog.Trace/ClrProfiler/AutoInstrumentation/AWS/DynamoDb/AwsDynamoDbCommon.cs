// <copyright file="AwsDynamoDbCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.DynamoDb
{
    internal static class AwsDynamoDbCommon
    {
        private const string DatadogAwsDynamoDbServiceName = "aws-dynamodb";
        private const string DynamoDbServiceName = "DynamoDB";
        private const string DynamoDbOperationName = "aws.dynamodb.request";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsDynamoDbCommon));

        internal const string IntegrationName = nameof(Configuration.IntegrationId.AwsDynamoDb);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.AwsDynamoDb;

        public static Scope CreateScope(Tracer tracer, string operation, out AwsDynamoDbTags tags, ISpanContext parentContext = null)
        {
            tags = null;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId) || !tracer.Settings.IsIntegrationEnabled(AwsConstants.IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                tags = tracer.CurrentTraceSettings.Schema.Database.CreateAwsDynamoDbTags();
                var serviceName = tracer.CurrentTraceSettings.GetServiceName(tracer, DatadogAwsDynamoDbServiceName);
                scope = tracer.StartActiveInternal(DynamoDbOperationName, parent: parentContext, tags: tags, serviceName: serviceName);
                var span = scope.Span;

                // This is needed to showcase the DynamoDB action in the
                // span details. This will also cause to repeat an HTTP span.
                span.Type = SpanTypes.DynamoDb;
                span.ResourceName = $"{DynamoDbServiceName}.{operation}";

                tags.Service = DynamoDbServiceName;
                tags.Operation = operation;
                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return scope;
        }

        public static void TagTableNameAndResourceName(string tableName, AwsDynamoDbTags tags, Scope scope)
        {
            if (scope == null || tags == null || tableName == null)
            {
                return;
            }

            tags.TableName = tableName;
            var span = scope.Span;
            span.ResourceName = $"{span.ResourceName} {tableName}";
        }

        public static void TagBatchRequest<TBatchRequest>(TBatchRequest request, AwsDynamoDbTags tags, Scope scope)
            where TBatchRequest : IBatchRequest
        {
            if (request.RequestItems.Count != 1)
            {
                return;
            }

            // TableName tagging only when batch is from one table.
            var iterator = request.RequestItems.GetEnumerator();
            while (iterator.MoveNext())
            {
                var tableName = iterator.Key as string;
                TagTableNameAndResourceName(tableName, tags, scope);
            }
        }
    }
}
