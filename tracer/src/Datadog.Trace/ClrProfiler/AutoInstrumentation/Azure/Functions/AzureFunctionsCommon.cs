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
                    var messageId = ExtractServiceBusMessageId(context, bindingName);
                    tags.MessagingMessageId = messageId;

                    // Try to extract destination name from binding metadata
                    // TODO: @pmartinez there must be a better way than this to get the queue
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

                // Create propagation context inline
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

        // TODO: @pmartinez I hate this
        private static string? ExtractQueueNameFromFunctionMethod(string? entryPoint)
        {
            try
            {
                if (string.IsNullOrEmpty(entryPoint))
                {
                    return null;
                }

                var parts = entryPoint!.Split('.');
                if (parts.Length < 3)
                {
                    return null;
                }

                var methodName = parts[parts.Length - 1];
                var className = string.Join(".", parts.Take(parts.Length - 1));

                Type? functionType = null;

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        functionType = assembly.GetType(className);
                        if (functionType != null)
                        {
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
                    return null;
                }

                var method = functionType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                if (method == null)
                {
                    return null;
                }

                var parameters = method.GetParameters();
                foreach (var parameter in parameters)
                {
                    var attributes = parameter.GetCustomAttributes(false);
                    foreach (var attribute in attributes)
                    {
                        var attributeType = attribute.GetType();

                        if (attributeType.Name == "ServiceBusTriggerAttribute")
                        {
                            var queueName = ExtractQueueNameFromServiceBusTriggerAttribute(attribute);
                            if (!string.IsNullOrEmpty(queueName))
                            {
                                return queueName;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error extracting queue name from function method: {EntryPoint}", entryPoint);
            }

            return null;
        }

        // TODO: @pmartinez I hate this
        private static string? ExtractQueueNameFromServiceBusTriggerAttribute(object serviceBusTriggerAttribute)
        {
            try
            {
                var attributeType = serviceBusTriggerAttribute.GetType();

                var properties = attributeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var property in properties)
                {
                    var propValue = property.GetValue(serviceBusTriggerAttribute);
                    if ((property.Name.Equals("QueueName") || property.Name.Equals("TopicName")) &&
                        propValue is string strValue && !string.IsNullOrEmpty(strValue))
                    {
                        return strValue;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error extracting queue name from ServiceBusTrigger attribute");
            }

            return null;
        }
    }
}

#endif
