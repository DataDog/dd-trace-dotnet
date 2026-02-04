// <copyright file="AzureFunctionsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    internal static class AzureFunctionsCommon
    {
        private const string HttpRequestContextKey = "HttpRequestContext";
        private const string SpanType = SpanTypes.Serverless;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AzureFunctionsCommon));

        public const string IntegrationName = nameof(IntegrationId.AzureFunctions);
        public const IntegrationId IntegrationId = Configuration.IntegrationId.AzureFunctions;
        public const string OperationName = "azure_functions.invoke";

        public static CallTargetState OnFunctionExecutionBegin<TTarget, TFunction>(TTarget instance, TFunction instanceParam)
            where TFunction : IFunctionInstance
        {
            var tracer = Tracer.Instance;

            if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return CallTargetState.GetDefault();
            }

            if (tracer.Settings.AzureAppServiceMetadata is { IsIsolatedFunctionsApp: true }
             && tracer.InternalActiveScope is null)
            {
                // in a "timer" trigger, or similar. Context won't be propagated to child, so no
                // need to create the scope etc.
                return CallTargetState.GetDefault();
            }

            var scope = CreateScope(tracer, instanceParam);
            return new CallTargetState(scope);
        }

        private static Scope? CreateScope<TFunction>(Tracer tracer, TFunction instanceParam)
            where TFunction : IFunctionInstance
        {
            Scope? scope = null;

            try
            {
                var triggerType = "Unknown";
                var bindingSourceType = instanceParam.BindingSource.GetType();
                var bindingSourceFullName = bindingSourceType.FullName ?? string.Empty;

                switch (instanceParam.Reason)
                {
                    case AzureFunctionsExecutionReason.HostCall:
                        // The root span will be the AspNetCoreDiagnosticObserver
                        // This could be HttpTrigger or EventGridTrigger

                        // Default HttpTrigger binding source: Microsoft.Azure.WebJobs.Host.Executors.BindingSource
                        triggerType = "Http";

                        if (bindingSourceFullName.Contains("Newtonsoft.Json.Linq.JObject"))
                        {
                            // ex: Microsoft.Azure.WebJobs.Host.Triggers.TriggerBindingSource`1[[Newtonsoft.Json.Linq.JObject, Newtonsoft.Json, Version=12.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed]]
                            triggerType = "EventGrid";
                        }

                        break;
                    case AzureFunctionsExecutionReason.AutomaticTrigger:
                        // This can apply to anything not triggered by HTTP or manually from the dashboard
                        // e.g., timer, queues ...
                        // Automatic is the catch all for any triggers we don't explicitly handle
                        triggerType = "Automatic";

                        if (bindingSourceFullName.Contains("Timer"))
                        {
                            // ex: Microsoft.Azure.WebJobs.Host.Triggers.TriggerBindingSource`1[[Microsoft.Azure.WebJobs.TimerInfo, Microsoft.Azure.WebJobs.Extensions, Version=4.0.3.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35]]
                            triggerType = "Timer";
                        }
                        else if (bindingSourceFullName.Contains("ServiceBus"))
                        {
                            // ex: Microsoft.Azure.WebJobs.Host.Triggers.TriggerBindingSource`1[[Microsoft.Azure.WebJobs.ServiceBus.ServiceBusTriggerInput, Microsoft.Azure.WebJobs.ServiceBus, Version=4.3.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35]]
                            triggerType = "ServiceBus";
                        }
                        else if (bindingSourceFullName.Contains("Blob"))
                        {
                            // ex: Microsoft.Azure.WebJobs.Host.Triggers.TriggerBindingSource`1[[Microsoft.Azure.Storage.Blob.ICloudBlob, Microsoft.Azure.Storage.Blob, Version=11.1.7.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35]]
                            triggerType = "Blob";
                        }
                        else if (bindingSourceFullName.Contains("Microsoft.Azure.DocumentDB.Core"))
                        {
                            // ex: Microsoft.Azure.WebJobs.Host.Triggers.TriggerBindingSource`1[[System.Collections.Generic.IReadOnlyList`1[[Microsoft.Azure.Documents.Document, Microsoft.Azure.DocumentDB.Core, Version=2.13.1.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35]], System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]]
                            triggerType = "Cosmos";
                        }

                        break;
                    case AzureFunctionsExecutionReason.Dashboard:
                        triggerType = "Dashboard";
                        break;
                }

                var functionName = instanceParam.FunctionDescriptor.ShortName;

                // Ignoring null because guaranteed running in AAS
                if (tracer.Settings.AzureAppServiceMetadata is { IsIsolatedFunctionsApp: true }
                 && tracer.InternalActiveScope is { } activeScope)
                {
                    // We don't want to create a new scope here when running isolated functions,
                    // otherwise it is essentially a duplicate of the span created inside the
                    // isolated app, but we _do_ want to populate the "root" span here with the appropriate names
                    // and update it to be a "serverless" span.
                    var rootSpan = activeScope.Root.Span;

                    // The shortname is prefixed with "Functions.", so strip that off
                    var remoteFunctionName = functionName?.StartsWith("Functions.") == true
                                                 ? functionName.Substring(10)
                                                 : functionName;

                    AzureFunctionsTags.SetRootSpanTags(
                        rootSpan,
                        shortName: remoteFunctionName,
                        fullName: rootSpan.Tags is AzureFunctionsTags t ? t.FullName : null, // can't get anything meaningful here, so leave it as-is
                        bindingSource: bindingSourceType.FullName,
                        triggerType: triggerType);
                    rootSpan.Type = SpanType;
                    return null;
                }

                if (tracer.InternalActiveScope == null)
                {
                    var tags = new AzureFunctionsTags
                    {
                        TriggerType = triggerType,
                        ShortName = functionName,
                        FullName = instanceParam.FunctionDescriptor.FullName,
                        BindingSource = bindingSourceType.FullName
                    };

                    // This is the root scope
                    tags.SetAnalyticsSampleRate(IntegrationId, tracer.CurrentTraceSettings.Settings, enabledWithGlobalSetting: false);
                    scope = tracer.StartActiveInternal(OperationName, tags: tags);
                }
                else
                {
                    scope = tracer.StartActiveInternal(OperationName);
                    AzureFunctionsTags.SetRootSpanTags(
                        scope.Root.Span,
                        shortName: functionName,
                        fullName: instanceParam.FunctionDescriptor.FullName,
                        bindingSource: bindingSourceType.FullName,
                        triggerType: triggerType);
                }

                scope.Root.Span.Type = SpanType;
                scope.Span.ResourceName = $"{triggerType} {functionName}";
                scope.Span.Type = SpanType;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return scope;
        }

        public static CallTargetState OnIsolatedFunctionBegin<T>(T functionContext)
            where T : IFunctionContext
        {
            var tracer = Tracer.Instance;

            if (tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
            {
                var scope = CreateIsolatedFunctionScope(tracer, functionContext);

                if (scope != null)
                {
                    return new CallTargetState(scope);
                }
            }

            return CallTargetState.GetDefault();
        }

        private static Scope? CreateIsolatedFunctionScope<T>(Tracer tracer, T functionContext)
            where T : IFunctionContext
        {
            Scope? scope = null;

            try
            {
                // Try to work out which trigger type it is
                var triggerType = "Unknown";
                PropagationContext extractedContext = default;

#pragma warning disable CS8605 // Unboxing a possibly null value. This is a lie, that only affects .NET Core 3.1
                foreach (DictionaryEntry entry in functionContext.FunctionDefinition.InputBindings)
#pragma warning restore CS8605 // Unboxing a possibly null value.
                {
                    var binding = entry.Value.DuckCast<BindingMetadata>();
                    if (binding.Direction != BindingDirection.In || binding.BindingType is null)
                    {
                        continue;
                    }

                    var type = binding.BindingType;
                    triggerType = type switch
                    {
                        _ when type.Equals("httpTrigger", StringComparison.OrdinalIgnoreCase) => "Http",             // Microsoft.Azure.Functions.Worker.Extensions.Http
                        _ when type.Equals("timerTrigger", StringComparison.OrdinalIgnoreCase) => "Timer",           // Microsoft.Azure.Functions.Worker.Extensions.Timer
                        _ when type.Equals("serviceBusTrigger", StringComparison.OrdinalIgnoreCase) => "ServiceBus", // Microsoft.Azure.Functions.Worker.Extensions.ServiceBus
                        _ when type.Equals("queue", StringComparison.OrdinalIgnoreCase) => "Queue",                  // Microsoft.Azure.Functions.Worker.Extensions.Queues
                        _ when type.StartsWith("blob", StringComparison.OrdinalIgnoreCase) => "Blob",                // Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs
                        _ when type.StartsWith("eventHub", StringComparison.OrdinalIgnoreCase) => "EventHub",        // Microsoft.Azure.Functions.Worker.Extensions.EventHubs
                        _ when type.StartsWith("cosmosDb", StringComparison.OrdinalIgnoreCase) => "Cosmos",          // Microsoft.Azure.Functions.Worker.Extensions.CosmosDB
                        _ when type.StartsWith("eventGrid", StringComparison.OrdinalIgnoreCase) => "EventGrid",      // Microsoft.Azure.Functions.Worker.Extensions.EventGrid.CosmosDB
                        _ => "Automatic",                                                                            // Automatic is the catch all for any triggers we don't explicitly handle
                    };

                    switch (triggerType)
                    {
                        case "Http":
                        {
                            // Detect ASP.NET Core integration by checking for HttpContext in FunctionContext.Items.
                            // In ASP.NET Core mode, HTTP requests are proxied directly (not via gRPC).
                            // The headers in the gRPC message are STALE (contain host's root span context).
                            // The key "HttpRequestContext" is set by FunctionsHttpProxyingMiddleware in the worker
                            var isAspNetCoreIntegration = functionContext.Items?.ContainsKey(HttpRequestContextKey) == true;

                            if (isAspNetCoreIntegration)
                            {
                                // Skip extraction - will rely on HttpContext.Items bridge or create root span
                                Log.Debug("Skipping header extraction - HTTP trigger with ASP.NET Core integration detected (HTTP proxying mode)");
                            }
                            else
                            {
                                // Only extract from gRPC message when NOT using ASP.NET Core integration
                                extractedContext = ExtractPropagatedContextFromHttp(functionContext, entry.Key as string).MergeBaggageInto(Baggage.Current);
                                Log.Debug("Extracted trace context from gRPC message (non-ASP.NET Core mode)");
                            }

                            break;
                        }

                        case "ServiceBus" when tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus):
                            extractedContext = ExtractPropagatedContextFromMessaging(functionContext, "UserProperties", "UserPropertiesArray").MergeBaggageInto(Baggage.Current);
                            break;

                        case "EventHub" when tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs):
                            extractedContext = ExtractPropagatedContextFromMessaging(functionContext, "Properties", "PropertiesArray").MergeBaggageInto(Baggage.Current);
                            break;
                    }

                    break;
                }

                var tags = new AzureFunctionsTags
                {
                    TriggerType = triggerType,
                    ShortName = functionContext.FunctionDefinition.Name,
                    FullName = functionContext.FunctionDefinition.EntryPoint,
                };

                // Try to get parent span context from (in order):
                // - existing local span
                // - HttpContext.Items, for HTTP triggers using ASP.NET Core integration. Set in AspNetCoreHttpRequestHandler.StartAspNetCorePipelineScope().
                // - extracted from propagation headers
                var parentSpanContext = tracer.InternalActiveScope?.Span.Context ??
                                        GetAspNetCoreScope(functionContext)?.Span.Context ??
                                        extractedContext.SpanContext;

                scope = tracer.StartActiveInternal(OperationName, parent: parentSpanContext, tags: tags);
                var rootSpan = scope.Root.Span;

                if (scope.Span == rootSpan)
                {
                    // this is the local root span
                    tags.SetAnalyticsSampleRate(IntegrationId, tracer.CurrentTraceSettings.Settings, enabledWithGlobalSetting: false);
                }
                else
                {
                    // this is NOT the local root span, copy some tags to the root span
                    AzureFunctionsTags.SetRootSpanTags(
                        rootSpan,
                        shortName: tags.ShortName,
                        fullName: tags.FullName,
                        bindingSource: rootSpan.Tags is AzureFunctionsTags t ? t.BindingSource : null,
                        triggerType: tags.TriggerType);
                }

                // change root span's type to "serverless"
                scope.Root.Span.Type = SpanType;

                scope.Span.ResourceName = $"{tags.TriggerType} {tags.ShortName}";
                scope.Span.Type = SpanType;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return scope;
        }

        private static Scope? GetAspNetCoreScope<T>(T functionContext)
            where T : IFunctionContext
        {
            Log.Debug("Azure Functions span creation: AsyncLocal context not available - attempting HttpContext.Items bridge");

            // AsyncLocal context didn't flow - try to get parent scope from HttpContext.Items
            // This happens in Azure Functions isolated worker where middleware breaks AsyncLocal flow
            Scope? parentScope = null;

            try
            {
                if (functionContext.Items != null &&
                    functionContext.Items.TryGetValue(HttpRequestContextKey, out var httpContextObj) &&
                    httpContextObj is Microsoft.AspNetCore.Http.HttpContext httpContext &&
                    httpContext.Items.TryGetValue(AspNetCoreHttpRequestHandler.HttpContextActiveScopeKey, out var scopeObj) &&
                    scopeObj is Scope aspNetCoreScope)
                {
                    parentScope = aspNetCoreScope;

                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        var spanContext = parentScope.Span.Context;

                        Log.Debug(
                            "Azure Functions span creation: Retrieved AspNetCore scope {TraceId}-{SpanId}",
                            spanContext.RawTraceId,
                            spanContext.RawSpanId);
                    }
                }
                else
                {
                    Log.Debug("Azure Functions span creation: Could not retrieve AspNetCore scope from HttpContext.Items");
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Azure Functions span creation: Error retrieving AspNetCore scope from HttpContext.Items");
            }

            return parentScope;
        }

        private static PropagationContext ExtractPropagatedContextFromHttp<T>(T context, string? bindingName)
            where T : IFunctionContext
        {
            // Need to try and grab the headers from the context
            // Unfortunately, the parsed object isn't available yet, so we grab it
            // directly from the grpc call instead. This is... interesting. It
            // is effectively doing the equivalent of context.GetHttpRequestDataAsync() which is
            // the suggested approach in the docs.
            if (context.Features is null || string.IsNullOrEmpty(bindingName))
            {
                return default;
            }

            try
            {
                object? feature = null;
                foreach (var keyValuePair in context.Features)
                {
                    if (keyValuePair.Key.FullName?.Equals("Microsoft.Azure.Functions.Worker.Context.Features.IFunctionBindingsFeature") == true)
                    {
                        feature = keyValuePair.Value;
                        break;
                    }
                }

                if (feature is null || !feature.TryDuckCast<FunctionBindingsFeatureStruct>(out var bindingFeature))
                {
                    return default;
                }

                if (bindingFeature.InputData is null
                 || !bindingFeature.InputData.TryGetValue(bindingName!, out var requestDataObject)
                 || requestDataObject is null)
                {
                    return default;
                }

                if (!requestDataObject.TryDuckCast<HttpRequestDataStruct>(out var httpRequest))
                {
                    return default;
                }

                return Tracer.Instance.TracerManager.SpanContextPropagator.Extract(new HttpHeadersCollection(httpRequest.Headers));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting propagated HTTP context from Http binding");
                return default;
            }
        }

        internal static PropagationContext ExtractPropagatedContextFromMessaging<T>(T context, string singlePropertyKey, string batchPropertyKey)
            where T : IFunctionContext
        {
            try
            {
                if (context.Features == null)
                {
                    return default;
                }

                GrpcBindingsFeatureStruct? bindingsFeature = null;
                foreach (var kvp in context.Features)
                {
                    if (kvp.Key.FullName?.Equals("Microsoft.Azure.Functions.Worker.Context.Features.IFunctionBindingsFeature") == true)
                    {
                        bindingsFeature = kvp.Value?.TryDuckCast<GrpcBindingsFeatureStruct>(out var feature) == true ? feature : null;
                        break;
                    }
                }

                if (bindingsFeature == null)
                {
                    return default;
                }

                var triggerMetadata = bindingsFeature.Value.TriggerMetadata;
                var extractedContexts = new List<PropagationContext>();

                // Extract from single message properties
                if (triggerMetadata?.TryGetValue(singlePropertyKey, out var singlePropsObj) == true &&
                    TryParseJson<Dictionary<string, object>>(singlePropsObj, out var singleProps) && singleProps != null)
                {
                    var singleContext = Shared.AzureMessagingCommon.ExtractContext(singleProps);
                    if (singleContext.SpanContext != null)
                    {
                        extractedContexts.Add(singleContext);
                    }
                }

                // Extract from batch properties array
                if (triggerMetadata?.TryGetValue(batchPropertyKey, out var arrayPropsObj) == true &&
                    TryParseJson<Dictionary<string, object>[]>(arrayPropsObj, out var propsArray) && propsArray != null)
                {
                    foreach (var props in propsArray)
                    {
                        var batchContext = Shared.AzureMessagingCommon.ExtractContext(props);
                        if (batchContext.SpanContext != null)
                        {
                            extractedContexts.Add(batchContext);
                        }
                    }
                }

                if (extractedContexts.Count == 0)
                {
                    return default;
                }

                bool areAllTheSame = extractedContexts.Count == 1 ||
                                     (extractedContexts.Count > 1 && AreAllSpanContextsIdentical(extractedContexts));

                if (!areAllTheSame)
                {
                    Log.Warning("Multiple different contexts found in messages. Using first context for parentship.");
                }

                return extractedContexts[0];
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting propagated context from messaging binding");
                return default;
            }
        }

        private static bool TryParseJson<T>(object? jsonObj, [NotNullWhen(true)] out T? result)
            where T : class
        {
            result = null;
            if (jsonObj is not string jsonString)
            {
                return false;
            }

            try
            {
                result = JsonConvert.DeserializeObject<T>(jsonString);
                return result != null;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to parse JSON: {Json}", jsonString);
                return false;
            }
        }

        // Checks if all SpanContexts are identical (ignores baggage)
        private static bool AreAllSpanContextsIdentical(List<PropagationContext> contexts)
        {
            if (contexts.Count <= 1)
            {
                return true;
            }

            var first = contexts[0].SpanContext;
            return contexts.All(ctx =>
                ctx.SpanContext != null &&
                ctx.SpanContext.TraceId128 == first!.TraceId128 &&
                ctx.SpanContext.SpanId == first.SpanId);
        }
    }
}

#endif
