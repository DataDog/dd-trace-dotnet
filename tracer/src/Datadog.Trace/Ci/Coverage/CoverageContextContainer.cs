// <copyright file="CoverageContextContainer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Ci.Coverage
{
    /// <summary>
    /// Coverage context container instance
    /// </summary>
    internal sealed class CoverageContextContainer
    {
        private readonly List<CoverageInstruction> _payloads = new(32);

        /// <summary>
        /// Gets or sets a value indicating whether if the coverage is enabled for the context
        /// </summary>
        public bool Enabled
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal set;
        }

            = true;

        /// <summary>
        /// Stores coverage instruction
        /// </summary>
        /// <param name="filePath">Filepath for the range</param>
        /// <param name="range">Range value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Store(string filePath, ulong range)
        {
            // Jit emits better asm code with this data locality, also the global field is marked as readonly
            var payloads = _payloads;
            lock (payloads)
            {
                payloads.Add(new CoverageInstruction(filePath, range));
            }
        }

        /// <summary>
        /// Stores multiple coverage instructions
        /// </summary>
        /// <param name="filePath">Filepath for the range</param>
        /// <param name="range">Range value</param>
        /// <param name="range2">Range2 value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Store(string filePath, ulong range, ulong range2)
        {
            // Jit emits better asm code with this data locality, also the global field is marked as readonly
            var payloads = _payloads;
            lock (payloads)
            {
                payloads.Add(new CoverageInstruction(filePath, range));
                payloads.Add(new CoverageInstruction(filePath, range2));
            }
        }

        /// <summary>
        /// Gets payload data from the context
        /// </summary>
        /// <returns>Instruction array from the context</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CoverageInstruction[] GetPayload()
        {
            // Jit emits better asm code with this data locality, also the global field is marked as readonly
            var payloads = _payloads;
            lock (payloads)
            {
                return payloads.ToArray();
            }
        }
    }
}
