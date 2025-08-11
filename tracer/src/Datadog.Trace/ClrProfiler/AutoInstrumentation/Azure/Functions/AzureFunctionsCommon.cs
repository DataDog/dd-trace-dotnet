// <copyright file="AzureFunctionsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
                Log.Information("CreateIsolatedFunctionScope");

                // Try to work out which trigger type it is
                var triggerType = "Unknown";
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

                    Log.Information("Detected trigger of type {Type} interpreted as {TriggerType}", type, triggerType);

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

                if (tracer.InternalActiveScope == null)
                {
                    // This is the root scope
                    tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
                    scope = tracer.StartActiveInternal(OperationName, tags: tags, parent: null, links: extractedContext.Links);
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
            Log.Information("ExtractPropagatedContextFromHttp called with bindingName: {BindingName}", bindingName);

            // Need to try and grab the headers from the context
            // Unfortunately, the parsed object isn't available yet, so we grab it
            // directly from the grpc call instead. This is... interesting. It
            // is effectively doing the equivalent of context.GetHttpRequestDataAsync() which is
            // the suggested approach in the docs.
            if (context.Features is null || string.IsNullOrEmpty(bindingName))
            {
                Log.Information(
                    "Early return: context.Features is null: {FeaturesIsNull}, bindingName is null or empty: {BindingNameIsEmpty}",
                    context.Features is null,
                    string.IsNullOrEmpty(bindingName));
                return default;
            }

            Log.Information("Proceeding to search for IFunctionBindingsFeature in context features");

            try
            {
                object? feature = null;
                Log.Information("Searching for IFunctionBindingsFeature in context features");

                foreach (var keyValuePair in context.Features)
                {
                    Log.Information("Checking feature key: {FeatureKey}", keyValuePair.Key.FullName);

                    if (keyValuePair.Key.FullName?.Equals("Microsoft.Azure.Functions.Worker.Context.Features.IFunctionBindingsFeature") == true)
                    {
                        feature = keyValuePair.Value;
                        Log.Information("Found IFunctionBindingsFeature, feature type: {FeatureType}", feature?.GetType().FullName);
                        break;
                    }
                }

                if (feature is null || !feature.TryDuckCast<FunctionBindingsFeatureStruct>(out var bindingFeature))
                {
                    Log.Information(
                        "Failed to get binding feature: feature is null: {FeatureIsNull}, duck cast successful: {DuckCastSuccess}",
                        feature is null,
                        feature?.TryDuckCast<FunctionBindingsFeatureStruct>(out var _) == true);
                    return default;
                }

                Log.Information("Successfully obtained binding feature, InputData is null: {InputDataIsNull}", bindingFeature.InputData is null);

                object? requestDataObject = null;
                var hasInputData = bindingFeature.InputData != null;
                var hasBindingName = hasInputData && bindingFeature.InputData!.TryGetValue(bindingName!, out requestDataObject);

                if (!hasInputData || !hasBindingName || requestDataObject is null)
                {
                    Log.Information(
                        "Failed to get request data: InputData is null: {InputDataIsNull}, contains binding: {ContainsBinding}, requestDataObject is null: {RequestDataObjectIsNull}",
                        !hasInputData,
                        hasInputData && bindingFeature.InputData!.ContainsKey(bindingName!),
                        requestDataObject is null);
                    return default;
                }

                Log.Information("Got request data object, type: {RequestDataObjectType}", requestDataObject.GetType().FullName);

                foreach (var entry in bindingFeature.InputData ?? Enumerable.Empty<KeyValuePair<string, object?>>())
                {
                    var valueTypeName = entry.Value?.GetType()?.FullName ?? "null";
                    Log.Information(
                        "InputData contains key: {Key}, value type: {ValueType}",
                        entry.Key,
                        valueTypeName);
                }

                if (requestDataObject.GetType().FullName == "System.String")
                {
                    Log.Information("Request data object is a string with value: {RequestDataObjectValue}", requestDataObject);
                }

                if (!requestDataObject.TryDuckCast<HttpRequestDataStruct>(out var httpRequest))
                {
                    Log.Information("Failed to duck cast request data object to HttpRequestDataStruct");
                    return default;
                }

                Log.Information("Successfully cast to HttpRequestDataStruct, headers available: {HeadersAvailable}", httpRequest.Headers != null);

                if (httpRequest.Headers == null)
                {
                    Log.Information("HttpRequest.Headers is null, cannot extract propagation context");
                    return default;
                }

                var propagationContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(new HttpHeadersCollection(httpRequest.Headers));

                Log.Information("Successfully extracted propagation context");

                return propagationContext;
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
            Log.Information("ExtractPropagatedContextFromServiceBus called with bindingName: {BindingName}", bindingName);

            // Need to try and grab the UserProperties from the ServiceBus message in the context
            if (context.Features is null || string.IsNullOrEmpty(bindingName))
            {
                Log.Information(
                    "Early return: context.Features is null: {FeaturesIsNull}, bindingName is null or empty: {BindingNameIsEmpty}",
                    context.Features is null,
                    string.IsNullOrEmpty(bindingName));
                return default;
            }

            Log.Information("Proceeding to search for IFunctionBindingsFeature in context features for ServiceBus");

            try
            {
                object? feature = null;
                Log.Information("Searching for IFunctionBindingsFeature in context features");

                foreach (var keyValuePair in context.Features)
                {
                    Log.Information("Checking feature key: {FeatureKey}", keyValuePair.Key.FullName);

                    if (keyValuePair.Key.FullName?.Equals("Microsoft.Azure.Functions.Worker.Context.Features.IFunctionBindingsFeature") == true)
                    {
                        feature = keyValuePair.Value;
                        Log.Information("Found IFunctionBindingsFeature, feature type: {FeatureType}", feature?.GetType().FullName);
                        break;
                    }
                }

                if (feature is null || !feature.TryDuckCast<FunctionBindingsFeatureStruct>(out var bindingFeature))
                {
                    Log.Information(
                        "Failed to get binding feature: feature is null: {FeatureIsNull}, duck cast successful: {DuckCastSuccess}",
                        feature is null,
                        feature?.TryDuckCast<FunctionBindingsFeatureStruct>(out var _) == true);
                    return default;
                }

                if (!feature.TryDuckCast<GrpcBindingsFeatureStruct>(out var grpcFeature))
                {
                    Log.Information("Failed to duck cast feature to GrpcBindingsFeatureStruct for ServiceBus");
                    return default;
                }

                Log.Information("Successfully obtained grpc feature for ServiceBus, TriggerMetadata is null: {TriggerMetadataIsNull}", grpcFeature.TriggerMetadata is null);

                var spanLinks = new List<SpanLink>();

                // Handle single message scenario (UserProperties)
                if (grpcFeature.TriggerMetadata?.TryGetValue("UserProperties", out var userPropertiesObj) == true)
                {
                    Log.Information("Found UserProperties in TriggerMetadata, type: {UserPropertiesType}", userPropertiesObj?.GetType().FullName);
                    var extractedContext = ExtractContextFromUserProperties(userPropertiesObj);
                    if (extractedContext.SpanContext != null)
                    {
                        spanLinks.Add(new SpanLink(extractedContext.SpanContext));
                        Log.Information(
                            "Added span link from UserProperties: TraceId={TraceId}, SpanId={SpanId}",
                            extractedContext.SpanContext.TraceId128.Lower,
                            extractedContext.SpanContext.SpanId);
                    }
                }
                else
                {
                    Log.Information("UserProperties not found in TriggerMetadata");
                }

                // Handle batch message scenario (UserPropertiesArray)
                if (grpcFeature.TriggerMetadata?.TryGetValue("UserPropertiesArray", out var userPropertiesArrayObj) == true)
                {
                    Log.Information("Found UserPropertiesArray in TriggerMetadata, type: {UserPropertiesArrayType}", userPropertiesArrayObj?.GetType().FullName);
                    var batchSpanLinks = ExtractSpanLinksFromUserPropertiesArray(userPropertiesArrayObj);
                    spanLinks.AddRange(batchSpanLinks);
                    Log.Information("Added {Count} span links from UserPropertiesArray", (object)batchSpanLinks.Count);
                }
                else
                {
                    Log.Information("UserPropertiesArray not found in TriggerMetadata");
                }

                if (spanLinks.Count > 0)
                {
                    Log.Information("Successfully extracted {Count} span links from ServiceBus metadata", (object)spanLinks.Count);
                    // Return PropagationContext with links instead of parent span context
                    return new PropagationContext(
                        spanContext: null, // No parenting - we're using links instead
                        baggage: Baggage.Current,
                        extractionSpanLinks: spanLinks);
                }
                else
                {
                    Log.Information("No span links extracted from ServiceBus metadata");
                }

                return default;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting propagated context from ServiceBus binding");
                return default;
            }
        }

        private static PropagationContext ExtractContextFromUserProperties(object? userPropertiesObj)
        {
            if (userPropertiesObj is string userPropertiesJson)
            {
                Log.Information("UserProperties is a JSON string: {UserPropertiesJson}", userPropertiesJson);

                try
                {
                    // Parse the JSON string to extract headers
                    var userPropertiesDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(userPropertiesJson);

                    if (userPropertiesDict != null)
                    {
                        Log.Information("Successfully parsed UserProperties JSON, contains {Count} keys", (object)userPropertiesDict.Count);

                        // Log all keys for debugging
                        foreach (var kvp in userPropertiesDict)
                        {
                            Log.Information("UserProperties key: {Key}, value: {Value}", kvp.Key, kvp.Value);
                        }

                        // Create a headers collection adapter for context extraction
                        var headerAdapter = new ServiceBusUserPropertiesHeadersCollection(userPropertiesDict);
                        var propagationContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(headerAdapter);

                        Log.Information("Successfully extracted propagation context from UserProperties");
                        return propagationContext;
                    }
                    else
                    {
                        Log.Information("Failed to deserialize UserProperties JSON to dictionary");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error parsing UserProperties JSON: {Json}", userPropertiesJson);
                }
            }
            else
            {
                Log.Information("UserProperties is not a string, cannot parse as JSON");
            }

            return default;
        }

        private static List<SpanLink> ExtractSpanLinksFromUserPropertiesArray(object? userPropertiesArrayObj)
        {
            var spanLinks = new List<SpanLink>();

            try
            {
                // Handle array of UserProperties (batch scenario)
                if (userPropertiesArrayObj is string userPropertiesArrayJson)
                {
                    Log.Information("UserPropertiesArray is a JSON string: {UserPropertiesArrayJson}", userPropertiesArrayJson);

                    // The UserPropertiesArray contains an array of objects, not an array of strings
                    var userPropertiesArray = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(userPropertiesArrayJson);

                    if (userPropertiesArray != null)
                    {
                        Log.Information("Successfully parsed UserPropertiesArray JSON, contains {Count} items", (object)userPropertiesArray.Length);

                        for (int i = 0; i < userPropertiesArray.Length; i++)
                        {
                            var userPropertiesDict = userPropertiesArray[i];
                            Log.Information("Processing UserPropertiesArray item {Index}: {ItemKeys}", (object)i, string.Join(", ", userPropertiesDict.Keys));

                            // Create a headers collection adapter for context extraction
                            var headerAdapter = new ServiceBusUserPropertiesHeadersCollection(userPropertiesDict);
                            var extractedContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(headerAdapter);

                            if (extractedContext.SpanContext != null)
                            {
                                spanLinks.Add(new SpanLink(extractedContext.SpanContext));
                                Log.Information(
                                    "Added span link from UserPropertiesArray item {Index}: TraceId={TraceId}, SpanId={SpanId}",
                                    (object)i,
                                    extractedContext.SpanContext.TraceId128.Lower,
                                    extractedContext.SpanContext.SpanId);
                            }
                            else
                            {
                                Log.Information("No span context extracted from UserPropertiesArray item {Index}", (object)i);
                            }
                        }
                    }
                    else
                    {
                        Log.Information("Failed to deserialize UserPropertiesArray JSON to object array");
                    }
                }

                // Could also handle direct array objects if they come in that format
                else if (userPropertiesArrayObj is Dictionary<string, object>[] directDictArray)
                {
                    Log.Information("UserPropertiesArray is a direct Dictionary array with {Count} items", (object)directDictArray.Length);

                    for (int i = 0; i < directDictArray.Length; i++)
                    {
                        var userPropertiesDict = directDictArray[i];
                        Log.Information("Processing UserPropertiesArray direct item {Index}: {ItemKeys}", (object)i, string.Join(", ", userPropertiesDict.Keys));

                        // Create a headers collection adapter for context extraction
                        var headerAdapter = new ServiceBusUserPropertiesHeadersCollection(userPropertiesDict);
                        var extractedContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(headerAdapter);

                        if (extractedContext.SpanContext != null)
                        {
                            spanLinks.Add(new SpanLink(extractedContext.SpanContext));
                            Log.Information(
                                "Added span link from UserPropertiesArray direct item {Index}: TraceId={TraceId}, SpanId={SpanId}",
                                (object)i,
                                extractedContext.SpanContext.TraceId128.Lower,
                                extractedContext.SpanContext.SpanId);
                        }
                        else
                        {
                            Log.Information("No span context extracted from UserPropertiesArray direct item {Index}", (object)i);
                        }
                    }
                }
                else if (userPropertiesArrayObj is object[] directArray)
                {
                    Log.Information("UserPropertiesArray is a generic object array with {Count} items", (object)directArray.Length);

                    for (int i = 0; i < directArray.Length; i++)
                    {
                        var item = directArray[i];
                        Log.Information("Processing UserPropertiesArray generic item {Index}: {Item}", (object)i, item?.GetType().FullName ?? "null");

                        // Try to convert the object to a dictionary if possible
                        if (item is Dictionary<string, object> itemDict)
                        {
                            var headerAdapter = new ServiceBusUserPropertiesHeadersCollection(itemDict);
                            var extractedContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(headerAdapter);

                            if (extractedContext.SpanContext != null)
                            {
                                spanLinks.Add(new SpanLink(extractedContext.SpanContext));
                                Log.Information(
                                    "Added span link from UserPropertiesArray generic item {Index}: TraceId={TraceId}, SpanId={SpanId}",
                                    (object)i,
                                    extractedContext.SpanContext.TraceId128.Lower,
                                    extractedContext.SpanContext.SpanId);
                            }
                            else
                            {
                                Log.Information("No span context extracted from UserPropertiesArray generic item {Index}", (object)i);
                            }
                        }
                        else
                        {
                            Log.Information("UserPropertiesArray generic item {Index} is not a Dictionary<string, object>", (object)i);
                        }
                    }
                }
                else
                {
                    Log.Information("UserPropertiesArray is of unexpected type: {Type}", userPropertiesArrayObj?.GetType().FullName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error parsing UserPropertiesArray: {Array}", userPropertiesArrayObj);
            }

            return spanLinks;
        }
    }
}

#endif
