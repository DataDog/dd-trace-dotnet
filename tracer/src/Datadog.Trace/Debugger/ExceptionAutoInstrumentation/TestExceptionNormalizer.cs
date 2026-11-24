// <copyright file="TestExceptionNormalizer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;

#nullable enable

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    /// <summary>
    /// Important: Should only be used in testing. Not thread-safe.
    /// </summary>
    internal sealed class TestExceptionNormalizer : ExceptionNormalizer
    {
        private StringBuilder? _debug;

        public TestExceptionNormalizer()
        {
        }

        internal int NormalizeAndHashException(string exceptionString, string outerExceptionType, string? innerExceptionType, StringBuilder debug)
        {
            _debug = debug;
            var hash = NormalizeAndHashException(exceptionString, outerExceptionType, innerExceptionType);
            _debug = null;
            return hash;
        }

        protected override int HashLine(ReadOnlySpan<char> line, int fnvHashCode)
        {
#pragma warning disable MA0028 // Optimize StringBuilder usage - only runs when a test passes in a debug builder
            _debug?.AppendLine(line.ToString());
#pragma warning restore MA0028
            return base.HashLine(line, fnvHashCode);
        }
    }
}
