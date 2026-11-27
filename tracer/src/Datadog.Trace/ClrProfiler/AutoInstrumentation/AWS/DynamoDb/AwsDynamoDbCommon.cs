// <copyright file="AwsDynamoDbCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.DynamoDb
{
    internal static class AwsDynamoDbCommon
    {
        private const string DatadogAwsDynamoDbServiceName = "aws-dynamodb";
        private const string DynamoDbServiceName = "DynamoDB";
        private const string DynamoDbOperationName = "aws.dynamodb.request";
        private const string TableResourcePrefix = "table/";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsDynamoDbCommon));

        internal const string IntegrationName = nameof(Configuration.IntegrationId.AwsDynamoDb);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.AwsDynamoDb;

        public static Scope? CreateScope(Tracer tracer, string operation, out AwsDynamoDbTags? tags, ISpanContext? parentContext = null)
        {
            tags = null;

            var perTraceSettings = tracer.CurrentTraceSettings;
            if (!perTraceSettings.Settings.IsIntegrationEnabled(IntegrationId) || !perTraceSettings.Settings.IsIntegrationEnabled(AwsConstants.IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope? scope = null;

            try
            {
                tags = perTraceSettings.Schema.Database.CreateAwsDynamoDbTags();
                var (serviceName, serviceNameSource) = perTraceSettings.GetServiceNameMetadata(DatadogAwsDynamoDbServiceName);
                scope = tracer.StartActiveInternal(DynamoDbOperationName, parent: parentContext, tags: tags, serviceName: serviceName, serviceNameSource: serviceNameSource);
                var span = scope.Span;

                // This is needed to showcase the DynamoDB action in the
                // span details. This will also cause to repeat an HTTP span.
                span.Type = SpanTypes.DynamoDb;
                span.ResourceName = $"{DynamoDbServiceName}.{operation}";

                tags.Service = DynamoDbServiceName;
                tags.Operation = operation;
                tags.SetAnalyticsSampleRate(IntegrationId, perTraceSettings.Settings, enabledWithGlobalSetting: false);
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

        public static void TagTableNameAndResourceName(string? tableName, AwsDynamoDbTags? tags, Scope? scope)
        {
            if (scope == null || tags == null || tableName == null)
            {
                return;
            }

            tags.TableName = tableName;
            tags.TableArn = GetTableArn(tableName);
            var span = scope.Span;
            span.ResourceName = $"{span.ResourceName} {tableName}";
        }

        /// <summary>
        /// Returns the table name if it is a full DynamoDB table ARN
        /// (`arn:partition:dynamodb:region:account:table/name`), otherwise null.
        /// DynamoDB accepts either a table name or a table ARN as `TableName`.
        /// </summary>
        public static string? GetTableArn(string? tableName)
        {
            // cheap check first: most requests use a plain table name
            if (tableName is null || !tableName.StartsWith("arn:", StringComparison.Ordinal))
            {
                return null;
            }

            // arn:<partition>:dynamodb:<region>:<account>:table/<name>
            var index = 0;
            foreach (var part in tableName.SplitIntoSpans(':', count: 6))
            {
                ReadOnlySpan<char> value = part;
                var valid = index switch
                {
                    0 => true, // "arn", checked above
                    2 => value.SequenceEqual("dynamodb".AsSpan()),
                    5 => IsTableResource(value),
                    _ => !value.IsEmpty, // partition, region, account
                };

                if (!valid)
                {
                    return null;
                }

                index++;
            }

            return index == 6 ? tableName : null;
        }

        // `table/<name>`, without a further path such as `/index/<index>` or `/stream/<label>`
        private static bool IsTableResource(ReadOnlySpan<char> resource)
        {
            if (!resource.StartsWith(TableResourcePrefix.AsSpan(), StringComparison.Ordinal))
            {
                return false;
            }

            var name = resource.Slice(TableResourcePrefix.Length);
            return !name.IsEmpty && name.IndexOf('/') < 0;
        }

        public static void TagBatchRequest<TBatchRequest>(TBatchRequest request, AwsDynamoDbTags? tags, Scope? scope)
            where TBatchRequest : IBatchRequest
        {
            if (request.RequestItems?.Count != 1)
            {
                return;
            }

            // TableName tagging only when batch is from one table.
            var iterator = request.RequestItems.GetEnumerator();
            using var disposable = iterator as IDisposable;
            while (iterator.MoveNext())
            {
                var tableName = iterator.Key as string;
                TagTableNameAndResourceName(tableName, tags, scope);
            }
        }

        public static string GetValueFromDynamoDbAttribute(IDynamoDbAttributeValue value)
        {
            if (value.S != null)
            {
                return value.S;
            }

            if (value.N != null)
            {
                return value.N;
            }

            if (value.B != null)
            {
#if NETCOREAPP3_1_OR_GREATER
                if (value.B.TryGetBuffer(out var buffer))
                {
                    return Encoding.UTF8.GetString(buffer.AsSpan());
                }
#endif
                // fallback always copies bytes into a new array
                return Encoding.UTF8.GetString(value.B.ToArray());
            }

            return string.Empty;
        }
    }
}
