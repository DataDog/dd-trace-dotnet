// <copyright file="ActivityHandlerTestExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.DuckTyping;
using SD = System.Diagnostics;

namespace Datadog.Trace.Tests.Activity;

internal static class ActivityHandlerTestExtensions
{
    /// <summary>
    /// Calls the stop method that matches the Activity shape available on the current runtime.
    /// </summary>
    public static void StopActivity(this IActivityHandler handler, string sourceName, SD.Activity activity)
    {
        if (activity.TryDuckCast<IActivity5>(out var activity5))
        {
            handler.ActivityStopped(sourceName, activity5, default);
        }
        else
        {
            handler.LegacyActivityStopped(sourceName, activity.DuckCast<IActivity>());
        }
    }
}
