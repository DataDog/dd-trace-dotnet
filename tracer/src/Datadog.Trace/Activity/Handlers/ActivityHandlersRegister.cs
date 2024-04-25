// <copyright file="ActivityHandlersRegister.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Activity.Handlers
{
    internal static class ActivityHandlersRegister
    {
        // Ignore Activity Handler catches existing integrations that also emits activities.
        internal static readonly IgnoreActivityHandler IgnoreHandler = new();

        // Activity handlers in order, the first handler where ShouldListenTo returns true will always handle that source.
        internal static readonly IActivityHandler[] Handlers =
        {
            IgnoreHandler,

            // Azure Service Bus handlers
            new AzureServiceBusActivityHandler(),

            // The default handler catches an activity and creates a datadog span from it.
            new DefaultActivityHandler(),
        };
    }
}
