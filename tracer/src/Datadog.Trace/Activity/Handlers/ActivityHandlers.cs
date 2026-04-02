// <copyright file="ActivityHandlers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Activity.Handlers
{
    internal sealed class ActivityHandlers
    {
        internal ActivityHandlers(bool isActivityListenerEnabled, string[]? disabledActivitySources, string[]? forceEnabledActivitySources)
        {
            // Today, we have to have an ignore handler no matter what we do, because it's checked in Tracer.CreateSpanContext()
            // TODO: _Should_ Tracer.CreateSpanContext() be checking the ignore handler if the activity listener is _not_ enabled?
            IgnoreHandler = new IgnoreActivityHandler(forceEnabledActivitySources);

            if (!isActivityListenerEnabled)
            {
                Handlers = [];
                return;
            }

            Handlers =
            [
                // Disable Activity Handler does not listen to the activity source at all.
                new DisableActivityHandler(disabledActivitySources),

                IgnoreHandler,

                // Azure Service Bus handlers
                new AzureServiceBusActivityHandler(),

                // Quartz handlers
                new QuartzActivityHandler(),

                // The default handler catches an activity and creates a datadog span from it.
                new DefaultActivityHandler(),
            ];
        }

        // Ignore Activity Handler catches existing integrations that also emits activities.
        // Always created, even when activity listener is disabled, because Tracer uses it
        // to check whether to adopt an Activity's trace ID.
        internal IgnoreActivityHandler IgnoreHandler { get; }

        // Activity handlers in order, the first handler where ShouldListenTo returns true will always handle that source.
        internal IActivityHandler[] Handlers { get; }
    }
}
