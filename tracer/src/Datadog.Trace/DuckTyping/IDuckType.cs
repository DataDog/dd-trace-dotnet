// <copyright file="IDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck type interface
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IDuckType
    {
        /// <summary>
        /// Gets instance
        /// </summary>
        object? Instance { get; }

        /// <summary>
        /// Gets instance Type
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets the instance without boxing to an object
        /// </summary>
        /// <typeparam name="TReturn">Return type (should be compatible with Type)</typeparam>
        /// <returns>Returns the instance</returns>
        ref TReturn? GetInternalDuckTypedInstance<TReturn>();

        /// <summary>
        /// Calls ToString() on the instance
        /// </summary>
        /// <returns>ToString result</returns>
        string ToString();
    }
}
