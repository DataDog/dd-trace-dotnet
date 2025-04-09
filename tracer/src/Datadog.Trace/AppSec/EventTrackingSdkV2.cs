// <copyright file="EventTrackingSdkV2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.AppSec;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.AppSec;

/// <summary>
/// Allow
/// </summary>
public static class EventTrackingSdkV2
{
    internal static void TrackUserLoginSuccess<TUserDetails>(string userLogin, TUserDetails? userDetails, IDictionary<string, string>? metadata, Tracer tracer)
        where TUserDetails : IUserDetails?
    {
        TelemetryFactory.Metrics.RecordCountUserEventSdk(MetricTags.UserEventSdk.UserEventLoginSuccessSdkV2);

        if (string.IsNullOrEmpty(userLogin))
        {
            ThrowHelper.ThrowArgumentNullException(nameof(userLogin));
        }

        var span = tracer.ActiveScope?.Span;

        if (span is null)
        {
            ThrowHelper.ThrowException("Can't create a tracking event with no active span");
        }

        var setTag = TaggingUtils.GetSpanSetter(span, out var internalSpan);
        if (internalSpan is null)
        {
            ThrowHelper.ThrowException("Can't create a tracking event without a span setter found");
        }

        setTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessLogin, userLogin);
        setTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessTrack, Tags.AppSec.EventsUsers.True);
        setTag(Tags.AppSec.EventsUsers.LoginEvent.SuccessSdkSource, Tags.AppSec.EventsUsers.True);

        PopulateTags(userDetails, metadata, setTag, true);

        RunSecurityChecksAndReport(internalSpan, userLogin: userLogin, userId: userDetails?.Id, loginSuccess: true);
    }

    internal static void TrackUserLoginFailure(string userLogin, bool exists, IUserDetails? userDetails, IDictionary<string, string>? metadata, Tracer tracer)
    {
        TelemetryFactory.Metrics.RecordCountUserEventSdk(MetricTags.UserEventSdk.UserEventFailureSdkV2);

        if (string.IsNullOrEmpty(userLogin))
        {
            ThrowHelper.ThrowArgumentNullException(nameof(userLogin));
        }

        var span = tracer.ActiveScope?.Span;

        if (span is null)
        {
            ThrowHelper.ThrowException("Can't create a tracking event with no active span");
        }

        var setTag = TaggingUtils.GetSpanSetter(span, out var internalSpan);
        if (internalSpan is null)
        {
            ThrowHelper.ThrowException("Can't create a tracking event without a span setter found");
        }

        setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureTrack, Tags.AppSec.EventsUsers.True);
        setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureSdkSource, Tags.AppSec.EventsUsers.True);
        setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureUserExists, exists ? Tags.AppSec.EventsUsers.True : Tags.AppSec.EventsUsers.False);
        setTag(Tags.AppSec.EventsUsers.LoginEvent.FailureUserLogin, userLogin);

        PopulateTags(userDetails, metadata, setTag, false);

        RunSecurityChecksAndReport(internalSpan, userLogin: userLogin, loginSuccess: false);
    }

    /// <summary>
    /// This method is a bit similar to the extension SetUser but is more specific to the user login
    /// </summary>
    private static void PopulateTags(IUserDetails? userDetails, IDictionary<string, string>? metadata, Action<string, string> setTag, bool loginSuccess)
    {
        setTag(Tags.AppSec.EventsUsers.CollectionMode, Tags.AppSec.EventsUsers.Sdk);
        var prefix = loginSuccess ? Tags.AppSec.EventsUsers.LoginEvent.Success : Tags.AppSec.EventsUsers.LoginEvent.Failure;
        var prefixUser = prefix + ".usr";
        if (metadata is { Count: > 0 })
        {
            foreach (var kvp in metadata)
            {
                setTag($"{prefix}.{kvp.Key}", kvp.Value);
            }
        }

        if (userDetails is { } userDetailsValue)
        {
            // usr.id should always be set, even when PropagateId is true
            setTag(Tags.User.Id, userDetailsValue.Id);
            setTag(Tags.AppSec.EventsUsers.CollectionMode, Tags.AppSec.EventsUsers.Sdk);
            setTag(prefixUser + ".id", userDetailsValue.Id);

            if (userDetailsValue.PropagateId)
            {
                var base64UserId = Convert.ToBase64String(Encoding.UTF8.GetBytes(userDetailsValue.Id));
                const string propagatedUserIdTag = TagPropagation.PropagatedTagPrefix + Tags.User.Id;
                setTag(propagatedUserIdTag, base64UserId);
                setTag(prefixUser + ".propagate_id", Tags.AppSec.EventsUsers.True);
            }
            else
            {
                setTag(prefixUser + ".propagate_id", Tags.AppSec.EventsUsers.False);
            }

            if (userDetailsValue.Name is not null)
            {
                setTag(prefixUser + ".name", userDetailsValue.Name);
                setTag(Tags.User.Name, userDetailsValue.Name);
            }

            if (userDetailsValue.Scope is not null)
            {
                setTag(Tags.User.Scope, userDetailsValue.Scope);
                setTag(prefixUser + ".scope", userDetailsValue.Scope);
            }

            if (userDetailsValue.Role is not null)
            {
                setTag(Tags.User.Role, userDetailsValue.Role);
                setTag(prefixUser + ".role", userDetailsValue.Role);
            }

            if (userDetailsValue.SessionId is not null)
            {
                setTag(Tags.User.SessionId, userDetailsValue.SessionId);
                setTag(prefixUser + ".session_id", userDetailsValue.SessionId);
            }

            if (userDetailsValue.Email is not null)
            {
                setTag(Tags.User.Email, userDetailsValue.Email);
                setTag(prefixUser + ".email", userDetailsValue.Email);
            }
        }
    }

    /// <summary>
    /// Biggest part of this method will only work in a web context
    /// </summary>
    /// <param name="span">span</param>
    /// <param name="userLogin">now mandatory user login</param>
    /// <param name="userId">user id</param>
    /// <param name="loginSuccess">whether it's a login success</param>
    private static void RunSecurityChecksAndReport(Span span, string userLogin, string? userId = null, bool loginSuccess = false)
    {
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
            var result = securityCoordinator.Value.RunWafForUser(userLogin: userLogin, userId: userId, fromSdk: true, otherTags: new() { { loginSuccess ? AddressesConstants.UserBusinessLoginSuccess : AddressesConstants.UserBusinessLoginFailure, string.Empty } });
            securityCoordinator.Value.BlockAndReport(result);
        }
    }
}
