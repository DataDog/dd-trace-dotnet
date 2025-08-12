// <copyright file="AzureFunctionsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    internal static class AzureFunctionsCommon
    {
        public const string IntegrationName = nameof(Configuration.IntegrationId.AzureFunctions);

        public const string OperationName = "azure.functions.invoke";
        public const string SpanType = SpanTypes.Serverless;
        public const IntegrationId IntegrationId = Configuration.IntegrationId.AzureFunctions;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AzureFunctionsCommon));

        public static CallTargetState OnFunctionExecutionBegin<TTarget, TFunction>(TTarget instance, TFunction instanceParam)
            where TFunction : IFunctionInstance
        {
            var tracer = Tracer.Instance;

            if (tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
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

            return CallTargetState.GetDefault();
        }

        internal static Scope? CreateScope<TFunction>(Tracer tracer, TFunction instanceParam)
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
                    tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
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

            if (tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                var scope = CreateIsolatedFunctionScope(tracer, functionContext);

                if (scope != null)
                {
                    return new CallTargetState(scope);
                }
            }

            return CallTargetState.GetDefault();
        }

        internal static Scope? CreateIsolatedFunctionScope<T>(Tracer tracer, T context)
            where T : IFunctionContext
        {
            Scope? scope = null;

            try
            {
                // Try to work out which trigger type it is
                var triggerType = "Unknown";
                var bindingName = default(string);
                PropagationContext extractedContext = default;
#pragma warning disable CS8605 // Unboxing a possibly null value. This is a lie, that only affects .NET Core 3.1
                foreach (DictionaryEntry entry in context.FunctionDefinition.InputBindings)
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
                        _ when type.Equals("httpTrigger", StringComparison.OrdinalIgnoreCase) => "Http", // Microsoft.Azure.Functions.Worker.Extensions.Http
                        _ when type.Equals("timerTrigger", StringComparison.OrdinalIgnoreCase) => "Timer", // Microsoft.Azure.Functions.Worker.Extensions.Timer
                        _ when type.Equals("serviceBusTrigger", StringComparison.OrdinalIgnoreCase) => "ServiceBus", // Microsoft.Azure.Functions.Worker.Extensions.ServiceBus
                        _ when type.Equals("queue", StringComparison.OrdinalIgnoreCase) => "Queue", // Microsoft.Azure.Functions.Worker.Extensions.Queues
                        _ when type.StartsWith("blob", StringComparison.OrdinalIgnoreCase) => "Blob", // Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs
                        _ when type.StartsWith("eventHub", StringComparison.OrdinalIgnoreCase) => "EventHub", // Microsoft.Azure.Functions.Worker.Extensions.EventHubs
                        _ when type.StartsWith("cosmosDb", StringComparison.OrdinalIgnoreCase) => "Cosmos", // Microsoft.Azure.Functions.Worker.Extensions.CosmosDB
                        _ when type.StartsWith("eventGrid", StringComparison.OrdinalIgnoreCase) => "EventGrid", // Microsoft.Azure.Functions.Worker.Extensions.EventGrid.CosmosDB
                        _ => "Automatic", // Automatic is the catch all for any triggers we don't explicitly handle
                    };

                    // need to extract the headers from the context.
                    // We currently only support httpTrigger, but other triggers may also propagate context,
                    // e.g. Cosmos + ServiceBus, so we should handle those too
                    if (triggerType == "Http")
                    {
                        extractedContext = ExtractPropagatedContextFromHttp(context, entry.Key as string).MergeBaggageInto(Baggage.Current);
                    }
                    else if (triggerType == "ServiceBus")
                    {
                        extractedContext = ExtractPropagatedContextFromServiceBus(context, entry.Key as string).MergeBaggageInto(Baggage.Current);
                    }

                    break;
                }

                var functionName = context.FunctionDefinition.Name;

                var tags = new AzureFunctionsTags
                {
                    TriggerType = triggerType,
                    ShortName = functionName,
                    FullName = context.FunctionDefinition.EntryPoint,
                };

                // Extract ServiceBus messaging metadata if this is a ServiceBus trigger
                if (triggerType == "ServiceBus")
                {
                    LogServiceBusBindingDebugInfo(context, bindingName);

                    var messageId = ExtractServiceBusMessageId(context, bindingName);
                    tags.MessagingMessageId = messageId;

                    // Try to extract destination name from binding metadata
                    var destinationName = ExtractServiceBusDestinationName(context, bindingName);
                    if (!string.IsNullOrEmpty(destinationName))
                    {
                        // Store this in the span context for later retrieval by ProcessMessageIntegration
                        // We'll add this as a tag that ProcessMessageIntegration can read
                        if (scope?.Span?.Tags is AzureFunctionsTags azureTags)
                        {
                            // TODO: We need a way to pass this to ProcessMessageIntegration
                            // For now, just log it
                            Log.Information("AzureFunctions: Extracted ServiceBus destination name '{DestinationName}' from binding metadata", destinationName);
                        }
                    }
                }

                if (tracer.InternalActiveScope == null)
                {
                    // This is the root scope
                    tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
                    scope = tracer.StartActiveInternal(OperationName, tags: tags, parent: extractedContext.SpanContext, links: extractedContext.Links);
                }
                else
                {
                    // shouldn't be hit, but better safe than sorry
                    scope = tracer.StartActiveInternal(OperationName);
                    var rootSpan = scope.Root.Span;
                    AzureFunctionsTags.SetRootSpanTags(
                        rootSpan,
                        shortName: functionName,
                        fullName: context.FunctionDefinition.EntryPoint,
                        bindingSource: rootSpan.Tags is AzureFunctionsTags t ? t.BindingSource : null,
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

        private static PropagationContext ExtractPropagatedContextFromServiceBus<T>(T context, string? bindingName)
            where T : IFunctionContext
        {
            try
            {
                Log.Information("AzureFunctions: === ServiceBus Context Extraction Debug ===");
                Log.Information("AzureFunctions: Function='{FunctionName}', BindingName='{BindingName}'", context.FunctionDefinition.Name, bindingName);

                // Get bindings feature inline
                if (context.Features == null)
                {
                    Log.Warning("AzureFunctions: context.Features is null - no ServiceBus context available");
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
                    Log.Warning("AzureFunctions: IFunctionBindingsFeature not found - no ServiceBus context available");
                    return default;
                }

                // Extract contexts inline
                var triggerMetadata = bindingsFeature.Value.TriggerMetadata;
                var spanContexts = new List<SpanContext>();

                var triggerMetadataCount = triggerMetadata?.Count ?? 0;
                Log.Information<int>("AzureFunctions: TriggerMetadata has {Count} items", triggerMetadataCount);

                // COMPREHENSIVE TRIGGERMETADATA ANALYSIS - Print ALL keys and values
                if (triggerMetadata != null)
                {
                    Log.Information("=== COMPLETE TRIGGERMETADATA DUMP START ===");
                    foreach (var item in triggerMetadata)
                    {
                        try
                        {
                            var key = item.Key;
                            var value = item.Value;
                            var valueType = value?.GetType()?.FullName ?? "null";
                            var valueString = value?.ToString() ?? "null";

                            Log.Information("TriggerMetadata[{Key}] = {Value} (Type: {Type})", key, valueString, valueType);

                            // For complex objects, try JSON serialization
                            if (value != null && value.GetType() != typeof(string) && !value.GetType().IsPrimitive)
                            {
                                try
                                {
                                    var jsonValue = JsonConvert.SerializeObject(value, Formatting.Indented);
                                    if (jsonValue.Length > 50 && jsonValue != valueString)
                                    {
                                        Log.Information("TriggerMetadata[{Key}] JSON: {JsonValue}", key, jsonValue);
                                    }
                                }
                                catch
                                {
                                    // Ignore JSON serialization errors
                                }
                            }
                        }
                        catch (Exception itemEx)
                        {
                            Log.Error(itemEx, "Error analyzing TriggerMetadata item: {Key}", item.Key);
                        }
                    }

                    Log.Information("=== COMPLETE TRIGGERMETADATA DUMP END ===");
                }

                // Extract from single message UserProperties
                if (triggerMetadata?.TryGetValue("UserProperties", out var singlePropsObj) == true)
                {
                    Log.Information("AzureFunctions: Found UserProperties");
                    if (TryParseJson<Dictionary<string, object>>(singlePropsObj) is { } singleProps)
                    {
                        Log.Information<int>("AzureFunctions: Parsed UserProperties with {Count} properties", singleProps.Count);
                        foreach (var prop in singleProps)
                        {
                            var isTracingHeader = prop.Key.StartsWith("x-datadog-") || prop.Key.StartsWith("traceparent") || prop.Key.StartsWith("tracestate");
                            if (isTracingHeader)
                            {
                                Log.Information("AzureFunctions: TRACING HEADER {Key}: {Value}", prop.Key, prop.Value);
                            }
                            else
                            {
                                Log.Information("AzureFunctions: Property {Key} found", prop.Key);
                            }
                        }

                        if (ExtractSpanContextFromProperties(singleProps) is { } singleContext)
                        {
                            Log.Information("AzureFunctions: Successfully extracted SpanContext from UserProperties - TraceId: {TraceId}", singleContext.TraceId128);
                            spanContexts.Add(singleContext);
                        }
                        else
                        {
                            Log.Warning("AzureFunctions: Failed to extract SpanContext from UserProperties");
                        }
                    }
                    else
                    {
                        Log.Warning("AzureFunctions: Failed to parse UserProperties as JSON");
                    }
                }

                // Extract from batch UserPropertiesArray
                if (triggerMetadata?.TryGetValue("UserPropertiesArray", out var arrayPropsObj) == true)
                {
                    Log.Information("AzureFunctions: Found UserPropertiesArray");
                    if (TryParseJson<Dictionary<string, object>[]>(arrayPropsObj) is { } propsArray)
                    {
                        Log.Information<int>("AzureFunctions: Parsed UserPropertiesArray with {Count} messages", propsArray.Length);
                        for (int i = 0; i < propsArray.Length; i++)
                        {
                            Log.Information<int, int>("AzureFunctions: Message {Index} has {Count} properties", i, propsArray[i].Count);
                            if (ExtractSpanContextFromProperties(propsArray[i]) is { } batchContext)
                            {
                                Log.Information<int>("AzureFunctions: Successfully extracted SpanContext from batch message {Index}", i);
                                spanContexts.Add(batchContext);
                            }
                            else
                            {
                                Log.Warning<int>("AzureFunctions: Failed to extract SpanContext from batch message {Index}", i);
                            }
                        }
                    }
                    else
                    {
                        Log.Warning("AzureFunctions: Failed to parse UserPropertiesArray as JSON");
                    }
                }

                // Also try ApplicationProperties for backward compatibility
                if (triggerMetadata?.TryGetValue("ApplicationProperties", out var appPropsObj) == true)
                {
                    Log.Information("AzureFunctions: Found ApplicationProperties");
                    if (TryParseJson<Dictionary<string, object>>(appPropsObj) is { } appProps)
                    {
                        Log.Information<int>("AzureFunctions: Parsed ApplicationProperties with {Count} properties", appProps.Count);
                    }
                }

                Log.Information<int>("AzureFunctions: Extracted {Count} SpanContext(s) from ServiceBus metadata", spanContexts.Count);

                if (spanContexts.Count == 0)
                {
                    Log.Warning("AzureFunctions: No SpanContext found in ServiceBus metadata - returning default context");
                    return default;
                }

                // Create propagation context inline
                bool shouldReparent = spanContexts.Count == 1 ||
                                     (spanContexts.Count > 1 && AreAllContextsIdentical(spanContexts));

                Log.Information("AzureFunctions: Should reparent: {ShouldReparent}", shouldReparent);

                return shouldReparent
                    ? new PropagationContext(spanContexts[0], Baggage.Current, null)
                    : new PropagationContext(null, Baggage.Current, spanContexts.Select(ctx => new SpanLink(ctx)).ToList());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting propagated context from ServiceBus binding");
                return default;
            }
        }

        private static SpanContext? ExtractSpanContextFromProperties(Dictionary<string, object> userProperties)
        {
            var headerAdapter = new ServiceBusUserPropertiesHeadersCollection(userProperties);
            var extractedContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(headerAdapter);
            return extractedContext.SpanContext;
        }

        private static T? TryParseJson<T>(object? jsonObj)
            where T : class
        {
            if (jsonObj is not string jsonString)
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(jsonString);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to parse JSON: {Json}", jsonString);
                return null;
            }
        }

        private static bool AreAllContextsIdentical(List<SpanContext> contexts)
        {
            if (contexts.Count <= 1)
            {
                return true;
            }

            var first = contexts[0];
            return contexts.All(ctx =>
                ctx.TraceId128.Equals(first.TraceId128) &&
                ctx.SpanId == first.SpanId);
        }

        private static string? ExtractServiceBusMessageId<T>(T context, string? bindingName)
            where T : IFunctionContext
        {
            try
            {
                // Get bindings feature
                if (context.Features == null)
                {
                    return null;
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
                    return null;
                }

                var triggerMetadata = bindingsFeature.Value.TriggerMetadata;

                // Extract message ID for single message only (not for batches)
                string? messageId = null;
                if (triggerMetadata?.TryGetValue("MessageId", out var msgIdObj) == true && msgIdObj is string msgId)
                {
                    messageId = msgId;
                }

                // Don't extract messageId if this is a batch (UserPropertiesArray indicates batch)
                else if (triggerMetadata?.ContainsKey("UserPropertiesArray") == true)
                {
                    messageId = null; // Explicitly null for batch triggers
                }

                return messageId;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error extracting ServiceBus message ID");
                return null;
            }
        }

        private static string? ExtractServiceBusDestinationName<T>(T context, string? bindingName)
            where T : IFunctionContext
        {
            try
            {
                // FIRST: Try to extract from function binding definition (most reliable)
                var destinationFromBinding = ExtractDestinationFromBindingDefinition(context);
                if (!string.IsNullOrEmpty(destinationFromBinding))
                {
                    Log.Information("AzureFunctions: Extracted destination from binding definition: '{Destination}'", destinationFromBinding);
                    return destinationFromBinding;
                }

                if (context.Features == null)
                {
                    return null;
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
                    return null;
                }

                var triggerMetadata = bindingsFeature.Value.TriggerMetadata;

                // Try various metadata keys for destination name
                string? destinationName = null;

                // Try queueName first (for queue triggers)
                if (triggerMetadata?.TryGetValue("queueName", out var queueNameObj) == true && queueNameObj is string queueName)
                {
                    destinationName = queueName;
                    Log.Information("AzureFunctions: Found queueName='{QueueName}' in TriggerMetadata", queueName);
                }

                // Try topicName (for topic triggers)
                else if (triggerMetadata?.TryGetValue("topicName", out var topicNameObj) == true && topicNameObj is string topicName)
                {
                    destinationName = topicName;
                    Log.Information("AzureFunctions: Found topicName='{TopicName}' in TriggerMetadata", topicName);
                }

                // Try entityPath
                else if (triggerMetadata?.TryGetValue("entityPath", out var entityPathObj) == true && entityPathObj is string entityPath)
                {
                    destinationName = entityPath;
                    Log.Information("AzureFunctions: Found entityPath='{EntityPath}' in TriggerMetadata", entityPath);
                }

                // Try to extract ServiceBusMetadata from TriggerMetadata.Metadata property
                // Based on Azure SDK source: ServiceBusMetadata serviceBusMetadata = JsonConvert.DeserializeObject<ServiceBusMetadata>(triggerMetadata.Metadata.ToString());
                if (triggerMetadata?.TryGetValue("Metadata", out var metadataObj) == true && metadataObj != null)
                {
                    try
                    {
                        var metadataString = metadataObj.ToString();
                        Log.Information("AzureFunctions: Raw ServiceBus Metadata JSON: {Metadata}", metadataString);

                        // Try to parse as ServiceBusMetadata (following Azure SDK pattern)
                        var serviceBusMetadata = JsonConvert.DeserializeObject<ServiceBusMetadataInfo>(metadataString ?? string.Empty);
                        if (serviceBusMetadata != null)
                        {
                            Log.Information("AzureFunctions: ServiceBusMetadata - Type: {Type}, QueueName: {QueueName}", serviceBusMetadata.Type, serviceBusMetadata.QueueName);
                            Log.Information("AzureFunctions: ServiceBusMetadata - TopicName: {TopicName}, IsSessionsEnabled: {IsSessionsEnabled}", serviceBusMetadata.TopicName, serviceBusMetadata.IsSessionsEnabled);

                            // Prefer queue name if available, then topic name
                            if (!string.IsNullOrEmpty(serviceBusMetadata.QueueName))
                            {
                                destinationName = serviceBusMetadata.QueueName;
                                Log.Information("AzureFunctions: Using QueueName from ServiceBusMetadata: '{QueueName}'", destinationName);
                            }
                            else if (!string.IsNullOrEmpty(serviceBusMetadata.TopicName))
                            {
                                destinationName = serviceBusMetadata.TopicName;
                                Log.Information("AzureFunctions: Using TopicName from ServiceBusMetadata: '{TopicName}'", destinationName);
                            }
                        }
                    }
                    catch (Exception metadataEx)
                    {
                        Log.Debug(metadataEx, "Error parsing ServiceBusMetadata JSON: {Metadata}", metadataObj.ToString());
                    }
                }

                // FALLBACK: Try to parse Client identifier for queue/topic information
                if (string.IsNullOrEmpty(destinationName) && triggerMetadata?.TryGetValue("Client", out var clientObj) == true && clientObj != null)
                {
                    try
                    {
                        var clientJson = clientObj.ToString();
                        Log.Information("AzureFunctions: Raw ServiceBus Client JSON: {ClientJson}", clientJson);

                        // Try to parse as ServiceBusClient info
                        var clientInfo = JsonConvert.DeserializeObject<ServiceBusClientInfo>(clientJson ?? string.Empty);
                        if (clientInfo != null)
                        {
                            Log.Information("AzureFunctions: ServiceBusClient - FullyQualifiedNamespace: {Namespace}", clientInfo.FullyQualifiedNamespace);
                            Log.Information("AzureFunctions: ServiceBusClient - Identifier: {Identifier}", clientInfo.Identifier);
                            Log.Information<bool, int>("AzureFunctions: ServiceBusClient - IsClosed: {IsClosed}, TransportType: {TransportType}", clientInfo.IsClosed, clientInfo.TransportType);
                            // Try to extract entity information from identifier
                            destinationName = ParseEntityFromClientIdentifier(clientInfo.Identifier, clientInfo.FullyQualifiedNamespace);

                            if (!string.IsNullOrEmpty(destinationName))
                            {
                                Log.Information("AzureFunctions: Extracted destination from Client identifier: '{DestinationName}'", destinationName);
                            }
                            else
                            {
                                Log.Warning("AzureFunctions: Could not extract entity information from Client identifier");
                            }
                        }
                    }
                    catch (Exception clientEx)
                    {
                        Log.Debug(clientEx, "Error parsing ServiceBus Client JSON: {Client}", clientObj.ToString());
                    }
                }

                return destinationName;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error extracting ServiceBus destination name");
                return null;
            }
        }

        private static void LogServiceBusBindingDebugInfo<T>(T context, string? bindingName)
            where T : IFunctionContext
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== Azure Functions ServiceBus Binding Debug Info ===");
                sb.AppendLine($"FunctionContext Type: {context.GetType().FullName}");
                sb.AppendLine($"Function Name: {context.FunctionDefinition.Name}");
                sb.AppendLine($"Function EntryPoint: {context.FunctionDefinition.EntryPoint}");
                sb.AppendLine($"Binding Name: {bindingName}");

                // Log FunctionDefinition InputBindings
                sb.AppendLine("InputBindings:");
                if (context.FunctionDefinition.InputBindings is not null)
                {
                    try
                    {
                        foreach (var entry in context.FunctionDefinition.InputBindings)
                        {
                            if (entry is DictionaryEntry dictEntry)
                            {
                                var key = dictEntry.Key;
                                var value = dictEntry.Value;
                                sb.AppendLine($"  Key: {key} (Type: {key?.GetType()?.Name ?? "null"})");
                                sb.AppendLine($"  Value: {value} (Type: {value?.GetType()?.FullName ?? "null"})");

                                if (value is not null && value.TryDuckCast<BindingMetadata>(out var bindingMeta))
                                {
                                    sb.AppendLine($"    BindingType: {bindingMeta.BindingType}");
                                    sb.AppendLine($"    Direction: {bindingMeta.Direction}");

                                    // If this is a ServiceBus trigger, try to extract more details
                                    if (bindingMeta.BindingType?.Equals("serviceBusTrigger", StringComparison.OrdinalIgnoreCase) == true)
                                    {
                                        sb.AppendLine($"    *** SERVICEBUS TRIGGER FOUND ***");
                                        sb.AppendLine($"    Raw Value Type: {value.GetType().FullName}");
                                        sb.AppendLine($"    Raw Value: {value}");

                                        // Try to access properties via reflection since we don't have duck types
                                        var valueType = value.GetType();
                                        var properties = valueType.GetProperties();
                                        foreach (var prop in properties)
                                        {
                                            try
                                            {
                                                var propValue = prop.GetValue(value);
                                                sb.AppendLine($"    Property {prop.Name}: {propValue} (Type: {propValue?.GetType()?.Name})");
                                            }
                                            catch (Exception ex)
                                            {
                                                sb.AppendLine($"    Property {prop.Name}: Error - {ex.Message}");
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                sb.AppendLine($"  Non-DictionaryEntry: {entry?.GetType()?.FullName ?? "null"}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  Error iterating InputBindings: {ex.Message}");
                    }
                }

                // Log Features
                sb.AppendLine("Features:");
                if (context.Features is not null)
                {
                    foreach (var feature in context.Features)
                    {
                        sb.AppendLine($"  Key: {feature.Key.FullName}");
                        sb.AppendLine($"  Value: {feature.Value?.GetType()?.FullName ?? "null"}");

                        // If this is IFunctionBindingsFeature, log its contents
                        if (feature.Key.FullName?.Equals("Microsoft.Azure.Functions.Worker.Context.Features.IFunctionBindingsFeature") == true
                            && feature.Value?.TryDuckCast<GrpcBindingsFeatureStruct>(out var bindingsFeature) == true)
                        {
                            sb.AppendLine("    IFunctionBindingsFeature Details:");

                            // TriggerMetadata
                            sb.AppendLine($"    TriggerMetadata Count: {bindingsFeature.TriggerMetadata?.Count ?? 0}");
                            if (bindingsFeature.TriggerMetadata is not null)
                            {
                                foreach (var tm in bindingsFeature.TriggerMetadata)
                                {
                                    var valueInfo = tm.Value?.GetType()?.Name ?? "null";

                                    // Special handling for UserProperties and ApplicationProperties
                                    if (tm.Key.Equals("UserProperties", StringComparison.OrdinalIgnoreCase) ||
                                        tm.Key.Equals("ApplicationProperties", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (tm.Value is string jsonString)
                                        {
                                            sb.AppendLine($"      {tm.Key}: {jsonString} (JSON String)");
                                            try
                                            {
                                                var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
                                                if (parsed != null)
                                                {
                                                    sb.AppendLine($"        Parsed as Dictionary with {parsed.Count} properties:");
                                                    foreach (var prop in parsed)
                                                    {
                                                        sb.AppendLine($"          {prop.Key}: {prop.Value} (Type: {prop.Value?.GetType()?.Name})");
                                                    }
                                                }
                                            }
                                            catch (Exception parseEx)
                                            {
                                                sb.AppendLine($"        Failed to parse as JSON: {parseEx.Message}");
                                            }
                                        }
                                        else
                                        {
                                            sb.AppendLine($"      {tm.Key}: {tm.Value} (Type: {valueInfo})");
                                        }
                                    }

                                    // Special handling for UserPropertiesArray (batch scenarios)
                                    else if (tm.Key.Equals("UserPropertiesArray", StringComparison.OrdinalIgnoreCase) ||
                                             tm.Key.Equals("ApplicationPropertiesArray", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (tm.Value is string jsonArrayString)
                                        {
                                            sb.AppendLine($"      {tm.Key}: {jsonArrayString} (JSON Array String)");
                                            try
                                            {
                                                var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(jsonArrayString);
                                                if (parsed != null)
                                                {
                                                    sb.AppendLine($"        Parsed as Array with {parsed.Length} elements:");
                                                    for (int i = 0; i < parsed.Length; i++)
                                                    {
                                                        sb.AppendLine($"        Element {i} with {parsed[i].Count} properties:");
                                                        foreach (var prop in parsed[i])
                                                        {
                                                            sb.AppendLine($"          {prop.Key}: {prop.Value} (Type: {prop.Value?.GetType()?.Name})");
                                                        }
                                                    }
                                                }
                                            }
                                            catch (Exception parseEx)
                                            {
                                                sb.AppendLine($"        Failed to parse as JSON array: {parseEx.Message}");
                                            }
                                        }
                                        else
                                        {
                                            sb.AppendLine($"      {tm.Key}: {tm.Value} (Type: {valueInfo})");
                                        }
                                    }

                                    // Special handling for ServiceBusMetadata in Metadata field
                                    else if (tm.Key.Equals("Metadata", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (tm.Value != null)
                                        {
                                            var metadataString = tm.Value.ToString();
                                            sb.AppendLine($"      {tm.Key}: {metadataString} (ServiceBus Metadata JSON)");
                                            try
                                            {
                                                var serviceBusMetadata = JsonConvert.DeserializeObject<ServiceBusMetadataInfo>(metadataString ?? string.Empty);
                                                if (serviceBusMetadata != null)
                                                {
                                                    sb.AppendLine($"        Parsed ServiceBusMetadata:");
                                                    sb.AppendLine($"          Type: {serviceBusMetadata.Type}");
                                                    sb.AppendLine($"          QueueName: {serviceBusMetadata.QueueName}");
                                                    sb.AppendLine($"          TopicName: {serviceBusMetadata.TopicName}");
                                                    sb.AppendLine($"          SubscriptionName: {serviceBusMetadata.SubscriptionName}");
                                                    sb.AppendLine($"          IsSessionsEnabled: {serviceBusMetadata.IsSessionsEnabled}");
                                                    sb.AppendLine($"          Cardinality: {serviceBusMetadata.Cardinality}");
                                                }
                                            }
                                            catch (Exception parseEx)
                                            {
                                                sb.AppendLine($"        Failed to parse as ServiceBusMetadata: {parseEx.Message}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        sb.AppendLine($"      {tm.Key}: {tm.Value} (Type: {valueInfo})");
                                    }
                                }
                            }

                            // InputData
                            sb.AppendLine($"    InputData Count: {bindingsFeature.InputData?.Count ?? 0}");
                            if (bindingsFeature.InputData is not null)
                            {
                                foreach (var id in bindingsFeature.InputData)
                                {
                                    sb.AppendLine($"      {id.Key}: {id.Value?.GetType()?.Name ?? "null"}");
                                }
                            }

                            // OutputBindingData
                            sb.AppendLine($"    OutputBindingData Count: {bindingsFeature.OutputBindingData?.Count ?? 0}");
                            if (bindingsFeature.OutputBindingData is not null)
                            {
                                foreach (var obd in bindingsFeature.OutputBindingData)
                                {
                                    sb.AppendLine($"      {obd.Key}: {obd.Value?.GetType()?.Name ?? "null"}");
                                }
                            }

                            // OutputBindingsInfo
                            sb.AppendLine($"    OutputBindingsInfo: {bindingsFeature.OutputBindingsInfo?.GetType()?.FullName ?? "null"}");

                            // InvocationResult
                            sb.AppendLine($"    InvocationResult: {bindingsFeature.InvocationResult?.GetType()?.FullName ?? "null"}");
                        }
                    }
                }

                // Note: Items property not available in duck type interface

                var debugOutput = sb.ToString();
                Log.Information("{AzureFunctionsDebugInfo}", debugOutput);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AzureFunctions: Error logging ServiceBus binding debug info");
            }
        }

        private static string? ExtractDestinationFromBindingDefinition<T>(T context)
            where T : IFunctionContext
        {
            try
            {
                Log.Information("AzureFunctions: === Analyzing Function Binding Definition for ServiceBus destination ===");
                Log.Information("AzureFunctions: Function Name: {FunctionName}", context.FunctionDefinition.Name);
                Log.Information("AzureFunctions: Function EntryPoint: {EntryPoint}", context.FunctionDefinition.EntryPoint);

                // Try to extract queue name from the EntryPoint method via reflection
                var queueNameFromReflection = ExtractQueueNameFromFunctionMethod(context.FunctionDefinition.EntryPoint);
                if (!string.IsNullOrEmpty(queueNameFromReflection))
                {
                    Log.Information("AzureFunctions: *** FOUND DESTINATION from method reflection: '{QueueName}' ***", queueNameFromReflection);
                    return queueNameFromReflection;
                }

                // The queue/topic name should be in the ServiceBus trigger attribute
                // Look through the function definition for ServiceBus trigger configuration
                if (context.FunctionDefinition.InputBindings is not null)
                {
                    Log.Information<int>("AzureFunctions: Found {Count} input bindings", context.FunctionDefinition.InputBindings.Count);

                    foreach (var entry in context.FunctionDefinition.InputBindings)
                    {
                        if (entry is DictionaryEntry dictEntry && dictEntry.Value != null)
                        {
                            Log.Information(
                                "AzureFunctions: Checking binding - Key: '{Key}', Value Type: {ValueType}",
                                dictEntry.Key,
                                dictEntry.Value.GetType().FullName);

                            if (dictEntry.Value.TryDuckCast<BindingMetadata>(out var bindingMeta))
                            {
                                Log.Information(
                                    "AzureFunctions: Binding metadata - Type: '{BindingType}', Direction: {Direction}",
                                    bindingMeta.BindingType,
                                    bindingMeta.Direction);

                                if (bindingMeta.BindingType?.Equals("serviceBusTrigger", StringComparison.OrdinalIgnoreCase) == true)
                                {
                                    Log.Information("AzureFunctions: *** FOUND SERVICEBUS TRIGGER BINDING ***");

                                    // Found ServiceBus trigger, now extract the queue/topic name from the raw binding data
                                    var rawValue = dictEntry.Value;
                                    Log.Information("AzureFunctions: Raw binding value type: {Type}", rawValue.GetType().FullName);

                                    // Try to extract queue/topic name from ALL binding properties
                                    var valueType = rawValue.GetType();
                                    var properties = valueType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                                    Log.Information<int>("AzureFunctions: Found {Count} properties on binding object", properties.Length);

                                    // First, log all properties without filtering
                                    foreach (var prop in properties)
                                    {
                                        try
                                        {
                                            var propValue = prop.GetValue(rawValue);
                                            var propValueStr = propValue?.ToString();

                                            Log.Information(
                                                "AzureFunctions: Property '{PropertyName}' = '{Value}' (Type: {Type})",
                                                prop.Name,
                                                propValueStr,
                                                prop.PropertyType.Name);
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Debug(ex, "Error accessing property {Property}", prop.Name);
                                        }
                                    }

                                    // Now analyze properties for queue/topic name
                                    foreach (var prop in properties)
                                    {
                                        try
                                        {
                                            var propValue = prop.GetValue(rawValue);
                                            var propName = prop.Name.ToLowerInvariant();

                                            // Check for direct queue/topic name properties
                                            if (propName.Contains("queue") || propName.Contains("topic") || propName.Contains("entity") ||
                                                propName.Contains("path"))
                                            {
                                                if (propValue is string strValue && !string.IsNullOrEmpty(strValue) &&
                                                    !strValue.Equals("serviceBusTrigger", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    Log.Information("AzureFunctions: *** FOUND DESTINATION in property '{Property}': '{Value}' ***", prop.Name, strValue);
                                                    return strValue;
                                                }
                                            }

                                            // Skip parameter name (that's not the queue/topic name)
                                            if (propName.Equals("name", StringComparison.OrdinalIgnoreCase))
                                            {
                                                Log.Information("AzureFunctions: Skipping parameter name property: '{Property}' = '{Value}'", prop.Name, propValue);
                                                continue;
                                            }

                                            // Some bindings might have the queue/topic name as the first parameter
                                            // in a configuration object or array
                                            if (propName.Contains("parameter") || propName.Contains("config") || propName.Contains("arg") ||
                                                propName.Contains("data") || propName.Contains("source") || propName.Contains("metadata"))
                                            {
                                                // Try to extract from configuration structures
                                                if (propValue != null)
                                                {
                                                    Log.Information("AzureFunctions: Analyzing config property '{Property}' for queue/topic name", prop.Name);
                                                    var extracted = TryExtractFromConfigurationObject(propValue);
                                                    if (extracted != null && !extracted.Equals("msg", StringComparison.OrdinalIgnoreCase) &&
                                                        !extracted.Equals("serviceBusTrigger", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        Log.Information("AzureFunctions: *** FOUND DESTINATION in config object: '{Value}' ***", extracted);
                                                        return extracted;
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Debug(ex, "Error accessing property {Property}", prop.Name);
                                        }
                                    }

                                    // If we found the ServiceBus trigger but couldn't extract the destination,
                                    // try to get it from the binding key itself (parameter name)
                                    if (dictEntry.Key is string bindingKey && !string.IsNullOrEmpty(bindingKey))
                                    {
                                        Log.Information("AzureFunctions: ServiceBus trigger found but no destination in properties. Binding key: '{Key}'", bindingKey);

                                        // Sometimes the binding key might give us a hint about the entity
                                        // but usually it's just the parameter name like "message" or "msg"
                                    }
                                }
                            }
                            else
                            {
                                Log.Information("AzureFunctions: Could not duck cast to BindingMetadata for key '{Key}'", dictEntry.Key);
                            }
                        }
                        else
                        {
                            Log.Information("AzureFunctions: Skipping null or non-DictionaryEntry binding");
                        }
                    }
                }
                else
                {
                    Log.Warning("AzureFunctions: No input bindings found in function definition");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting destination from binding definition");
            }

            Log.Information("AzureFunctions: Could not extract ServiceBus destination from binding definition");
            return null;
        }

        private static string? TryExtractFromConfigurationObject(object configObj)
        {
            try
            {
                // Handle common configuration patterns
                if (configObj is string strConfig && !string.IsNullOrEmpty(strConfig))
                {
                    return strConfig;
                }

                // Handle arrays (first element might be queue/topic name)
                if (configObj is System.Collections.IEnumerable enumerable && configObj is not string)
                {
                    foreach (var item in enumerable)
                    {
                        if (item is string strItem && !string.IsNullOrEmpty(strItem))
                        {
                            return strItem;
                        }

                        break; // Only check first item
                    }
                }

                // Handle objects with properties
                var objType = configObj.GetType();
                if (!objType.IsPrimitive && objType != typeof(string))
                {
                    var props = objType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var prop in props)
                    {
                        if (prop.PropertyType == typeof(string))
                        {
                            var value = prop.GetValue(configObj) as string;
                            if (!string.IsNullOrEmpty(value))
                            {
                                return value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error extracting from configuration object");
            }

            return null;
        }

        private static string? ExtractQueueNameFromFunctionMethod(string? entryPoint)
        {
            try
            {
                if (string.IsNullOrEmpty(entryPoint))
                {
                    return null;
                }

                Log.Information("AzureFunctions: Attempting to extract queue name from method: {EntryPoint}", entryPoint);

                // EntryPoint format is typically: "Namespace.ClassName.MethodName"
                // We need to find the method and inspect its ServiceBusTrigger attribute
                var parts = entryPoint!.Split('.');
                if (parts.Length < 3)
                {
                    Log.Information("AzureFunctions: EntryPoint format unexpected, expected at least 3 parts: {Parts}", string.Join(".", parts));
                    return null;
                }

                var methodName = parts[parts.Length - 1];
                var className = string.Join(".", parts.Take(parts.Length - 1));

                Log.Information("AzureFunctions: Parsed EntryPoint - Class: '{ClassName}', Method: '{MethodName}'", className, methodName);

                // Try to find the type using reflection
                Type? functionType = null;

                // Search through all loaded assemblies for the type
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        functionType = assembly.GetType(className);
                        if (functionType != null)
                        {
                            Log.Information("AzureFunctions: Found function type: {TypeName} in assembly: {AssemblyName}", functionType.FullName, assembly.FullName);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Error getting type {ClassName} from assembly {AssemblyName}", className, assembly.FullName);
                    }
                }

                if (functionType == null)
                {
                    Log.Information("AzureFunctions: Could not find function type: {ClassName}", className);
                    return null;
                }

                // Find the method
                var method = functionType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                if (method == null)
                {
                    Log.Information("AzureFunctions: Could not find method: {MethodName} in type: {TypeName}", methodName, functionType.FullName);
                    return null;
                }

                Log.Information("AzureFunctions: Found method: {MethodName}, analyzing parameters", methodName);

                // Check method parameters for ServiceBusTrigger attribute
                var parameters = method.GetParameters();
                Log.Information<int>("AzureFunctions: Method has {Count} parameters", parameters.Length);

                foreach (var parameter in parameters)
                {
                    Log.Information("AzureFunctions: Parameter: {Name} (Type: {Type})", parameter.Name, parameter.ParameterType.Name);

                    var attributes = parameter.GetCustomAttributes(false);
                    foreach (var attribute in attributes)
                    {
                        var attributeType = attribute.GetType();
                        Log.Information("AzureFunctions: Parameter attribute: {AttributeType}", attributeType.FullName);

                        // Check if this is a ServiceBusTrigger attribute
                        if (attributeType.Name == "ServiceBusTriggerAttribute" ||
                            attributeType.FullName?.Contains("ServiceBusTrigger") == true)
                        {
                            Log.Information("AzureFunctions: *** FOUND ServiceBusTrigger attribute! ***");

                            // Try to get the queue/topic name from the attribute
                            var queueName = ExtractQueueNameFromServiceBusTriggerAttribute(attribute);
                            if (!string.IsNullOrEmpty(queueName))
                            {
                                Log.Information("AzureFunctions: *** EXTRACTED QUEUE NAME from ServiceBusTrigger: '{QueueName}' ***", queueName);
                                return queueName;
                            }
                        }
                    }
                }

                Log.Information("AzureFunctions: No ServiceBusTrigger attribute found in method parameters");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting queue name from function method: {EntryPoint}", entryPoint);
            }

            return null;
        }

        private static string? ExtractQueueNameFromServiceBusTriggerAttribute(object serviceBusTriggerAttribute)
        {
            try
            {
                var attributeType = serviceBusTriggerAttribute.GetType();
                Log.Information("AzureFunctions: Analyzing ServiceBusTrigger attribute type: {Type}", attributeType.FullName);

                // ServiceBusTriggerAttribute typically has properties like:
                // - QueueName or EntityName (for queue)
                // - TopicName (for topic)
                // Or constructor parameters

                // Try to get properties first
                var properties = attributeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                Log.Information<int>("AzureFunctions: ServiceBusTrigger attribute has {Count} properties", properties.Length);

                foreach (var property in properties)
                {
                    var propValue = property.GetValue(serviceBusTriggerAttribute);
                    Log.Information(
                        "AzureFunctions: ServiceBusTrigger property '{Name}' = '{Value}' (Type: {Type})",
                        property.Name,
                        propValue,
                        property.PropertyType.Name);

                    var propName = property.Name.ToLowerInvariant();
                    if ((propName.Contains("queue") || propName.Contains("topic") || propName.Contains("entity")) &&
                        propValue is string strValue && !string.IsNullOrEmpty(strValue))
                    {
                        Log.Information("AzureFunctions: *** FOUND QUEUE/TOPIC NAME in property '{Property}': '{Value}' ***", property.Name, strValue);
                        return strValue;
                    }
                }

                // If no properties found, try to access constructor arguments via reflection
                // This is more complex and might not always work, but the first constructor parameter
                // of ServiceBusTriggerAttribute is usually the queue/topic name
                var fields = attributeType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                foreach (var field in fields)
                {
                    var fieldValue = field.GetValue(serviceBusTriggerAttribute);
                    Log.Information(
                        "AzureFunctions: ServiceBusTrigger field '{Name}' = '{Value}' (Type: {Type})",
                        field.Name,
                        fieldValue,
                        field.FieldType.Name);

                    if (fieldValue is string strFieldValue && !string.IsNullOrEmpty(strFieldValue) &&
                        !strFieldValue.Equals("ServiceBusConnection", StringComparison.OrdinalIgnoreCase))
                    {
                        var fieldName = field.Name.ToLowerInvariant();
                        if (fieldName.Contains("queue") || fieldName.Contains("topic") || fieldName.Contains("entity") ||
                            fieldName.Contains("name") || strFieldValue.Length > 3)
                        {
                            Log.Information("AzureFunctions: *** FOUND QUEUE/TOPIC NAME in field '{Field}': '{Value}' ***", field.Name, strFieldValue);
                            return strFieldValue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting queue name from ServiceBusTrigger attribute");
            }

            return null;
        }

        private static string? ParseEntityFromClientIdentifier(string? identifier, string? fullyQualifiedNamespace)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return null;
            }

            try
            {
                Log.Information("AzureFunctions: Parsing Client identifier for entity info: '{Identifier}'", identifier);

                // Common patterns we might see in ServiceBus client identifiers:
                // Pattern 1: "namespace-guid" (no entity info)
                // Pattern 2: "namespace/entity-guid" (contains entity)
                // Pattern 3: "namespace:entity-guid" (contains entity)
                // Pattern 4: Could contain queue or topic name in various formats

                var namespacePart = fullyQualifiedNamespace?.Replace(".servicebus.windows.net", string.Empty);

                // Try different separators
                var separators = new[] { "/", ":", "-", "_" };

                foreach (var separator in separators)
                {
                    if (identifier!.Contains(separator))
                    {
                        var parts = identifier!.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
                        Log.Information<string, int, string>(
                            "AzureFunctions: Split identifier by '{Separator}' into {Count} parts: [{Parts}]",
                            separator,
                            parts.Length,
                            string.Join(", ", parts));
                        // Look for parts that might be entity names (not GUIDs, not namespace)
                        foreach (var part in parts)
                        {
                            // Skip if it's the namespace part
                            if (!string.IsNullOrEmpty(namespacePart) && part.Contains(namespacePart))
                            {
                                continue;
                            }

                            // Skip if it looks like a GUID (36 chars with dashes)
                            if (part.Length == 36 && part.Count(c => c == '-') == 4)
                            {
                                Log.Information("AzureFunctions: Skipping GUID part: '{Part}'", part);
                                continue;
                            }

                            // Skip if it's too short to be meaningful
                            if (part.Length < 3)
                            {
                                continue;
                            }

                            // This might be an entity name
                            Log.Information("AzureFunctions: Potential entity name found: '{Part}'", part);
                            return part;
                        }
                    }
                }

                // If no separators worked, log the full identifier for analysis
                Log.Warning("AzureFunctions: Could not parse entity from identifier pattern: '{Identifier}'", identifier);

                // As a last resort, try to extract anything after the namespace
                if (!string.IsNullOrEmpty(namespacePart) && identifier!.StartsWith(namespacePart))
                {
                    var remainder = identifier!.Substring(namespacePart!.Length).Trim('-', '_', '/', ':');
                    if (!string.IsNullOrEmpty(remainder) && remainder.Length > 5)
                    {
                        Log.Information("AzureFunctions: Extracted remainder as potential entity: '{Remainder}'", remainder);
                        return remainder;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error parsing entity from Client identifier: {Identifier}", identifier);
            }

            return null;
        }

        // Helper class to deserialize ServiceBus Client info
        private class ServiceBusClientInfo
        {
            public string? FullyQualifiedNamespace { get; set; }

            public bool IsClosed { get; set; }

            public int TransportType { get; set; }

            public string? Identifier { get; set; }
        }

        // Helper class to deserialize ServiceBusMetadata (matching Azure SDK structure)
        private class ServiceBusMetadataInfo
        {
            public string? Type { get; set; }

            public string? Connection { get; set; }

            public string? QueueName { get; set; }

            public string? TopicName { get; set; }

            public string? SubscriptionName { get; set; }

            public bool IsSessionsEnabled { get; set; }

            public string? Cardinality { get; set; }
        }
    }
}

#endif
