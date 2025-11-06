// <copyright file="RequestInvokerHandlerSendAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb;

/// <summary>
/// System.Threading.Tasks.Task`1[Microsoft.Azure.Cosmos.ResponseMessage] Microsoft.Azure.Cosmos.Handlers.RequestInvokerHandler::SendAsync(System.String,Microsoft.Azure.Documents.ResourceType,Microsoft.Azure.Documents.OperationType,Microsoft.Azure.Cosmos.RequestOptions,Microsoft.Azure.Cosmos.ContainerInternal,Microsoft.Azure.Cosmos.FeedRange,System.IO.Stream,System.Action`1[Microsoft.Azure.Cosmos.RequestMessage],Microsoft.Azure.Cosmos.Tracing.ITrace,System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.Azure.Cosmos.Client",
    TypeName = "Microsoft.Azure.Cosmos.Handlers.RequestInvokerHandler",
    MethodName = "SendAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.Azure.Cosmos.ResponseMessage]",
    ParameterTypeNames = [ClrNames.String, "Microsoft.Azure.Documents.ResourceType", "Microsoft.Azure.Documents.OperationType", "Microsoft.Azure.Cosmos.RequestOptions", "Microsoft.Azure.Cosmos.ContainerInternal", "Microsoft.Azure.Cosmos.FeedRange", ClrNames.Stream, "System.Action`1[Microsoft.Azure.Cosmos.RequestMessage]", "Microsoft.Azure.Cosmos.Tracing.ITrace", ClrNames.CancellationToken],
    MinimumVersion = "3.18.0",
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(IntegrationId.CosmosDb))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class RequestInvokerHandlerSendAsyncIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RequestInvokerHandlerSendAsyncIntegration));

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref string? resourceUriString, ref object resourceType, ref object operationType, ref object? requestOptions, ref object? cosmosContainerCore, ref object? feedRange, ref Stream? streamPayload, ref object? requestEnricher, ref object? trace, ref CancellationToken cancellationToken)
    {
        Log.Debug("RequestInvokerHandler.SendAsync instrumentation triggered");

        var tracer = Tracer.Instance;
        var perTraceSettings = tracer.CurrentTraceSettings;

        // if (!perTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.CosmosDb))
        // {
        //     return CallTargetState.GetDefault();
        // }

        try
        {
            // var parent = tracer.ActiveScope?.Span;

            // if (parent != null &&
            //     parent.Type == SpanTypes.Sql &&
            //     parent.GetTag(Tags.DbType) == "cosmosdb")
            // {
            //     // We're already instrumenting this from a parent span
            //     return CallTargetState.GetDefault();
            // }

            var operationName = perTraceSettings.Schema.Database.GetOperationName("cosmosdb");
            var serviceName = perTraceSettings.Schema.Database.GetServiceName("cosmosdb");
            var tags = perTraceSettings.Schema.Database.CreateCosmosDbTags();

            tags.SetAnalyticsSampleRate(IntegrationId.CosmosDb, perTraceSettings.Settings, enabledWithGlobalSetting: false);
            perTraceSettings.Schema.RemapPeerService(tags);

            var scope = tracer.StartActiveInternal(operationName, tags: tags, serviceName: serviceName);
            var span = scope.Span;

            span.Type = SpanTypes.Sql;
            // span.ResourceName = StringUtil.IsNullOrEmpty(containerName)
            //     ? $"{operationType}"
            //     : $"{operationType} {containerName}";

            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.CosmosDb);

            return new CallTargetState(scope);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating or populating scope for CosmosDb RunWithDiagnosticsHelperAsync.");
        }

        return CallTargetState.GetDefault();
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(
        TTarget instance,
        TReturn returnValue,
        Exception exception,
        in CallTargetState state)
    {
        state.Scope?.DisposeWithException(exception);
        return returnValue;
    }
}
