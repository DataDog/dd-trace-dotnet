// <copyright file="MockTracerAgent.Debugger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Immutable;
using System.Threading;

namespace Datadog.Trace.TestHelpers
{
    public partial class MockTracerAgent
    {
        public IImmutableList<string> Snapshots { get; private set; } = ImmutableList<string>.Empty;

        /// <summary>
        /// Wait for the given number of probe snapshots to appear.
        /// </summary>
        /// <param name="count">The expected number of probe snapshots when more than one snapshot is expected (e.g. multiple line probes in method).</param>
        /// <param name="timeoutInMilliseconds">The timeout</param>
        /// <returns>The list of probe snapshots.</returns>
        public IImmutableList<string> WaitForSnapshots(
            int count,
            int timeoutInMilliseconds = 20000)
        {
            var deadline = DateTime.Now.AddMilliseconds(timeoutInMilliseconds);

            IImmutableList<string> snapshots = ImmutableList<string>.Empty;

            while (DateTime.Now < deadline)
            {
                snapshots = Snapshots.ToImmutableList();

                if (snapshots.Count > 0)
                {
                    break;
                }

                Thread.Sleep(100);
            }

            return snapshots;
        }
    }
}
