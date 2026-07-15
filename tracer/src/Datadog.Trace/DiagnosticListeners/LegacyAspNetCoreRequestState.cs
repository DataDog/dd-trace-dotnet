// <copyright file="LegacyAspNetCoreRequestState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System.Threading;

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// Holds the state shared by the legacy ASP.NET Core request diagnostic events.
    /// </summary>
    internal sealed class LegacyAspNetCoreRequestState
    {
        private int _completed;

        public LegacyAspNetCoreRequestState(Scope rootScope)
        {
            RootScope = rootScope;
        }

        /// <summary>
        /// Gets the exact request scope created by the start event.
        /// </summary>
        public Scope RootScope { get; }

        /// <summary>
        /// Marks this request complete, returning false when another stop event already completed it.
        /// </summary>
        public bool TryComplete() => Interlocked.Exchange(ref _completed, 1) == 0;
    }
}

#endif
