// <copyright file="WebHeadersExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Net;
using Datadog.Trace.Headers;
using Datadog.Trace.Util;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for <see cref="WebHeaderCollection"/> objects.
    /// </summary>
    internal static class WebHeadersExtensions
    {
        /// <summary>
        /// Provides an <see cref="WebHeadersCollection"/> implementation that wraps the specified <see cref="WebHeaderCollection"/>.
        /// </summary>
        /// <param name="headers">The Web headers to wrap.</param>
        /// <returns>An object that implements <see cref="IHeadersCollection"/>.</returns>
        public static WebHeadersCollection Wrap(this WebHeaderCollection headers)
        {
            ThrowHelper.ThrowArgumentNullExceptionIfNull(headers);

            return new WebHeadersCollection(headers);
        }
    }
}

#endif
