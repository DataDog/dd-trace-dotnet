// <copyright file="IBinaryHeadersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Headers;

/// <summary>
/// Specified a common interface that can be used to manipulate collections of binary headers.
/// </summary>
internal interface IBinaryHeadersCollection
{
    /// <summary>
    /// Returns the first header value for a specified header stored in the collection.
    /// </summary>
    /// <param name="name">The specified header to return values for.</param>
    /// <returns>Zero or more header strings.</returns>
    byte[]? TryGetBytes(string name);

    /// <summary>
    /// Adds the specified header and its value into the collection.
    /// </summary>
    /// <param name="name">The header to add to the collection.</param>
    /// <param name="value">The content of the header.</param>
    void Add(string name, byte[] value);
}
