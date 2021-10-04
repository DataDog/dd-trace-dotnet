// <copyright file="LoggingEventLegacyDuck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission
{
    /// <summary>
    /// Duck type for LoggingEvent
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class LoggingEventLegacyDuck : LoggingEventDuckBase
    {
        /// <summary>
        /// Gets the Local time the event was logged
        /// </summary>
        public virtual DateTime TimeStamp { get; }
    }
}
