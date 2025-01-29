// <copyright file="EventTrackingSdk.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.AppSec;

/// <summary>
/// Allow
/// </summary>
public static class EventTrackingSdk
{
    /// <summary>
    /// Sets the details of a successful logon on the local root span
    /// </summary>
    /// <param name="userId">The userId associated with the login success</param>
    [PublicApi]
    public static void TrackUserLoginSuccessEvent(string userId)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.EventTrackingSdk_TrackUserLoginSuccessEvent);
        TrackUserLoginSuccessEvent(userId, null, Tracer.Instance);
    }

    /// <summary>
    /// Sets the details of a successful logon on the local root span
    /// </summary>
    /// <param name="userId">The userId associated with the login success</param>
    /// <param name="metadata">Metadata associated with the login success</param>
    [PublicApi]
    public static void TrackUserLoginSuccessEvent(string userId, IDictionary<string, string> metadata)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.EventTrackingSdk_TrackUserLoginSuccessEvent_Metadata);
        TrackUserLoginSuccessEvent(userId, metadata, Tracer.Instance);
    }

    internal static void TrackUserLoginSuccessEvent(string userId, IDictionary<string, string> metadata, Tracer tracer)
    {
        if (string.IsNullOrEmpty(userId))
        {
            ThrowHelper.ThrowArgumentNullException(nameof(userId));
        }

        var span = tracer?.ActiveScope?.Span;

        if (span is null)
        {
            ThrowHelper.ThrowException("Can't create a tracking event with no active span");
        }

        var setTag = TaggingUtils.GetSpanSetter(span, out var internalSpan);

        setTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessTrack, Tags.AppSec.EventsUsers.True);
        setTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessSdkSource, Tags.AppSec.EventsUsers.True);
        setTag(Tags.User.Id, userId);

        if (metadata is { Count: > 0 })
        {
            foreach (var kvp in metadata)
            {
                setTag($"{Tags.AppSec.EventsUsers.LoginEvent.Success}.{kvp.Key}", kvp.Value);
            }
        }

        FillUp(internalSpan, userId: userId);
    }

    /// <summary>
    /// Biggest part of this method will only work in a web context
    /// </summary>
    /// <param name="span">span</param>
    /// <param name="userId">userid</param>
    private static void FillUp(Span span, string userId = null)
    {
        if (span is null)
        {
            return;
        }

        var securityInstance = Security.Instance;
        if (!securityInstance.IsTrackUserEventsEnabled)
        {
            return;
        }

        securityInstance.SetTraceSamplingPriority(span);

        var securityCoordinator = SecurityCoordinator.TryGetSafe(securityInstance, span);
        if (securityCoordinator is not null)
        {
            RunWafAndCollectHeaders();
        }

        return;

        [MethodImpl(MethodImplOptions.NoInlining)]
        void RunWafAndCollectHeaders()
        {
            securityCoordinator.Value.Reporter.CollectHeaders();
            var result = securityCoordinator.Value.RunWafForUser(userId: userId, fromSdk: true);
            securityCoordinator.Value.BlockAndReport(result);
        }
    }

    /// <summary>
    /// Sets the details of a logon failure on the local root span
    /// </summary>
    /// <param name="userId">The userId associated with the login failure</param>
    /// <param name="exists">If the userId associated with the login failure exists</param>
    [PublicApi]
    public static void TrackUserLoginFailureEvent(string userId, bool exists)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.EventTrackingSdk_TrackUserLoginFailureEvent);
        TrackUserLoginFailureEvent(userId, exists, null, Tracer.Instance);
    }

    /// <summary>
    /// Sets the details of a logon failure on the local root span
    /// </summary>
    /// <param name="userId">The userId associated with the login failure</param>
    /// <param name="exists">If the userId associated with the login failure exists</param>
    /// <param name="metadata">Metadata associated with the login failure</param>
    [PublicApi]
    public static void TrackUserLoginFailureEvent(string userId, bool exists, IDictionary<string, string> metadata)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.EventTrackingSdk_TrackUserLoginFailureEvent_Metadata);
        TrackUserLoginFailureEvent(userId, exists, metadata, Tracer.Instance);
    }

    internal static void TrackUserLoginFailureEvent(string userId, bool exists, IDictionary<string, string> metadata, Tracer tracer)
    {
        if (string.IsNullOrEmpty(userId))
        {
            ThrowHelper.ThrowArgumentNullException(nameof(userId));
        }

        var span = tracer.ActiveScope?.Span;

        if (span is null)
        {
            ThrowHelper.ThrowException("Can't create a tracking event with no active span");
        }

        var setTag = TaggingUtils.GetSpanSetter(span, out var spanInternal);

        setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureTrack, Tags.AppSec.EventsUsers.True);
        setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureSdkSource, Tags.AppSec.EventsUsers.True);
        setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureUserId, userId);
        setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureUserExists, exists ? Tags.AppSec.EventsUsers.True : Tags.AppSec.EventsUsers.False);

        if (metadata is { Count: > 0 })
        {
            foreach (var kvp in metadata)
            {
                setTag($"{Tags.AppSec.EventsUsers.LoginEvent.Failure}.{kvp.Key}", kvp.Value);
            }
        }

        FillUp(spanInternal, userId: userId);
    }

    /// <summary>
    /// Sets the details of a custom event the local root span
    /// </summary>
    /// <param name="eventName">the name of the event to be tracked</param>
    [PublicApi]
    public static void TrackCustomEvent(string eventName)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.EventTrackingSdk_TrackCustomEvent);
        TrackCustomEvent(eventName, null, Tracer.Instance);
    }

    /// <summary>
    /// Sets the details of a custom event the local root span
    /// </summary>
    /// <param name="eventName">the name of the event to be tracked</param>
    /// <param name="metadata">Metadata associated with the custom event</param>
    [PublicApi]
    public static void TrackCustomEvent(string eventName, IDictionary<string, string> metadata)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.EventTrackingSdk_TrackCustomEvent_Metadata);
        TrackCustomEvent(eventName, metadata, Tracer.Instance);
    }

    internal static void TrackCustomEvent(string eventName, IDictionary<string, string> metadata, Tracer tracer)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            ThrowHelper.ThrowArgumentNullException(nameof(eventName));
        }

        var span = tracer.ActiveScope?.Span;

        if (span is null)
        {
            ThrowHelper.ThrowException("Can't create a tracking event with no active span");
        }

        var setTag = TaggingUtils.GetSpanSetter(span, out var internalSpan);

        setTag(Tags.AppSec.Track(eventName), Tags.AppSec.EventsUsers.True);
        setTag($"_dd.{Tags.AppSec.Events}{eventName}.sdk", Tags.AppSec.EventsUsers.True);

        if (metadata is { Count: > 0 })
        {
            foreach (var kvp in metadata)
            {
                setTag($"{Tags.AppSec.Events}{eventName}.{kvp.Key}", kvp.Value);
            }
        }

        FillUp(internalSpan);
    }
}
