// <copyright file="ServiceBusReceiverReceiveMessagesAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure_Messaging_ServiceBus;

/// <summary>
/// System.Threading.Tasks.Task`1[System.Collections.Generic.IReadOnlyList`1[Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]] Azure.Messaging.ServiceBus.ServiceBusReceiver::ReceiveMessagesAsync(System.Int32,System.Nullable`1[System.TimeSpan],System.Boolean,System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.ServiceBus",
    TypeName = "Azure.Messaging.ServiceBus.ServiceBusReceiver",
    MethodName = "ReceiveMessagesAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[System.Collections.Generic.IReadOnlyList`1[Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]]",
    ParameterTypeNames = [ClrNames.Int32, "System.Nullable`1[System.TimeSpan]", ClrNames.Bool, ClrNames.CancellationToken],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.AzureServiceBus))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ServiceBusReceiverReceiveMessagesAsyncIntegration
{
    private const string OperationName = "azure.servicebus.receive";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServiceBusReceiverReceiveMessagesAsyncIntegration));

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref int maxMessages, ref TimeSpan? maxWaitTime, ref bool isProcessor, ref CancellationToken cancellationToken)
    {
        Log.Information("ReceiveMessagesAsync running");

        // Log the full call stack to understand who's calling this
        var stackTrace = new StackTrace(true);
        var frames = stackTrace.GetFrames();

        Log.Information("=== FULL CALL STACK FOR ReceiveMessagesAsync ===");
        if (frames != null)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                var frame = frames[i];
                var method = frame?.GetMethod();
                var fileName = frame?.GetFileName();
                var lineNumber = frame?.GetFileLineNumber() ?? 0;

                if (method != null)
                {
                    var declaringType = method.DeclaringType?.FullName ?? "Unknown";
                    var methodName = method.Name;
                    var fullFrameInfo = $"  [{i}] {declaringType}.{methodName}";
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        fullFrameInfo += $" at {Path.GetFileName(fileName)}:{lineNumber}";
                    }

                    Log.Information("{FrameInfo}", fullFrameInfo);
                }
            }
        }

        Log.Information("=== END CALL STACK ===");

        var tracer = Tracer.Instance;
        var scope = tracer.StartActiveInternal(OperationName);
        var span = scope.Span;
        span.SetTag(Tags.SpanKind, SpanKinds.Consumer);
        span.SetTag("azure.servicebus.entity_path", "entity_path");
        span.SetTag("azure.servicebus.namespace", "namespace");
        span.SetTag("azure.servicebus.operation", "receive_batch");
        return new CallTargetState(scope);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        Log.Information("ReceiveMessagesAsync ending");

        Scope? scope = state.Scope;

        if (scope is null)
        {
            Log.Information("Scope is null");
            return returnValue;
        }

        try
        {
            if (exception != null)
            {
                scope.Span.SetException(exception);
            }
        }
        finally
        {
            scope.Dispose();
        }

        return returnValue;
    }
}
