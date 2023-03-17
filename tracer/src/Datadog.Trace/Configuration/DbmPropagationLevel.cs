// <copyright file="DbmPropagationLevel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Gets or sets a value indicating whether the tracer should propagate service data in db queries
    /// </summary>
    internal enum DbmPropagationLevel
    {
        /// <summary>Nothing should be propagated</summary>
        Disabled,

        /// <summary>Only service tags should be injected</summary>
        Service,

        /// <summary>Both service details and span traceparent should be injected</summary>
        Full
    }
}
