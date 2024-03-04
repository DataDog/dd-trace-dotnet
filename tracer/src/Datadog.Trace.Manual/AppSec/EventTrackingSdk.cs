// <copyright file="EventTrackingSdk.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.AppSec;

/// <summary>
/// Handlers for setting ASM logon events in traces
/// </summary>
public static class EventTrackingSdk
{
    /// <summary>
    /// Sets the details of a successful logon on the trace
    /// </summary>
    /// <param name="userId">The userId associated with the login success</param>
    [Instrumented]
    public static void TrackUserLoginSuccessEvent(string userId)
    {
    }

    /// <summary>
    /// Sets the details of a successful logon on the trace
    /// </summary>
    /// <param name="userId">The userId associated with the login success</param>
    /// <param name="metadata">Metadata associated with the login success</param>
    [Instrumented]
    public static void TrackUserLoginSuccessEvent(string userId, IDictionary<string, string> metadata)
    {
    }

    /// <summary>
    /// Sets the details of a logon failure on the trace
    /// </summary>
    /// <param name="userId">The userId associated with the login failure</param>
    /// <param name="exists">If the userId associated with the login failure exists</param>
    [Instrumented]
    public static void TrackUserLoginFailureEvent(string userId, bool exists)
    {
    }

    /// <summary>
    /// Sets the details of a logon failure on the trace
    /// </summary>
    /// <param name="userId">The userId associated with the login failure</param>
    /// <param name="exists">If the userId associated with the login failure exists</param>
    /// <param name="metadata">Metadata associated with the login failure</param>
    [Instrumented]
    public static void TrackUserLoginFailureEvent(string userId, bool exists, IDictionary<string, string> metadata)
    {
    }

    /// <summary>
    /// Sets the details of a custom event the trace
    /// </summary>
    /// <param name="eventName">the name of the event to be tracked</param>
    [Instrumented]
    public static void TrackCustomEvent(string eventName)
    {
    }

    /// <summary>
    /// Sets the details of a custom event the trace
    /// </summary>
    /// <param name="eventName">the name of the event to be tracked</param>
    /// <param name="metadata">Metadata associated with the custom event</param>
    [Instrumented]
    public static void TrackCustomEvent(string eventName, IDictionary<string, string> metadata)
    {
    }
}
