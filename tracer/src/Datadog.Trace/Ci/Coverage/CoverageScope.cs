// <copyright file="CoverageScope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;

namespace Datadog.Trace.Ci.Coverage
{
    /// <summary>
    /// Coverage scope
    /// </summary>
    public readonly ref struct CoverageScope
    {
        private readonly string _filePath;
        private readonly CoverageContextContainer _container;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal CoverageScope(string filePath, CoverageContextContainer container)
        {
            _filePath = filePath;
            _container = container;
        }

        /// <summary>
        /// Report a running instruction
        /// </summary>
        /// <param name="range">Range value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Report(ulong range)
        {
            _container.Store(_filePath, range);
        }

        /// <summary>
        /// Report a running instruction
        /// </summary>
        /// <param name="range">Range value</param>
        /// <param name="range2">Range2 value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Report(ulong range, ulong range2)
        {
            _container.Store(_filePath, range, range2);
        }
    }
}
