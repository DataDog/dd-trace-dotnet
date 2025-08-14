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
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    internal static class AzureFunctionsCommon
    {
        public const string IntegrationName = nameof(Configuration.IntegrationId.AzureFunctions);

        public const string OperationName = "azure_functions.invoke";
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

                // Try to extract entity path for ServiceBus triggers
                string? entityPath = null;
                if (triggerType == "ServiceBus")
                {
                    Log.Debug("ServiceBus trigger detected, attempting to extract EntityPath");

                    // Try to extract from the BindingSource if it's a ServiceBusTriggerInput
                    entityPath = ExtractEntityPathFromBindingSource(instanceParam.BindingSource);
                    if (!string.IsNullOrEmpty(entityPath))
                    {
                        Log.Information("Successfully extracted EntityPath from BindingSource: {EntityPath}", entityPath);
                    }
                    else
                    {
                        Log.Debug("Could not extract EntityPath from BindingSource, falling back to attribute extraction");
                        // Fallback to extracting from attributes
                        entityPath = ExtractQueueNameFromFunctionMethod(instanceParam.FunctionDescriptor.FullName);
                        if (!string.IsNullOrEmpty(entityPath))
                        {
                            Log.Information("Successfully extracted EntityPath from attributes: {EntityPath}", entityPath);
                        }
                        else
                        {
                            Log.Warning("Could not extract EntityPath from ServiceBus trigger");
                        }
                    }
                }

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

                    // Add ServiceBus-specific tags if we have entity path
                    if (!string.IsNullOrEmpty(entityPath))
                    {
                        rootSpan.SetTag(Tags.MessagingDestinationName, entityPath);
                    }

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

                    // Add ServiceBus-specific tags if we have entity path
                    if (!string.IsNullOrEmpty(entityPath))
                    {
                        tags.MessagingDestinationName = entityPath;
                    }

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

                    // Add ServiceBus-specific tags if we have entity path
                    if (!string.IsNullOrEmpty(entityPath))
                    {
                        scope.Root.Span.SetTag(Tags.MessagingDestinationName, entityPath);
                    }
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
                    bindingName = entry.Key as string;
                    if (triggerType == "Http")
                    {
                        extractedContext = ExtractPropagatedContextFromHttp(context, bindingName).MergeBaggageInto(Baggage.Current);
                    }
                    else if (triggerType == "ServiceBus")
                    {
                        extractedContext = ExtractPropagatedContextFromServiceBus(context, bindingName).MergeBaggageInto(Baggage.Current);
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
                    // Try to extract destination name from binding metadata
                    var destinationName = ExtractQueueNameFromFunctionMethod(context.FunctionDefinition.EntryPoint);
                    if (!string.IsNullOrEmpty(destinationName))
                    {
                        tags.MessagingDestinationName = destinationName;
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
                // Get bindings feature inline
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

                // Extract contexts inline
                var triggerMetadata = bindingsFeature.Value.TriggerMetadata;
                var spanContexts = new List<SpanContext>();

                // Extract from single message UserProperties
                if (triggerMetadata?.TryGetValue("UserProperties", out var singlePropsObj) == true &&
                    TryParseJson<Dictionary<string, object>>(singlePropsObj) is { } singleProps)
                {
                    if (ExtractSpanContextFromProperties(singleProps) is { } singleContext)
                    {
                        spanContexts.Add(singleContext);
                    }
                }

                // Extract from batch UserPropertiesArray
                if (triggerMetadata?.TryGetValue("UserPropertiesArray", out var arrayPropsObj) == true &&
                    TryParseJson<Dictionary<string, object>[]>(arrayPropsObj) is { } propsArray)
                {
                    foreach (var props in propsArray)
                    {
                        if (ExtractSpanContextFromProperties(props) is { } batchContext)
                        {
                            spanContexts.Add(batchContext);
                        }
                    }
                }

                if (spanContexts.Count == 0)
                {
                    return default;
                }

                bool shouldReparent = spanContexts.Count == 1 ||
                                     (spanContexts.Count > 1 && AreAllContextsIdentical(spanContexts));

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

        private static string? ExtractQueueNameFromFunctionMethod(string? entryPoint)
        {
            if (string.IsNullOrEmpty(entryPoint))
            {
                return null;
            }

            return AttributeReflectionHelper.ExtractAttributeProperty(entryPoint!, "ServiceBusTriggerAttribute", "QueueName", "TopicName");
        }

        /// <summary>
        /// Extracts entity path from a binding source that may contain a ServiceBusTriggerInput
        /// </summary>
        /// <param name="bindingSource">The binding source object</param>
        /// <returns>The entity path if found, otherwise null</returns>
        private static string? ExtractEntityPathFromBindingSource(object? bindingSource)
        {
            if (bindingSource == null)
            {
                Log.Debug("ExtractEntityPathFromBindingSource: bindingSource is null");
                return null;
            }

            try
            {
                var bindingSourceType = bindingSource.GetType();
                Log.Debug("ExtractEntityPathFromBindingSource: bindingSource type = {TypeName}", bindingSourceType.FullName);

                // Check if this is a TriggerBindingSource<ServiceBusTriggerInput>
                if (!bindingSourceType.IsGenericType)
                {
                    Log.Debug("ExtractEntityPathFromBindingSource: bindingSource is not a generic type");
                    return null;
                }

                // Try to get the Value property which should contain the ServiceBusTriggerInput
                var valueProperty = bindingSourceType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                if (valueProperty == null)
                {
                    Log.Debug("ExtractEntityPathFromBindingSource: Value property not found on bindingSource");
                    return null;
                }

                var triggerInput = valueProperty.GetValue(bindingSource);
                if (triggerInput == null)
                {
                    Log.Debug("ExtractEntityPathFromBindingSource: Value property returned null");
                    return null;
                }

                // Check if this is a ServiceBusTriggerInput
                var triggerInputTypeName = triggerInput.GetType().FullName;
                Log.Debug("ExtractEntityPathFromBindingSource: triggerInput type = {TypeName}", triggerInputTypeName);

                if (triggerInputTypeName?.Contains("ServiceBusTriggerInput") == true)
                {
                    Log.Debug("ExtractEntityPathFromBindingSource: Found ServiceBusTriggerInput, extracting EntityPath");
                    return ExtractEntityPathFromServiceBusTriggerInput(triggerInput);
                }
                else
                {
                    Log.Debug("ExtractEntityPathFromBindingSource: triggerInput is not ServiceBusTriggerInput");
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "ExtractEntityPathFromBindingSource: Error extracting entity path from binding source");
            }

            return null;
        }

        /// <summary>
        /// Safely explores a ServiceBusTriggerInput instance to extract the EntityPath using duck typing.
        /// </summary>
        /// <param name="serviceBusTriggerInput">The ServiceBusTriggerInput instance to explore</param>
        /// <returns>The entity path if found, otherwise null</returns>
        private static string? ExtractEntityPathFromServiceBusTriggerInput(object? serviceBusTriggerInput)
        {
            if (serviceBusTriggerInput == null)
            {
                return null;
            }

            try
            {
                var triggerInputType = serviceBusTriggerInput.GetType();
                Log.Debug("ExtractEntityPathFromServiceBusTriggerInput: Exploring type: {TypeName}", triggerInputType.FullName);

                // Log available properties and fields for debugging
                Log.Debug("ExtractEntityPathFromServiceBusTriggerInput: Available properties:");
                foreach (var prop in triggerInputType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    Log.Debug("  - {PropertyName} ({PropertyType})", prop.Name, prop.PropertyType.Name);
                }

                // Try to duck type to IServiceBusTriggerInput
                if (!serviceBusTriggerInput.TryDuckCast<IServiceBusTriggerInput>(out var triggerInput))
                {
                    Log.Warning("ExtractEntityPathFromServiceBusTriggerInput: Could not duck cast to IServiceBusTriggerInput. Type: {TypeName}", triggerInputType.FullName);
                    return null;
                }

                Log.Debug("ExtractEntityPathFromServiceBusTriggerInput: Successfully duck casted to IServiceBusTriggerInput");

                // Try to access ReceiveActions
                var receiveActions = triggerInput.ReceiveActions;
                if (receiveActions == null)
                {
                    Log.Debug("ExtractEntityPathFromServiceBusTriggerInput: ReceiveActions is null on ServiceBusTriggerInput");
                    return null;
                }

                var receiveActionsType = receiveActions.GetType();
                Log.Debug("ExtractEntityPathFromServiceBusTriggerInput: ReceiveActions type: {TypeName}", receiveActionsType.FullName);

                // Log available fields for debugging
                Log.Debug("ExtractEntityPathFromServiceBusTriggerInput: ReceiveActions fields:");
                foreach (var field in receiveActionsType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    Log.Debug("  - {FieldName} ({FieldType})", field.Name, field.FieldType.Name);
                }

                // Try to access the internal _receiver field (already duck typed to IServiceBusReceiver)
                var receiver = receiveActions.Receiver;
                if (receiver == null)
                {
                    Log.Warning("ExtractEntityPathFromServiceBusTriggerInput: Receiver (_receiver field) is null on ServiceBusReceiveActions");
                    return null;
                }

                var receiverType = receiver.Instance?.GetType();
                Log.Debug("ExtractEntityPathFromServiceBusTriggerInput: Found receiver of type: {TypeName}", receiverType?.FullName ?? "null");

                var entityPath = receiver.EntityPath;
                Log.Debug("ExtractEntityPathFromServiceBusTriggerInput: EntityPath value = '{EntityPath}'", entityPath ?? "<null>");

                if (!string.IsNullOrEmpty(entityPath))
                {
                    Log.Information("ExtractEntityPathFromServiceBusTriggerInput: Successfully extracted EntityPath: {EntityPath}", entityPath);
                    return entityPath;
                }
                else
                {
                    Log.Warning("ExtractEntityPathFromServiceBusTriggerInput: EntityPath is null or empty on ServiceBusReceiver");
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error extracting EntityPath from ServiceBusTriggerInput");
            }

            return null;
        }
    }
}

#endif
