// <copyright file="MockSpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Globalization;

namespace Datadog.Trace.TestHelpers
{
    public static class MockSpanExtensions
    {
        /// <summary>
        /// Gets the HTTP status code recorded on the span, or <c>null</c> if there isn't one.
        /// </summary>
        /// <remarks>
        /// This is the <see cref="MockSpan"/> counterpart to <c>SpanExtensions.GetHttpStatusCode</c>.
        /// Tests should go through it rather than reading <c>http.status_code</c> directly, so that
        /// adding OTel semantic conventions only has to be handled here.
        /// </remarks>
        /// <param name="span">The span to read the status code from.</param>
        /// <returns>The HTTP status code, or <c>null</c> if the span doesn't have one.</returns>
        public static string GetHttpStatusCodeString(this MockSpan span)
            => span.GetTag(Tags.HttpStatusCode) ?? span.GetTag(Tags.HttpResponseStatusCode);
    }
}
