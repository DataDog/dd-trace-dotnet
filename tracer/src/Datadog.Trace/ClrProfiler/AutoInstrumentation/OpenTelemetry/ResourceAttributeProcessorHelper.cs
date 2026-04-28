// <copyright file="ResourceAttributeProcessorHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection.Emit;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity;
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

        /// <summary>
        /// Applies the OTel resource attributes (service.name, service.version, etc.) previously
        /// stashed on the activity via <see cref="OnStart"/>. Called from
        /// <c>ActivityStartIntegration.OnMethodEnd</c> after the span has been created.
        /// </summary>
        /// <remarks>
        /// The processor's OnStart fires *during* Activity.Start() — before our OnMethodEnd runs.
        /// At that point the Datadog span doesn't exist yet, so OnStart stashes the resource on the
        /// Activity itself (via a custom property). Reading it back here ties each span to the resource
        /// of the specific TracerProvider that produced its Activity, so multiple TracerProviders with
        /// different resources are handled correctly.
        /// </remarks>
        internal static void ApplyResourceAttributesFromActivity(Span span, IActivity5 activity5)
        {
            var resourceObject = activity5.GetCustomProperty(ActivityCustomPropertyKeys.Resource);
            if (resourceObject is null)
            {
                return;
            }

            if (resourceObject.TryDuckCast<IResource>(out var resource))
            {
                ApplyResourceToSpan(span, resource);
            }
        }

        public static void OnStart(object processor, object activityData)
        {
            if (_getResourceDelegate is null
                || !activityData.TryDuckCast<IActivity>(out var activity)
                || !processor.TryDuckCast<BaseProcessorStruct>(out var baseProcessor))
            {
                return;
            }

            // When CallTarget-based Activity interception is enabled, the Datadog span doesn't exist yet
            // (this OnStart fires during Activity.Start(), before ActivityStartIntegration.OnMethodEnd).
            // Stash the raw resource object on the activity so OnMethodEnd can apply it to the span.
            // This naturally handles multiple TracerProviders with different resources, since each Activity
            // carries the resource of the provider that started it.
            if (Tracer.Instance.Settings.IsActivityInterceptionEnabled)
            {
                if (baseProcessor.ParentProvider is not null
                 && activityData.TryDuckCast<IActivity5>(out var activity5))
                {
                    var resourceObject = _getResourceDelegate(baseProcessor.ParentProvider);
                    activity5.SetCustomProperty(ActivityCustomPropertyKeys.Resource, resourceObject);
                }

                return;
            }

            // Managed ActivityListener path: look up the span via ConcurrentDictionary
            Span? span;
            ActivityKey key;
            if (activityData.TryDuckCast<IW3CActivity>(out var w3cActivity) && w3cActivity.TraceId is { } traceId && w3cActivity.SpanId is { } spanId)
            {
                key = new(traceId, spanId);
            }
            else
            {
                key = new(activity.Id);
            }

            if (!key.IsValid() || !ActivityHandlerCommon.ActivityMappingById.TryGetValue(key, out var activityMapping))
            {
                return;
            }

            span = activityMapping.Scope?.Span;

            if (span is not null && baseProcessor.ParentProvider is not null)
            {
                var resourceObject = _getResourceDelegate(baseProcessor.ParentProvider);
                if (resourceObject.TryDuckCast<IResource>(out var resource))
                {
                    ApplyResourceToSpan(span, resource);
                }
            }
        }

        private static void ApplyResourceToSpan(Span span, IResource resource)
        {
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

                        span.SetService(resourceServiceName, null);
                    }
                    else if (attribute.Key == "service.version")
                    {
                        span.SetTag(Tags.Version, attribute.Value.ToString());
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
