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

            // When CallTarget-based Activity interception is enabled, this OnStart fires at different
            // points depending on the loaded DiagnosticSource version:
            //
            //  - DS 6.0+: from inside Activity.Start() — BEFORE ActivityStartIntegration.OnMethodEnd
            //    creates the Datadog scope. Stash the Resource on the activity so OnMethodEnd can read
            //    it back when it creates the span.
            //  - DS 5.x: from inside ActivitySource.StartActivity, AFTER Activity.CreateAndStart returns
            //    (and therefore AFTER ActivityCreateAndStartIntegration.OnMethodEnd has already created
            //    the scope). The stash on the activity is too late to be read by OnMethodEnd, so apply
            //    the Resource directly to the existing span instead.
            //
            // Both branches handle multiple TracerProviders with different resources correctly, since
            // each Activity carries the resource of the provider that started it.
            if (Tracer.Instance.Settings.IsActivityInterceptionEnabled)
            {
                if (baseProcessor.ParentProvider is not null
                 && activityData.TryDuckCast<IActivity5>(out var activity5))
                {
                    var resourceObject = _getResourceDelegate(baseProcessor.ParentProvider);
                    activity5.SetCustomProperty(ActivityCustomPropertyKeys.Resource, resourceObject);

                    // DS 5.x path: scope already exists; apply the Resource directly to the span.
                    // Use the preserve-existing-tags variant because, unlike the DS 6.0+ path
                    // (where Resource is applied BEFORE the activity tag-copy step in
                    // ActivityStartIntegration.CreateAndLinkScope), here the scope creation has
                    // already finished, so any explicit `service.name` tag the user set will
                    // already be on the span — we must not overwrite it with the Resource value.
                    // On DS 6.0+ this branch is a no-op because OnMethodEnd hasn't run yet so
                    // __dd_span__ isn't set.
                    if (activity5.GetCustomProperty(ActivityCustomPropertyKeys.Span) is Scope scope
                     && resourceObject.TryDuckCast<IResource>(out var resource))
                    {
                        ApplyResourceToSpanPreservingExistingTags(scope.Span, resource);
                    }
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

        /// <summary>
        /// Applies the Resource attributes to a span, but only for attributes the span doesn't
        /// already have. Used on the DS 5.x interception path where the activity tag-copy in
        /// <c>ActivityStartIntegration.CreateAndLinkScope</c> has already run by the time this
        /// is called from <see cref="OnStart"/>; we must not clobber user-supplied tag overrides
        /// (in particular `service.name = "ServiceNameOverride"` style overrides).
        /// </summary>
        private static void ApplyResourceToSpanPreservingExistingTags(Span span, IResource resource)
            => ApplyResourceToSpanCore(span, resource, preserveExisting: true);

        private static void ApplyResourceToSpan(Span span, IResource resource)
            => ApplyResourceToSpanCore(span, resource, preserveExisting: false);

        // One SetTag per resource attribute (plus an optional Tags.Version mirror for service.version).
        // attribute.Value.ToString() is computed at most once per attribute.
        private static void ApplyResourceToSpanCore(Span span, IResource resource, bool preserveExisting)
        {
            foreach (var attribute in resource.Attributes)
            {
                var key = attribute.Key;

                if (preserveExisting && span.GetTag(key) is not null)
                {
                    continue;
                }

                var value = attribute.Value?.ToString();

                if (key == "service.name" && attribute.Value is not null)
                {
                    // If OTEL_SERVICE_NAME isn't set, OpenTelemetry will set "service.name" to
                    // "unknown_service" or "unknown_service:ProcessName" — fall back to the
                    // Datadog default service name in that case.
                    if (string.IsNullOrEmpty(value)
                     || string.Equals(value, "unknown_service", StringComparison.Ordinal)
                     || value!.StartsWith("unknown_service:", StringComparison.Ordinal))
                    {
                        value = Tracer.Instance.DefaultServiceName;
                    }

                    span.SetTag(key, value);
                    span.SetService(value!, source: null);
                    continue;
                }

                span.SetTag(key, value);

                // service.version is additionally mirrored into the Datadog `version` tag.
                // The early-out at the top has already preserved any user-supplied service.version
                // tag in preserveExisting mode; if we reach here, mirroring is always desired.
                if (key == "service.version" && value is not null)
                {
                    span.SetTag(Tags.Version, value);
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
