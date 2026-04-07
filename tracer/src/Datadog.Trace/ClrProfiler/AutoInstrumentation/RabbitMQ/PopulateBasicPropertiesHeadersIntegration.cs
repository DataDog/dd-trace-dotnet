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
using Datadog.Trace.Logging;
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
public sealed class PopulateBasicPropertiesHeadersIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(PopulateBasicPropertiesHeadersIntegration));

    internal static CallTargetState OnMethodBegin<TTarget, TBasicProperties, TActivity>(TTarget instance, TBasicProperties basicProperties, TActivity sendActivity, ulong publishSequenceNumber)
    {
        return new CallTargetState(null, basicProperties);
    }

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

        // TReturn is type RabbitMQ.Client.BasicProperties
        TReturn? basicProperties;

        // PopulateBasicPropertiesHeaders returns null if the supplied IReadOnlyBasicProperties
        // does not have to be modified or if it's a writable instance.
        // If that is the case then we have to fetch IReadOnlyBasicProperties from the argument
        // list instead of creating a new instance that overwrites the supplied properties.
        if (returnValue is null)
        {
            // state.State is of type RabbitMQ.Client.IReadOnlyBasicProperties
            if (state.State is TReturn writable)
            {
                // Use the existing BasicProperties if it's already of type RabbitMQ.Client.BasicProperties
                basicProperties = writable;
            }
            else if (state.State is null)
            {
                // This case cannot happen as argument is not nullable.
                Log.Warning("Invalid state: PopulateBasicPropertiesHeaders() is returning null and CallTargetState.State has type {Type}", state.State?.GetType().FullName ?? "null");
                return new CallTargetReturn<TReturn?>(returnValue);
            }
            else
            {
                // create new BasicProperties using the BasicProperties(IReadOnlyBasicProperties) copy constructor
                basicProperties = CachedBasicPropertiesHelper<TReturn>.CreateHeaders(state.State!);
            }
        }
        else
        {
            basicProperties = returnValue;
        }

        // duck cast so we can access the Headers property
        var duckType = basicProperties.DuckCast<IBasicProperties>()!;

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

        return new CallTargetReturn<TReturn?>(basicProperties);
    }
}
