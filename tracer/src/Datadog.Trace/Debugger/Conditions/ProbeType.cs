// <copyright file="ProbeType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.Conditions;

internal enum ProbeType
{
    Snapshot = 0,
    Log = 1,
    Metric = 2
}
