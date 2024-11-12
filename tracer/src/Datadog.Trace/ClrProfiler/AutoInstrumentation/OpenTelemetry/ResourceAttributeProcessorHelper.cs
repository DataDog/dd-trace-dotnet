// <copyright file="ResourceAttributeProcessorHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection.Emit;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.OpenTelemetry
{
    internal static class ResourceAttributeProcessorHelper
    {
        private static Func<object, object>? _getResourceDelegate;

        static ResourceAttributeProcessorHelper()
        {
            _getResourceDelegate = CreateGetResourceDelegate();
        }

        public static void OnStart(object processor, object activityData)
        {
            if (_getResourceDelegate is null
                || !activityData.TryDuckCast<IActivity>(out var activity)
                || !processor.TryDuckCast<BaseProcessorStruct>(out var baseProcessor))
            {
                return;
            }

            string key;
            if (activityData.TryDuckCast<IW3CActivity>(out var w3cActivity) && w3cActivity.TraceId is { } traceId && w3cActivity.SpanId is { } spanId)
            {
                key = traceId + spanId;
            }
            else
            {
                key = activity.Id;
            }

            if (ActivityHandlerCommon.ActivityMappingById.TryGetValue(key, out var activityMapping))
            {
                if (baseProcessor.ParentProvider is not null)
                {
                    var resourceObject = _getResourceDelegate(baseProcessor.ParentProvider);
                    if (resourceObject.TryDuckCast<IResource>(out var resource))
                    {
                        var span = activityMapping.Scope.Span;
                        foreach (var attribute in resource.Attributes)
                        {
                            span.SetTag(attribute.Key, attribute.Value?.ToString());

                            // In addition to copying the attribute as a tag, update span fields for specific keys
                            if (attribute.Value is not null)
                            {
                                if (attribute.Key == "service.name")
                                {
                                    var resourceServiceName = attribute.Value.ToString();

                                    // if OTEL_SERVICE_NAME isn't set, OpenTelemetry will set "service.name" to:
                                    // "unknown_service" or "unknown_service:ProcessName"
                                    if (string.IsNullOrEmpty(resourceServiceName)
                                     || string.Equals(resourceServiceName, "unknown_service", StringComparison.Ordinal)
                                     || resourceServiceName.StartsWith("unknown_service:", StringComparison.Ordinal))
                                    {
                                        resourceServiceName = Tracer.Instance.DefaultServiceName;

                                        span.SetTag(attribute.Key, resourceServiceName);
                                    }

                                    span.ServiceName = resourceServiceName;
                                }
                                else if (attribute.Key == "service.version")
                                {
                                    span.SetTag(Tags.Version, attribute.Value.ToString());
                                }
                            }
                        }
                    }
                }
            }
        }

        private static Func<object, object>? CreateGetResourceDelegate()
        {
            Type? providerExtensionsType = Type.GetType("OpenTelemetry.ProviderExtensions, OpenTelemetry", throwOnError: false);
            Type? baseProviderType = Type.GetType("OpenTelemetry.BaseProvider, OpenTelemetry.Api", throwOnError: false);

            if (providerExtensionsType is null || baseProviderType is null)
            {
                return null;
            }

            // Get actual extension method from the API
            var targetGetResourceMethod = providerExtensionsType?.GetMethod("GetResource", new[] { baseProviderType });
            if (targetGetResourceMethod is null)
            {
                return null;
            }

            // Create a Delegate that accepts its inputs and outputs as object
            // and will handle converting to/from object
            DynamicMethod dynMethod = new DynamicMethod(
                     $"{nameof(ResourceAttributeProcessorHelper)}.GetResource",
                     typeof(object),
                     new Type[] { typeof(object) },
                     typeof(ResourceAttributeProcessorHelper).Module,
                     true);
            ILGenerator ilWriter = dynMethod.GetILGenerator();
            ilWriter.Emit(OpCodes.Ldarg_0);
            ilWriter.Emit(OpCodes.Castclass, baseProviderType); // Cast to OpenTelemetry.BaseProvider

            ilWriter.EmitCall(OpCodes.Call, targetGetResourceMethod, null);
            ilWriter.Emit(OpCodes.Ret);

            return (Func<object, object>)dynMethod.CreateDelegate(typeof(Func<object, object>));
        }
    }
}
