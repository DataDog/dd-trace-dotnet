// <copyright file="InMemoryTransportMessageIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

// MassTransit 7 only runs on .NET Core/.NET 5+, so we exclude .NET Framework
#if !NETFRAMEWORK
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.CallTarget
{
    /// <summary>
    /// Hooks the InMemoryTransportMessage constructor to copy trace context headers into the
    /// transport message's Headers dictionary. This enables context propagation for the in-memory
    /// (loopback) transport in MT7 versions prior to 7.3.0, which do not natively copy
    /// SendContext.Headers to InMemoryTransportMessage.Headers.
    /// <para/>
    /// The span context to inject is passed via <see cref="MassTransitCommon.PendingInMemorySpanContext"/>
    /// which is set by the DiagnosticObserver's OnProduceStart before the constructor fires.
    /// The constructor executes synchronously within the same async context as the Send.Start
    /// diagnostic event, so AsyncLocal is safe to use here.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "MassTransit",
        TypeName = "MassTransit.Transports.InMemory.Fabric.InMemoryTransportMessage",
        MethodName = ".ctor",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.Guid, "System.Byte[]", ClrNames.String, ClrNames.String },
        MinimumVersion = "7.0.0",
        MaximumVersion = "7.*.*",
        IntegrationName = MassTransitConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class InMemoryTransportMessageIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(InMemoryTransportMessageIntegration));

        /// <summary>
        /// OnMethodEnd callback — fires after the constructor body completes.
        /// At this point, instance.Headers already has MessageId and Content-Type set.
        /// We inject the pending trace context headers if one is available.
        /// </summary>
        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, System.Exception? exception, in CallTargetState state)
            where TTarget : class
        {
            var spanContext = MassTransitCommon.PendingInMemorySpanContext.Value;
            if (spanContext == null)
            {
                return CallTargetReturn.GetDefault();
            }

            // Clear immediately to avoid leaking to nested sends
            MassTransitCommon.PendingInMemorySpanContext.Value = null;

            try
            {
                // Get the Headers property — IDictionary<string, object> on InMemoryTransportMessage
                var headers = MassTransitCommon.TryGetProperty<IDictionary<string, object>>(instance, "Headers");
                if (headers == null)
                {
                    Log.Debug("InMemoryTransportMessageIntegration: Headers property not found");
                    return CallTargetReturn.GetDefault();
                }

                // Inject trace context into the transport message headers using a dict adapter
                var adapter = new CarrierWithDelegate<IDictionary<string, object>>(
                    headers,
                    setter: (d, k, v) => d[k] = v);
                var propagationContext = new PropagationContext(spanContext, Baggage.Current);
                Tracer.Instance.TracerManager.SpanContextPropagator.Inject(propagationContext, adapter);

                Log.Debug(
                    "InMemoryTransportMessageIntegration: Injected trace context into InMemoryTransportMessage.Headers TraceId={TraceId}",
                    spanContext.TraceId);
            }
            catch (System.Exception ex)
            {
                Log.Debug(ex, "InMemoryTransportMessageIntegration: Failed to inject trace context");
            }

            return CallTargetReturn.GetDefault();
        }
    }
}
#endif
