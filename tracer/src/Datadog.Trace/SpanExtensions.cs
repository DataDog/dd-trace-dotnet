// <copyright file="SpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace
{
    /// <summary>
    /// Extension methods for the <see cref="ISpan"/> interface
    /// </summary>
    internal static class SpanExtensions
    {
        /// <summary>
        /// Sets the details of the user on the local root span
        /// </summary>
        public static void SetUser(this ISpan span, UserDetails userDetails)
        {
            if (userDetails.Email != null)
            {
                span.SetTag(Tags.User.Email, userDetails.Email);
            }

            if (userDetails.Name != null)
            {
                span.SetTag(Tags.User.Name, userDetails.Name);
            }

            if (userDetails.Id != null)
            {
                span.SetTag(Tags.User.Id, userDetails.Id);
            }

            if (userDetails.SessionId != null)
            {
                span.SetTag(Tags.User.SessionId, userDetails.SessionId);
            }

            if (userDetails.Role != null)
            {
                span.SetTag(Tags.User.Role, userDetails.Role);
            }
        }
    }
}
