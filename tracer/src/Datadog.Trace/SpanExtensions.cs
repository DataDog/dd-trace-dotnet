// <copyright file="SpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    /// <summary>
    /// Extension methods for the <see cref="ISpan"/> interface
    /// </summary>
    public static class SpanExtensions
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanExtensions));

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
            TraceContext traceContext = null;
            if (span is Span spanClass)
            {
                traceContext = spanClass.Context.TraceContext;
                localRootSpan = traceContext?.RootSpan ?? span;
            }

            if (userDetails.PropagateId)
            {
                var base64UserId = Convert.ToBase64String(Encoding.UTF8.GetBytes(userDetails.Id));

                var propagationHeaderMaxLength =
                    traceContext?.Tracer.Settings.TagPropagationHeaderMaxLength ??
                        TagPropagation.OutgoingPropagationHeaderMaxLength;
                if (base64UserId.Length > propagationHeaderMaxLength)
                {
                    Log.Warning<string, int>("{Id} is {IdLength} bytes long, which is longer than the configured max length of {MaxLength}", userDetails.Id, base64UserId.Length, propagationHeaderMaxLength);
                }

                localRootSpan.SetTag(TagPropagation.PropagatedTagPrefix + Tags.User.Id, base64UserId);
            }
            else
            {
                localRootSpan.SetTag(Tags.User.Id, userDetails.Id);
            }

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
