// <copyright file="IContainsInput.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.StepFunctions
{
    /// <summary>
    /// MessageAttributes interface for ducktyping
    /// </summary>
    internal interface IContainsInput
    {
        /// <summary>
        /// Gets or sets the input
        /// </summary>
        string? Input { get; set; }
    }
}
