// <copyright file="ISpanInternal.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Tagging;

namespace Datadog.Trace;

internal interface ISpanInternal : ISpan
{
    TraceId TraceId128 { get; }

    ulong RootSpanId { get; }

    ITags Tags { get; set; }

    new ISpanContextInternal Context { get; }

    DateTimeOffset StartTime { get; }

    TimeSpan Duration { get; }

    bool IsFinished { get; }

    bool IsRootSpan { get; }

    bool IsTopLevel { get; }

    double? GetMetric(string key);

    ISpanInternal SetMetric(string key, double? value);

    void Finish(TimeSpan duration);

    void ResetStartTime();

    void SetStartTime(DateTimeOffset startTime);

    void SetDuration(TimeSpan duration);

    string ToString();
}
