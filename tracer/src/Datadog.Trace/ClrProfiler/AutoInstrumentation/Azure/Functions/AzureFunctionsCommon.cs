// <copyright file="AzureFunctionsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Tagging;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    internal static class AzureFunctionsCommon
    {
        public const string IntegrationName = nameof(Configuration.IntegrationId.AzureFunctions);

        public const string OperationName = "azure-functions.invoke";
        public const string SpanType = SpanTypes.Serverless;
        public const IntegrationId IntegrationId = Configuration.IntegrationId.AzureFunctions;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AzureFunctionsCommon));
        private static readonly AspNetCoreHttpRequestHandler AspNetCoreRequestHandler = new AspNetCoreHttpRequestHandler(Log, OperationName, IntegrationId);

        public static CallTargetState OnFunctionMiddlewareBegin<TTarget>(TTarget instance, HttpContext httpContext)
        {
            var tracer = Tracer.Instance;

            if (tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                var scope = AspNetCoreRequestHandler.StartAspNetCorePipelineScope(tracer, httpContext, httpContext.Request, resourceName: null);

                if (scope != null)
                {
                    return new CallTargetState(scope);
                }
            }

            return CallTargetState.GetDefault();
        }

        public static CallTargetState OnFunctionExecutionBegin<TTarget, TFunction>(TTarget instance, TFunction instanceParam)
            where TFunction : IFunctionInstance
        {
            var tracer = Tracer.Instance;

            if (tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                var scope = CreateScope(tracer, instanceParam);
                return new CallTargetState(scope);
            }

            return CallTargetState.GetDefault();
        }

        internal static Scope CreateScope(Tracer tracer, IFunctionInstance instanceParam)
        {
            Scope scope = null;

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

                var tags = new AzureFunctionsTags
                {
                    TriggerType = triggerType,
                    ShortName = functionName,
                    FullName = instanceParam.FunctionDescriptor.FullName,
                    BindingSource = bindingSourceType.FullName
                };

                if (tracer.InternalActiveScope == null)
                {
                    // This is the root scope
                    tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
                    scope = tracer.StartActiveWithTags(OperationName, tags: tags);
                }
                else
                {
                    scope = tracer.StartActive(OperationName);
                    foreach (var tagProperty in AzureFunctionsTags.AzureFunctionsExtraTags)
                    {
                        scope.Root.Span.SetTag(tagProperty.Key, tagProperty.Getter(tags));
                    }
                }

                scope.Root.Span.Type = SpanType;
                scope.Span.ResourceName = $"{triggerType} {functionName}";
                scope.Span.Type = SpanType;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return scope;
        }
    }
}
#endif
