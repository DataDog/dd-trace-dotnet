// <copyright file="PopulateBasicPropertiesHeadersIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

/// <summary>
/// RabbitMQ.Client.BasicProperties RabbitMQ.Client.Impl.Channel::PopulateBasicPropertiesHeaders[TProperties](TProperties,System.Diagnostics.Activity,System.UInt64) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "RabbitMQ.Client",
    TypeName = "RabbitMQ.Client.Impl.Channel",
    MethodName = "PopulateBasicPropertiesHeaders",
    ReturnTypeName = "RabbitMQ.Client.BasicProperties",
    ParameterTypeNames = ["!!0", ClrNames.Activity, ClrNames.UInt64],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = RabbitMQConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class PopulateBasicPropertiesHeadersIntegration
{
    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        var tracer = Tracer.Instance;
        var activeSpan = tracer.ActiveScope?.Span;
        if (activeSpan is not Span { Tags: RabbitMQTags tags } span
            || !RabbitMQIntegration.IsRabbitMqSpan(tracer, span, SpanKinds.Producer))
        {
            // We're not in a RabbitMQ span "produce", so bail out - either
            // we didn't create an active scope for some reason (e.g integration disabled),
            // or internal refactoring of the RabbitMQ library means this method is called
            // outside of BasicPublishAsync. Either way, we don't need to add the headers.
            return new CallTargetReturn<TReturn?>(returnValue);
        }

        returnValue ??= CachedBasicPropertiesHelper<TReturn>.CreateHeaders();
        var duckType = returnValue.DuckCast<IBasicProperties>()!;

        // add distributed tracing headers to the message
        duckType.Headers ??= new Dictionary<string, object>();

        var context = new PropagationContext(span.Context, Baggage.Current);
        tracer.TracerManager.SpanContextPropagator.Inject(context, duckType.Headers, default(ContextPropagation));

        RabbitMQIntegration.SetDataStreamsCheckpointOnProduce(
            tracer,
            span,
            tags,
            duckType.Headers,
            int.TryParse(tags.MessageSize, out var bodyLength) ? bodyLength : 0);

        return new CallTargetReturn<TReturn?>(returnValue);
    }
}
