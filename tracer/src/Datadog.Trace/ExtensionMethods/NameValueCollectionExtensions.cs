// <copyright file="NameValueCollectionExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for <see cref="NameValueCollection"/> objects.
    /// </summary>
    internal static class NameValueCollectionExtensions
    {
        /// <summary>
        /// Provides an <see cref="IHeadersCollection"/> implementation that wraps the specified <see cref="NameValueCollection"/>.
        /// </summary>
        /// <param name="collection">The name/value collection to wrap.</param>
        /// <returns>An object that implements <see cref="IHeadersCollection"/>.</returns>
        public static IHeadersCollection Wrap(this NameValueCollection collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            return new NameValueHeadersCollection(collection);
        }
    }
}
