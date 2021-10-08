// <copyright file="ILoggingEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Log4Net
{
    /// <summary>
    /// log4net.Core.LoggingEvent interface for ducktyping
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface ILoggingEvent
    {
        /// <summary>
        /// Gets the properties of the logging event
        /// </summary>
        IDictionary Properties { get; }
    }
}
