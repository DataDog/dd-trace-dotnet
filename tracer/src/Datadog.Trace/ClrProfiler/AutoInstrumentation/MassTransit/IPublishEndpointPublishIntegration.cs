// <copyright file="IPublishEndpointPublishIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit;

/// <summary>
/// System.Threading.Tasks.Task MassTransit.IPublishEndpoint::Publish[T](T,System.Threading.CancellationToken) calltarget instrumentation
/// NOTE: This instrumentation is DISABLED to match MT8 OTEL behavior.
/// MT8 OTEL does not create a separate "publish" span - only the "send" span is created.
/// The SendEndpointPipeSendIntegration captures all send operations including publishes.
/// </summary>
// [InstrumentMethod(
//     AssemblyName = "MassTransit",
//     TypeName = "MassTransit.MassTransitBus",
//     MethodName = "MassTransit.IPublishEndpoint.Publish",
//     ReturnTypeName = ClrNames.Task,
//     ParameterTypeNames = [ClrNames.Ignore, ClrNames.CancellationToken],
//     MinimumVersion = "7.0.0",
//     MaximumVersion = "7.*.*",
//     IntegrationName = nameof(IntegrationId.MassTransit))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class IPublishEndpointPublishIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(IPublishEndpointPublishIntegration));

    internal static CallTargetState OnMethodBegin<TTarget, TMessage>(TTarget instance, TMessage message, CancellationToken cancellationToken)
    {
        Log.Debug("MassTransit IPublishEndpointPublishIntegration.OnMethodBegin() - Intercepted IPublishEndpoint.Publish<{MessageType}>", typeof(TMessage).Name);

        var messageType = typeof(TMessage).Name;
        var messageTypeFullName = typeof(TMessage).FullName;
        var scope = MassTransitIntegration.CreateProducerScope(
            Tracer.Instance,
            MassTransitConstants.OperationPublish,
            messageType,
            destinationName: $"urn:message:{messageTypeFullName}");

        if (scope != null)
        {
            Log.Debug("MassTransit IPublishEndpointPublishIntegration - Created producer scope for message type: {MessageType}", messageType);
        }
        else
        {
            Log.Warning("MassTransit IPublishEndpointPublishIntegration - Failed to create producer scope (integration may be disabled)");
        }

        return new CallTargetState(scope);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        Log.Debug("MassTransit IPublishEndpointPublishIntegration.OnAsyncMethodEnd() - Completing publish span");

        if (exception != null)
        {
            Log.Warning(exception, "MassTransit IPublishEndpointPublishIntegration - Publish failed with exception");
        }

        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
}
