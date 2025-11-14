// <copyright file="StatsdConsumer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.DogStatsd;

[Flags]
internal enum StatsdConsumer
{
    // None = 0, Must not use this
    // Define bits per consumer:
    RuntimeMetricsWriter = 1 << 0,
    TraceApi = 1 << 1,
    AgentWriter = 1 << 2,
}
