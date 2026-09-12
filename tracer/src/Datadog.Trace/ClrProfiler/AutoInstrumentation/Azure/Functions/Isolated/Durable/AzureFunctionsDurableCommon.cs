// <copyright file="AzureFunctionsDurableCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable

using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

internal static class AzureFunctionsDurableCommon
{
    internal const string IntegrationName = nameof(Configuration.IntegrationId.AzureFunctions);
    internal const string OperationName = AzureFunctionsConstants.AzureFunctionName;
    internal const IntegrationId IntegrationId = Configuration.IntegrationId.AzureFunctions;

    private const string SpanType = SpanTypes.Serverless;
    private const string OrchestrationTrigger = "DurableOrchestration";
    private const string ActivityTrigger = "DurableActivity";
    private const string EntityTrigger = "DurableEntity";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AzureFunctionsDurableCommon));

    internal static CallTargetState OnFunctionExecutionBegin<TFunctionContext>(TFunctionContext functionContext)
        where TFunctionContext : IDurableFunctionContext
    {
        var tracer = Tracer.Instance;
        if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
        {
            return CallTargetState.GetDefault();
        }

        Scope? scope = null;
        try
        {
            var triggerType = GetTriggerType(functionContext);
            if (triggerType is null)
            {
                return CallTargetState.GetDefault();
            }

            var tags = new AzureFunctionsTags
            {
                TriggerType = triggerType,
                ShortName = functionContext.FunctionDefinition.Name,
                FullName = functionContext.FunctionDefinition.EntryPoint,
            };

            var extractedContext = ExtractPropagatedContext(functionContext).MergeBaggageInto(Baggage.Current);
            scope = tracer.StartActiveInternal(OperationName, parent: extractedContext.SpanContext, tags: tags);
            scope.Span.ResourceName = $"{triggerType} {functionContext.FunctionDefinition.Name}";
            scope.Span.Type = SpanType;

            if (scope.Span.IsRootSpan)
            {
                tags.SetAnalyticsSampleRate(IntegrationId, tracer.CurrentTraceSettings.Settings, enabledWithGlobalSetting: false);
            }

            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating Azure Durable Functions scope");
        }

        return new CallTargetState(scope);
    }

    [TestingAndPrivateOnly]
    internal static string? GetTriggerType<TFunctionContext>(TFunctionContext functionContext)
        where TFunctionContext : IDurableFunctionContext
    {
#pragma warning disable CS8605 // Unboxing a possibly null value. InputBindings contains non-null BindingMetadata values.
        foreach (DictionaryEntry entry in functionContext.FunctionDefinition.InputBindings)
#pragma warning restore CS8605
        {
            var binding = entry.Value.DuckCast<BindingMetadata>();
            if (binding.Direction != BindingDirection.In || binding.BindingType is null)
            {
                continue;
            }

            if (binding.BindingType.Equals("orchestrationTrigger", StringComparison.OrdinalIgnoreCase))
            {
                return OrchestrationTrigger;
            }

            if (binding.BindingType.Equals("activityTrigger", StringComparison.OrdinalIgnoreCase))
            {
                return ActivityTrigger;
            }

            if (binding.BindingType.Equals("entityTrigger", StringComparison.OrdinalIgnoreCase))
            {
                return EntityTrigger;
            }
        }

        return null;
    }

    [TestingAndPrivateOnly]
    internal static PropagationContext ExtractPropagatedContext<TFunctionContext>(TFunctionContext functionContext)
        where TFunctionContext : IDurableFunctionContext
    {
        try
        {
            if (functionContext.TraceContext is not { } rawTraceContext
             || !rawTraceContext.TryDuckCast<IWorkerTraceContext>(out var traceContext)
             || StringUtil.IsNullOrEmpty(traceContext.TraceParent))
            {
                return default;
            }

            // The Durable Functions Application Insights listener requests AllData instead of AllDataAndRecorded.
            // System.Diagnostics.Activity therefore clears the inherited W3C recorded flag while retaining
            // Datadog's positive sampling decision in tracestate. Reconcile the two values before extraction.
            // If there is no Datadog decision, remove Azure's implicit rejection and let the local sampler decide.
            var datadogSamplingPriority = W3CTraceContextPropagator.ParseTraceState(traceContext.TraceState).SamplingPriority;
            var carrier = (
                TraceParent: ReconcileTraceParentSampling(traceContext.TraceParent!, traceContext.TraceState),
                TraceState: traceContext.TraceState);

            var extractedContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(
                carrier,
                static (context, name) =>
                {
                    if (name.Equals("traceparent", StringComparison.OrdinalIgnoreCase))
                    {
                        return new[] { context.TraceParent };
                    }

                    if (name.Equals("tracestate", StringComparison.OrdinalIgnoreCase))
                    {
                        return new[] { context.TraceState };
                    }

                    return Enumerable.Empty<string?>();
                });

            return RemoveImplicitAzureSamplingDecision(extractedContext, datadogSamplingPriority);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error extracting Azure Durable Functions trace context");
            return default;
        }
    }

    [TestingAndPrivateOnly]
    internal static string ReconcileTraceParentSampling(string traceParent, string? traceState)
    {
        if (W3CTraceContextPropagator.ParseTraceState(traceState).SamplingPriority is not > 0)
        {
            return traceParent;
        }

        var flagsStart = traceParent.LastIndexOf('-') + 1;
        if (flagsStart <= 0
         || traceParent.Length - flagsStart != 2
         || !HexString.TryParseByte(traceParent.AsSpan(flagsStart, 2), out var flags)
         || (flags & 1) != 0)
        {
            return traceParent;
        }

        var reconciledTraceParent = traceParent.ToCharArray();
        var reconciledFlags = (flags | 1).ToString("x2", CultureInfo.InvariantCulture);
        reconciledTraceParent[flagsStart] = reconciledFlags[0];
        reconciledTraceParent[flagsStart + 1] = reconciledFlags[1];
        return new string(reconciledTraceParent);
    }

    private static PropagationContext RemoveImplicitAzureSamplingDecision(PropagationContext context, int? datadogSamplingPriority)
    {
        if (datadogSamplingPriority is not null
         || context.SpanContext is not { SamplingPriority: SamplingPriorityValues.AutoReject } spanContext)
        {
            return context;
        }

        var contextWithoutSamplingDecision = new SpanContext(
            traceId: spanContext.TraceId128,
            spanId: spanContext.SpanId,
            samplingPriority: null,
            serviceName: spanContext.ServiceName,
            origin: spanContext.Origin,
            rawTraceId: spanContext.RawTraceId,
            rawSpanId: spanContext.RawSpanId,
            isRemote: spanContext.IsRemote)
        {
            PropagatedTags = spanContext.PropagatedTags,
            AdditionalW3CTraceState = spanContext.AdditionalW3CTraceState,
            LastParentId = spanContext.LastParentId,
            ServiceNameSource = spanContext.ServiceNameSource,
        };

        return new PropagationContext(contextWithoutSamplingDecision, context.Baggage, context.Links);
    }
}

#endif
