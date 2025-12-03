// <copyright file="AwsS3Common.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3
{
    internal static class AwsS3Common
    {
        private const string DatadogAwsS3ServiceName = "aws-s3";
        private const string S3ServiceName = "S3";
        private const string S3OperationName = "aws.s3.request";
        private const string S3OperationNameV0 = "s3.request";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsS3Common));

        internal const string IntegrationName = nameof(IntegrationId.AwsS3);
        private const IntegrationId IntegrationId = Configuration.IntegrationId.AwsS3;

        public static Scope? CreateScope(Tracer tracer, string operation, out AwsS3Tags? tags, string spanKind = SpanKinds.Client, ISpanContext? parentContext = null)
        {
            tags = null;

            var perTraceSettings = tracer.CurrentTraceSettings;
            if (!perTraceSettings.Settings.IsIntegrationEnabled(IntegrationId) || !perTraceSettings.Settings.IsIntegrationEnabled(AwsConstants.IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope? scope = null;
            // try
            // {
            //     tags = perTraceSettings.Schema.Messaging.CreateAwsS3Tags(spanKind);
            //     var serviceName = perTraceSettings.GetServiceName(DatadogAwsS3ServiceName);
            //     var operationName = GetOperationName(tracer);
            //     scope = tracer.StartActiveInternal(operationName, parent: parentContext, tags: tags, serviceName: serviceName);
            //     var span = scope.Span;
            //
            //     span.Type = SpanTypes.Http;
            //     span.ResourceName = $"{S3ServiceName}.{operation}";
            //
            //     tags.Service = S3ServiceName;
            //     tags.Operation = operation;
            //     tags.SetAnalyticsSampleRate(IntegrationId, perTraceSettings.Settings, enabledWithGlobalSetting: false);
            //     tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            // }
            // catch (Exception ex)
            // {
            //     Log.Error(ex, "Error creating or populating scope.");
            // }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return scope;
        }

        internal static string GetOperationName(Tracer tracer)
        {
            return tracer.CurrentTraceSettings.Schema.Version == SchemaVersion.V0
                   ? S3OperationNameV0
                   : S3OperationName;
        }

        public static void SetTags(AwsS3Tags? tags, string? bucketName, string? objectKey)
        {
            if (tags == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(bucketName))
            {
                tags.BucketName = bucketName;
            }

            if (!string.IsNullOrEmpty(objectKey))
            {
                tags.ObjectKey = objectKey;
            }
        }
    }
}
