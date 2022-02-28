// <copyright file="CoverageReporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Ci.Coverage
{
    /// <summary>
    /// Coverage Reporter
    /// </summary>
    public static class CoverageReporter
    {
        private static CoverageEventHandler _handler = new DefaultCoverageEventHandler();

        /// <summary>
        /// Gets or sets coverage handler
        /// </summary>
        /// <exception cref="ArgumentNullException">If value is null</exception>
        internal static CoverageEventHandler Handler
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _handler;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _handler = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Tries to get a coverage scope instance for the current context
        /// </summary>
        /// <param name="filePath">Filepath</param>
        /// <param name="scope">Coverage scope instance</param>
        /// <returns>True if the scope could be created; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetScope(string filePath, out CoverageScope scope)
        {
            return _handler.TryGetScope(filePath, out scope);
        }
    }
}
