// <copyright file="TraceAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Annotations
{
    /// <summary>
    /// Attribute that marks the decorated method to be instrumented
    /// by Datadog automatic instrumentation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class TraceAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the span operation name
        /// </summary>
        public string? OperationName { get; set; }

        /// <summary>
        /// Gets or sets the span resource name
        /// </summary>
        public string? ResourceName { get; set; }
    }
}
