// <copyright file="DefaultActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Activity.Handlers
{
    /// <summary>
    /// The default handler catches an activity and creates a datadog span from it.
    /// </summary>
    internal class DefaultActivityHandler : IActivityHandler
    {
        public bool ShouldListenTo(string sourceName, string? version)
        {
            return true;
        }

        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
            => ActivityHandlerCommon.ActivityStarted(sourceName, activity, tags: new OpenTelemetryTags(), out _);

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
            => ActivityHandlerCommon.ActivityStopped(sourceName, activity);
    }
}
