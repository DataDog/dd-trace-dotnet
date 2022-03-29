// <copyright file="ILoggerConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.DirectSubmission
{
    /// <summary>
    /// Duck typing for LoggerConfiguration
    /// Interface, as used in instrumentation constraint
    /// </summary>
    internal interface ILoggerConfiguration
    {
        /// <summary>
        /// Gets the
        /// </summary>
        [DuckField(Name = "_logEventSinks")]
        public IList LogEventSinks { get; }
    }
}
