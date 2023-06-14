// <copyright file="SpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    /// <summary>
    /// Extension methods for the <see cref="ISpan"/> interface
    /// </summary>
    public static partial class SpanExtensions
    {
        /// <summary>
        /// Sets the details of the user on the local root span
        /// </summary>
        /// <param name="span">The span to be tagged</param>
        /// <param name="userDetails">The details of the current logged on user</param>
        [PublicApi]
        public static void SetUser(this ISpan span, UserDetails userDetails)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.SpanExtensions_SetUser);

            if (span is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(span));
            }

            if (string.IsNullOrEmpty(userDetails.Id))
            {
                ThrowHelper.ThrowArgumentException(nameof(userDetails) + ".Id must be set to a value other than null or the empty string", nameof(userDetails));
            }

            var setTag = TaggingUtils.GetSpanSetter(span, out var spanClass);

            // usr.id should always be set, even when PropagateId is true
            setTag(Tags.User.Id, userDetails.Id);

            if (userDetails.PropagateId)
            {
                var base64UserId = Convert.ToBase64String(Encoding.UTF8.GetBytes(userDetails.Id));
                const string propagatedUserIdTag = TagPropagation.PropagatedTagPrefix + Tags.User.Id;
                setTag(propagatedUserIdTag, base64UserId);
            }

            if (userDetails.Email is not null)
            {
                setTag(Tags.User.Email, userDetails.Email);
            }

            if (userDetails.Name is not null)
            {
                setTag(Tags.User.Name, userDetails.Name);
            }

            if (userDetails.SessionId is not null)
            {
                setTag(Tags.User.SessionId, userDetails.SessionId);
            }

            if (userDetails.Role is not null)
            {
                setTag(Tags.User.Role, userDetails.Role);
            }

            if (userDetails.Scope is not null)
            {
                setTag(Tags.User.Scope, userDetails.Scope);
            }

            if (spanClass != null)
            {
                RunBlockingCheck(spanClass, userDetails.Id);
            }
        }
    }
}
