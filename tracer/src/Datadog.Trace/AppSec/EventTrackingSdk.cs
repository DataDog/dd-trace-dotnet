// <copyright file="EventTrackingSdk.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Util;

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
        public static void TrackUserLoginSuccessEvent(string userId)
        {
            TrackUserLoginSuccessEvent(userId, null);
        }

        /// <summary>
        /// Sets the details of a successful logon on the local root span
        /// </summary>
        /// <param name="userId">The userId associated with the login success</param>
        /// <param name="metadata">Metadata associated with the login success</param>
        public static void TrackUserLoginSuccessEvent(string userId, IDictionary<string, string> metadata)
        {
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

            var setTag = TaggingUtils.GetSpanSetter(span);

            setTag(Tags.AppSec.EventsUsersLogin.SuccessTrack, "true");
            setTag(Tags.User.Id, userId);

            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    setTag(Tags.AppSec.EventsUsersLogin.Success + kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>
        /// Sets the details of a logon failure on the local root span
        /// </summary>
        /// <param name="userId">The userId associated with the login failure</param>
        /// <param name="exists">If the userId associated with the login failure exists</param>
        public static void TrackUserLoginFailureEvent(string userId, bool exists)
        {
            TrackUserLoginFailureEvent(userId, exists, null);
        }

        /// <summary>
        /// Sets the details of a logon failure on the local root span
        /// </summary>
        /// <param name="userId">The userId associated with the login failure</param>
        /// <param name="exists">If the userId associated with the login failure exists</param>
        /// <param name="metadata">Metadata associated with the login failure</param>
        public static void TrackUserLoginFailureEvent(string userId, bool exists, IDictionary<string, string> metadata)
        {
            TrackUserLoginFailureEvent(userId, exists, metadata, Tracer.Instance);
        }

        internal static void TrackUserLoginFailureEvent(string userId, bool exists, IDictionary<string, string> metadata, Tracer tracer)
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

            var setTag = TaggingUtils.GetSpanSetter(span);

            setTag(Tags.AppSec.EventsUsersLogin.FailureTrack, "true");
            setTag(Tags.AppSec.EventsUsersLogin.FailureUserId, userId);
            setTag(Tags.AppSec.EventsUsersLogin.FailureUserExists, exists ? "true" : "false");

            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    setTag(Tags.AppSec.EventsUsersLogin.Failure + kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>
        /// Sets the details of a custom event the local root span
        /// </summary>
        /// <param name="eventName">the name of the event to be tracked</param>
        public static void TrackCustomEvent(string eventName)
        {
            TrackCustomEvent(eventName, null);
        }

        /// <summary>
        /// Sets the details of a custom event the local root span
        /// </summary>
        /// <param name="eventName">the name of the event to be tracked</param>
        /// <param name="metadata">Metadata associated with the custom event</param>
        public static void TrackCustomEvent(string eventName, IDictionary<string, string> metadata)
        {
            TrackCustomEvent(eventName, metadata, Tracer.Instance);
        }

        internal static void TrackCustomEvent(string eventName, IDictionary<string, string> metadata, Tracer tracer)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                ThrowHelper.ThrowArgumentNullException(nameof(eventName));
            }

            var span = tracer?.ActiveScope?.Span;

            if (span is null)
            {
                ThrowHelper.ThrowException("Can't create a tracking event with no active span");
            }

            var setTag = TaggingUtils.GetSpanSetter(span);

            setTag(Tags.AppSec.Track(eventName), "true");

            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    setTag($"{Tags.AppSec.Events}{eventName}.{kvp.Key}", kvp.Value);
                }
            }
        }
}
