// <copyright file="IStepDetails.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Gauge
{
    /// <summary>
    /// Step details ducktype interface
    /// </summary>
    public interface IStepDetails
    {
        /// <summary>
        /// Gets a value indicating whether the current spec or scenario or step is failing due to error.
        /// </summary>
        bool IsFailing { get; }

        /// <summary>
        /// Gets the name of the step as given in the spec file.
        /// </summary>
        string Text { get; }
    }
}
