// <copyright file="UnitTestResultStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2
{
    /// <summary>
    /// UnitTestResult ducktype struct
    /// </summary>
    [DuckCopy]
    public struct UnitTestResultStruct
    {
        /// <summary>
        /// Gets the error message
        /// </summary>
        public string ErrorMessage;

        /// <summary>
        /// Gets the error stacktrace
        /// </summary>
        public string ErrorStackTrace;

        /// <summary>
        /// Gets the outcome enum
        /// </summary>
        public UnitTestResultOutcome Outcome;
    }
}
