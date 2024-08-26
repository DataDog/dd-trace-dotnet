// <copyright file="TestExceptionNormalizer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    /// <summary>
    /// Important: Should only be used in testing. Not thread-safe.
    /// </summary>
    internal class TestExceptionNormalizer : ExceptionNormalizer
    {
        private StringBuilder? _debug;

        internal int NormalizeAndHashException(string exceptionString, string outerExceptionType, string? innerExceptionType, StringBuilder debug)
        {
            _debug = debug;
            var hash = NormalizeAndHashException(exceptionString, outerExceptionType, innerExceptionType);
            _debug = null;
            return hash;
        }

        protected override int HashLine(VendoredMicrosoftCode.System.ReadOnlySpan<char> line, int fnvHashCode)
        {
            _debug?.AppendLine(line.ToString());
            return base.HashLine(line, fnvHashCode);
        }
    }
}
