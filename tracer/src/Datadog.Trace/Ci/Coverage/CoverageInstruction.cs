// <copyright file="CoverageInstruction.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Ci.Coverage
{
    /// <summary>
    /// Coverage instruction
    /// </summary>
    public readonly struct CoverageInstruction
    {
        /// <summary>
        /// FilePath value
        /// </summary>
        public readonly string FilePath;

        /// <summary>
        /// Range value
        /// </summary>
        public readonly ulong Range;

        internal CoverageInstruction(string filePath, ulong range)
        {
            FilePath = filePath;
            Range = range;
        }
    }
}
