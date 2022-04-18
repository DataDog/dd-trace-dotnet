// <copyright file="MockTracerAgent.Debugger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.TestHelpers
{
#pragma warning disable SA1601 // Partial elements should be documented
    public partial class MockTracerAgent
#pragma warning restore SA1601 // Partial elements should be documented
    {
        public IImmutableList<string> Snapshots { get; private set; } = ImmutableList<string>.Empty;

        /// <summary>
        /// Wait for the given number of probe snapshots to appear.
        /// </summary>
        /// <param name="count">The expected number of probe snapshots when more than one snapshot is expected (e.g. multiple line probes in method).</param>
        /// <param name="timeoutInMilliseconds">The timeout</param>
        /// <returns>The list of probe snapshots.</returns>
        public string[] WaitForSnapshots(
            int count,
            int timeoutInMilliseconds = 20000)
        {
            var deadline = DateTime.Now.AddMilliseconds(timeoutInMilliseconds);

            var snapshots = Array.Empty<string>();

            while (DateTime.Now < deadline)
            {
                snapshots = Snapshots.ToImmutableList()
                                     .SelectMany(JArray.Parse)
                                     .Select(snapshot => snapshot.ToString())
                                     .ToArray();

                if (snapshots.Length == count)
                {
                    break;
                }

                Thread.Sleep(100);
            }

            return snapshots;
        }

        public bool NoSnapshots(int timeoutInMilliseconds = 50000)
        {
            var deadline = DateTime.Now.AddMilliseconds(timeoutInMilliseconds);
            while (DateTime.Now < deadline)
            {
                if (Snapshots.Any())
                {
                    return false;
                }

                Thread.Sleep(100);
            }

            return !Snapshots.Any();
        }

        public void ClearSnapshots()
        {
            Snapshots = Snapshots.Clear();
        }
    }
}
