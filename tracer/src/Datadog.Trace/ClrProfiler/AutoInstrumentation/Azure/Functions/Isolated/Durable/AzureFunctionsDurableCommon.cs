// <copyright file="AzureFunctionsDurableCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable

using System;
using System.Collections;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

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

    internal static CallTargetState OnFunctionExecutionBegin<TFunctionContext>(TFunctionContext functionContext, DateTimeOffset? startTime = null)
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

            var extractedContext = AzureFunctionsDurablePropagation.ExtractPropagatedContext(functionContext).MergeBaggageInto(Baggage.Current);
            ISpanContext? parentContext = extractedContext.SpanContext;
            if (parentContext is null && extractedContext.Links is not null)
            {
                parentContext = SpanContext.None;
            }

            scope = tracer.StartActiveInternal(OperationName, parent: parentContext, startTime: startTime, tags: tags, links: extractedContext.Links);
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

    internal static bool IsOrchestration<TFunctionContext>(TFunctionContext functionContext)
        where TFunctionContext : IDurableFunctionContext
        => GetTriggerType(functionContext) == OrchestrationTrigger;

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
}

#endif
