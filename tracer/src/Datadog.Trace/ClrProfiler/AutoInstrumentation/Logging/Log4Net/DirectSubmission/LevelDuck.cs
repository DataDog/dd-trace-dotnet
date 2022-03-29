// <copyright file="LevelDuck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission
{
    /// <summary>
    /// Duck type for Level
    /// </summary>
    [DuckCopy]
    internal struct LevelDuck
    {
        /// <summary>
        /// Gets the level value
        /// </summary>
        public int Value;
    }
}
