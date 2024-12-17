// <copyright file="ISnapshotSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;

namespace Datadog.Trace.Debugger.Sink;

internal interface ISnapshotSink : IDisposable
{
    public void Add(string probeId, string snapshot);

    IList<string> GetSnapshots();

    int RemainingCapacity();
}
