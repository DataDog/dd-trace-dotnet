// <copyright file="OcelotMessageInvokerPoolIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if NET6_0_OR_GREATER

using System;
using System.ComponentModel;
using System.Diagnostics;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient.SocketsHttpHandler;

/// <summary>
/// System.Net.Http.SocketsHttpHandler Ocelot.Requester.MessageInvokerPool::CreateHandler(Ocelot.Configuration.DownstreamRoute) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Ocelot",
    TypeName = "Ocelot.Requester.MessageInvokerPool",
    MethodName = "CreateHandler",
    ReturnTypeName = "System.Net.Http.SocketsHttpHandler",
    ParameterTypeNames = ["Ocelot.Configuration.DownstreamRoute"],
    MinimumVersion = "24.1.0",
    MaximumVersion = "24.*.*",
    IntegrationName = IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OcelotMessageInvokerPoolIntegration
{
    private const string IntegrationName = nameof(Configuration.IntegrationId.HttpMessageHandler);
    private const IntegrationId IntegrationId = Configuration.IntegrationId.HttpMessageHandler;
    private const IntegrationId SocketHandlerIntegrationId = Configuration.IntegrationId.HttpSocketsHandler;

    /// <summary>
    /// OnMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
    /// <param name="returnValue">Instance of System.Net.Http.SocketsHttpHandler</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A return value, in an async scenario will be T of Task of T</returns>
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        // If our HttpClient and SocketsHttpHandler integrations are not enabled, do not modify the factory behavior
        var settings = Tracer.Instance.CurrentTraceSettings.Settings;
        if (!settings.IsIntegrationEnabled(IntegrationId) || !settings.IsIntegrationEnabled(SocketHandlerIntegrationId))
        {
            return new CallTargetReturn<TReturn>(returnValue);
        }

        // On net6.0+, the proxy will inject the current Activity into the request headers, using the propagator
        // stored in the SocketsHttpHandler.ActivityHeadersPropagator field. This will overwrite the propagation
        // headers that have already been set by our Datadog tracer's HttpClient instrumentation.
        // To ensure that distributed tracing works properly, unset the ActivityHeadersPropagator so the
        // trace context is not updated by Ocelot.
        if (returnValue is System.Net.Http.SocketsHttpHandler handler)
        {
            handler.ActivityHeadersPropagator = DistributedContextPropagator.CreateNoOutputPropagator();
        }

        return new CallTargetReturn<TReturn>(returnValue);
    }
}

#endif
