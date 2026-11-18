// <copyright file="AwsLambdaSdkCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.LambdaSdk;

internal static class AwsLambdaSdkCommon
{
    internal const string IntegrationName = nameof(IntegrationId.AwsLambdaSdk);
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsLambdaSdkCommon));

    internal static Scope? CreateScope(Tracer tracer, IInvokeRequest request)
    {
        var settings = tracer.CurrentTraceSettings;
        if (request.Instance is null
         || (request.InvocationType is { } invocationType && invocationType.Value != "RequestResponse")
         || !settings.Settings.IsIntegrationEnabled(IntegrationId.AwsLambdaSdk)
         || !settings.Settings.IsIntegrationEnabled(AwsConstants.IntegrationId))
        {
            return null;
        }

        Scope? scope = null;
        try
        {
            var tags = new AwsLambdaSdkTags { FunctionName = request.FunctionName, Service = "Lambda", Operation = "Invoke" };
            var (serviceName, serviceNameSource) = settings.GetServiceNameMetadata("aws-lambda");
            var operationName = settings.Schema.Version == SchemaVersion.V0 ? "lambda.request" : "aws.lambda.invoke";
            scope = tracer.StartActiveInternal(operationName, tags: tags, serviceName: serviceName, serviceNameSource: serviceNameSource);
            scope.Span.Type = SpanTypes.Http;
            scope.Span.ResourceName = $"Lambda.Invoke {request.FunctionName}";
            tags.SetAnalyticsSampleRate(IntegrationId.AwsLambdaSdk, settings.Settings, enabledWithGlobalSetting: false);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AwsLambdaSdk);

            ContextPropagation.Inject(tracer, request, scope.Span.Context);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating Lambda Invoke scope.");
        }

        return scope;
    }
}
