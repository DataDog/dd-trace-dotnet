// <copyright file="IExceptionAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit
{
    /// <summary>
    /// Exception aggregator interface
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IExceptionAggregator
    {
        /// <summary>
        /// Extract exception
        /// </summary>
        /// <returns>Exception instance</returns>
        Exception ToException();
    }
}
