// <copyright file="IExceptionAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Data;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

/// <summary>
/// Exception aggregator interface
/// </summary>
internal interface IExceptionAggregator
{
    /// <summary>
    /// Extract exception
    /// </summary>
    /// <returns>Exception instance</returns>
    Exception ToException();

    /// <summary>
    /// Clears the aggregator.
    /// </summary>
    void Clear();

    /// <summary>
    /// Adds an exception to the aggregator.
    /// </summary>
    /// <param name="ex">The exception to be added.</param>
    void Add(Exception ex);
}
