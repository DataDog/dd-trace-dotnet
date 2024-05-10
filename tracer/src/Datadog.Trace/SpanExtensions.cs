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
            SetUserInternal(span, userDetails);
        }

        internal static void SetUserInternal(this ISpan span, UserDetails userDetails)
        {
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

        /// <summary>
        /// Add the specified tag to this span.
        /// </summary>
        /// <param name="span">The span to be tagged</param>
        /// <param name="key">The tag's key.</param>
        /// <param name="value">The tag's value.</param>
        /// <returns>This span to allow method chaining.</returns>
        [PublicApi]
        public static ISpan SetTag(this ISpan span, string key, double? value)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.SpanExtensions_SetTag);
            return span.SetTagInternal(key, value);
        }

        internal static ISpan SetTagInternal(this ISpan span, string key, double? value)
        {
            if (span is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(span));
            }

            if (span is Span internalSpan)
            {
                return internalSpan.SetMetric(key, value);
            }

            // If is not an internal span, we add the numeric value as string as a fallback only
            // so it can be converted automatically by the backend (only if a measurement facet is created for this tag)
            return span.SetTag(key, value?.ToString());
        }

        internal static bool IsCiVisibilitySpan(this ISpan span)
            => span.Type is SpanTypes.TestSession or SpanTypes.TestModule or SpanTypes.TestSuite or SpanTypes.Test or SpanTypes.Browser;
    }
}
