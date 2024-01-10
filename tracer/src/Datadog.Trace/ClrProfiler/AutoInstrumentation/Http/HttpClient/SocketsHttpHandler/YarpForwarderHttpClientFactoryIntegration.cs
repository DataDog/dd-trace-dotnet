// <copyright file="YarpForwarderHttpClientFactoryIntegration.cs" company="Datadog">
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
/// System.Void Yarp.ReverseProxy.Forwarder.ForwarderHttpClientFactory::ConfigureHandler(Yarp.ReverseProxy.Forwarder.ForwarderHttpClientContext,System.Net.Http.SocketsHttpHandler) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Yarp.ReverseProxy",
    TypeName = "Yarp.ReverseProxy.Forwarder.ForwarderHttpClientFactory",
    MethodName = "ConfigureHandler",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "Yarp.ReverseProxy.Forwarder.ForwarderHttpClientContext", "System.Net.Http.SocketsHttpHandler" },
    MinimumVersion = "1.1.0",
    MaximumVersion = "2.*.*",
    IntegrationName = IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class YarpForwarderHttpClientFactoryIntegration
{
    private const string IntegrationName = nameof(Configuration.IntegrationId.HttpMessageHandler);
    private const IntegrationId IntegrationId = Configuration.IntegrationId.HttpMessageHandler;
    private const IntegrationId SocketHandlerIntegrationId = Configuration.IntegrationId.HttpSocketsHandler;

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TArg1">Type of the context</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
    /// <param name="context">Instance of Yarp.ReverseProxy.Forwarder.ForwarderHttpClientContext</param>
    /// <param name="handler">Instance of System.Net.Http.SocketsHttpHandler</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, ref TArg1 context, ref System.Net.Http.SocketsHttpHandler handler)
    {
        // If our HttpClient and SocketsHttpHandler integrations are not enabled, do not modify the factory behavior
        if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId) || !Tracer.Instance.Settings.IsIntegrationEnabled(SocketHandlerIntegrationId))
        {
            return CallTargetState.GetDefault();
        }

        return new CallTargetState(scope: null, state: handler);
    }

    /// <summary>
    /// OnMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A return value, in an async scenario will be T of Task of T</returns>
    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
    {
        // On net6.0+, the proxy will inject the current Activity into the request headers, using the propagator
        // stored in the SocketsHttpHandler.ActivityHeadersPropagator field. This will overwrite the propagation
        // headers that have already been set by our Datadog tracer's HttpClient instrumentation.
        // To ensure that distributed tracing works properly, unset the ActivityHeadersPropagator so the
        // trace context is not updated by Yarp.
        if (state.State is System.Net.Http.SocketsHttpHandler handler)
        {
            handler.ActivityHeadersPropagator = DistributedContextPropagator.CreateNoOutputPropagator();
        }

        return CallTargetReturn.GetDefault();
    }
}

#endif
