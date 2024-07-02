// <copyright file="IScope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace
{
    /// <summary>
    /// A scope is a handle used to manage the concept of an active span.
    /// Meaning that at a given time at most one span is considered active and
    /// all newly created spans that are not created with the ignoreActiveSpan
    /// parameter will be automatically children of the active span.
    /// </summary>
    public partial interface IScope : IDisposable
    {
        /// <summary>
        /// Gets the active span wrapped in this scope
        /// </summary>
        ISpan Span { get; }

        /// <summary>
        /// Closes the current scope and makes its parent scope active
        /// </summary>
        void Close();
    }
}
