// <copyright file="InMemoryTransportMessageIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.CallTarget
{
    /// <summary>
    /// Hooks the InMemoryTransportMessage constructor to copy trace context headers into the
    /// transport message's Headers dictionary. This enables context propagation for the in-memory
    /// (loopback) transport in MT7 versions up to and including 7.3.0, which do not natively copy
    /// SendContext.Headers to InMemoryTransportMessage.Headers. From 7.3.1 onward MassTransit
    /// performs this copy itself, so the integration's MaximumVersion stops at 7.3.0 (inclusive).
    /// <para/>
    /// The constructor executes synchronously within the same async context as the
    /// DiagnosticObserver's Send.Start event, so the masstransit.send scope created there is
    /// still the active scope when this hook fires; we read its span context directly.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "MassTransit",
        TypeName = "MassTransit.Transports.InMemory.Fabric.InMemoryTransportMessage",
        MethodName = ".ctor",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.Guid, "System.Byte[]", ClrNames.String, ClrNames.String },
        MinimumVersion = "7.0.0",
        MaximumVersion = "7.3.0",
        IntegrationName = MassTransitConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class InMemoryTransportMessageIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(InMemoryTransportMessageIntegration));

        /// <summary>
        /// OnMethodEnd callback — fires after the constructor body completes.
        /// At this point, instance.Headers already has MessageId and Content-Type set.
        /// We inject the active masstransit.send span's context if one is available.
        /// </summary>
        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, System.Exception? exception, in CallTargetState state)
            where TTarget : class
        {
            if (Tracer.Instance.ActiveScope?.Span.Context is not SpanContext spanContext)
            {
                return CallTargetReturn.GetDefault();
            }

            try
            {
                if (!instance.TryDuckCast<IInMemoryTransportMessage>(out var msg) || msg?.Headers == null)
                {
                    Log.Warning(
                        "InMemoryTransportMessageIntegration: Duck cast failed or Headers null for InstanceType={Type}",
                        instance.GetType().FullName);
                    return CallTargetReturn.GetDefault();
                }

                var adapter = new CarrierWithDelegate<IDictionary<string, object>>(
                    msg.Headers,
                    setter: (d, k, v) => d[k] = v);
                var propagationContext = new PropagationContext(spanContext, Baggage.Current);
                Tracer.Instance.TracerManager.SpanContextPropagator.Inject(propagationContext, adapter);

                Log.Debug(
                    "InMemoryTransportMessageIntegration: Injected trace context TraceId={TraceId}",
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
