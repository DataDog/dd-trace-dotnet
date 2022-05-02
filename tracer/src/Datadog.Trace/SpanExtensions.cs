// <copyright file="SpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Util;

namespace Datadog.Trace
{
    /// <summary>
    /// Extension methods for the <see cref="ISpan"/> interface
    /// </summary>
    public static class SpanExtensions
    {
        /// <summary>
        /// Sets the details of the user on the local root span
        /// </summary>
        /// <param name="span">The span to be tagged</param>
        /// <param name="userDetails">The details of the current logged on user</param>
        public static void SetUser(this ISpan span, UserDetails userDetails)
        {
            if (span is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(span));
            }

            if (string.IsNullOrEmpty(userDetails.Id))
            {
                ThrowHelper.ThrowArgumentException(nameof(userDetails) + ".Id must be set to a value other than null or the empty string", nameof(userDetails));
            }

            var localRootSpan = span;
            if (span is Span spanClass)
            {
                localRootSpan = spanClass.Context.TraceContext?.RootSpan ?? span;
            }

            localRootSpan.SetTag(Tags.User.Id, userDetails.Id);

            if (userDetails.Email is not null)
            {
                localRootSpan.SetTag(Tags.User.Email, userDetails.Email);
            }

            if (userDetails.Name is not null)
            {
                localRootSpan.SetTag(Tags.User.Name, userDetails.Name);
            }

            if (userDetails.SessionId is not null)
            {
                localRootSpan.SetTag(Tags.User.SessionId, userDetails.SessionId);
            }

            if (userDetails.Role is not null)
            {
                localRootSpan.SetTag(Tags.User.Role, userDetails.Role);
            }

            if (userDetails.Scope is not null)
            {
                localRootSpan.SetTag(Tags.User.Scope, userDetails.Scope);
            }
        }
    }
}
