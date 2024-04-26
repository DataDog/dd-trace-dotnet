// <copyright file="IProbeProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.Snapshots;

#nullable enable
namespace Datadog.Trace.Debugger.Expressions
{
    internal interface IProbeProcessor
    {
        bool ShouldProcess(in ProbeData probeData);

        bool Process<TCapture>(ref CaptureInfo<TCapture> info, IDebuggerSnapshotCreator snapshotCreator, in ProbeData probeData);

        void LogException(Exception ex, IDebuggerSnapshotCreator snapshotCreator);

        IProbeProcessor UpdateProbeProcessor(ProbeDefinition probe);

        IDebuggerSnapshotCreator CreateSnapshotCreator();
    }
}
