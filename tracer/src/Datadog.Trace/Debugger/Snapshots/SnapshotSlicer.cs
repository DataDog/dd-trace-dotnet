// <copyright file="SnapshotSlicer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Text;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using DatadogDebugger.Util;

namespace Datadog.Trace.Debugger.Snapshots
{
    internal class SnapshotSlicer
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SnapshotSlicer));

        private readonly int _maxDepth;
        private readonly int _maxSnapshotSize;

        internal SnapshotSlicer(int maxDepth, int maxSnapshotSize)
        {
            _maxSnapshotSize = maxSnapshotSize;
            _maxDepth = maxDepth;
        }

        public static SnapshotSlicer Create(DebuggerSettings settings, int maxSnapshotSize = 1024 * 1024)
        {
            return new SnapshotSlicer(settings.MaximumDepthOfMembersToCopy, maxSnapshotSize);
        }

        public string SliceIfNeeded(string probeId, string snapshot)
        {
            try
            {
                return SnapshotPruner.Prune(snapshot, _maxSnapshotSize, _maxDepth);
            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to fit snapshot with probe id {ProbeId} due to exception", probeId);
                return snapshot;
            }
        }
    }
}
