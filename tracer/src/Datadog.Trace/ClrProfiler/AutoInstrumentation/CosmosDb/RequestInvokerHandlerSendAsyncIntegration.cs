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
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb;

/// <summary>
/// System.Threading.Tasks.Task`1[Microsoft.Azure.Cosmos.ResponseMessage] Microsoft.Azure.Cosmos.Handlers.RequestInvokerHandler::SendAsync calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.Azure.Cosmos.Client",
    TypeName = "Microsoft.Azure.Cosmos.Handlers.RequestInvokerHandler",
    MethodName = "SendAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.Azure.Cosmos.ResponseMessage]",
    ParameterTypeNames = [ClrNames.String, "Microsoft.Azure.Documents.ResourceType", "Microsoft.Azure.Documents.OperationType", "Microsoft.Azure.Cosmos.RequestOptions", "Microsoft.Azure.Cosmos.ContainerInternal", ClrNames.Ignore, ClrNames.Stream, "System.Action`1[Microsoft.Azure.Cosmos.RequestMessage]", ClrNames.Ignore, ClrNames.CancellationToken],
    MinimumVersion = "3.12.0",
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(IntegrationId.CosmosDb))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class RequestInvokerHandlerSendAsyncIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RequestInvokerHandlerSendAsyncIntegration));

    internal static CallTargetState OnMethodBegin<TTarget, TContainer>(TTarget instance, string resourceUriString, object resourceType, object operationType, object? requestOptions, TContainer cosmosContainerCore, object? feedRange, Stream? streamPayload, object? requestEnricher, object? trace, CancellationToken cancellationToken)
        where TContainer : IContainer, IDuckType
        where TTarget : IRequestInvokerHandlerProxy
    {
        var tracer = Tracer.Instance;
        var perTraceSettings = tracer.CurrentTraceSettings;

        if (!perTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.CosmosDb))
        {
            return CallTargetState.GetDefault();
        }

        try
        {
            // Skip what's already being traced by query tracing
            var operationTypeString = operationType?.ToString() ?? string.Empty;
            if (operationTypeString.Equals("Query", StringComparison.Ordinal) ||
                operationTypeString.Equals("QueryPlan", StringComparison.Ordinal))
            {
                return CallTargetState.GetDefault();
            }

            string? containerId = null;
            string? databaseId = null;

            if (cosmosContainerCore?.Instance != null)
            {
                containerId = cosmosContainerCore.Id;

                if (cosmosContainerCore.Database.TryDuckCast<DatabaseNewStruct>(out var databaseNew))
                {
                    databaseId = databaseNew.Id;
                }
                else if (cosmosContainerCore.Database.TryDuckCast<DatabaseOldStruct>(out var databaseOld))
                {
                    databaseId = databaseOld.Id;
                }
            }

            var operationName = perTraceSettings.Schema.Database.GetOperationName("cosmosdb");
            var serviceName = perTraceSettings.Schema.Database.GetServiceName("cosmosdb");
            var tags = perTraceSettings.Schema.Database.CreateCosmosDbTags();

            tags.ContainerId = containerId;
            tags.DatabaseId = databaseId;
            tags.SetEndpoint(instance?.Client.Endpoint);

            if (instance?.Client.ClientContext != null &&
                instance.Client.ClientContext.TryDuckCast<CosmosContextClientStruct>(out var clientContext))
            {
                tags.UserAgent = clientContext.UserAgent;

                var connectionMode = clientContext.ClientOptions.ConnectionMode;
                tags.ConnectionMode = connectionMode == 0 ? "gateway" : "direct";
            }

            perTraceSettings.Schema.RemapPeerService(tags);

            var scope = tracer.StartActiveInternal(operationName, tags: tags, serviceName: serviceName);
            var span = scope.Span;

            span.Type = SpanTypes.Sql;
            span.ResourceName = $"{operationTypeString} {resourceUriString}";

            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.CosmosDb);

            return new CallTargetState(scope);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating or populating scope for CosmosDb.");
        }

        return CallTargetState.GetDefault();
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        where TReturn : IResponseMessage
    {
        var scope = state.Scope;

        if (scope != null && returnValue.Instance is not null)
        {
            try
            {
                var tags = (CosmosDbTags)scope.Span.Tags;
                var statusCode = (int)returnValue.StatusCode;
                tags.ResponseStatusCode = statusCode.ToString();

                if (returnValue.Headers?.Instance != null)
                {
                    var subStatusCode = returnValue.Headers.SubStatusCodeLiteral;
                    if (!StringUtil.IsNullOrEmpty(subStatusCode))
                    {
                        tags.ResponseSubStatusCode = subStatusCode;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling CosmosDb response.");
            }
        }

        scope?.DisposeWithException(exception);
        return returnValue;
    }
}
