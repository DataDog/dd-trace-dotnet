// <copyright file="IOtelThreadContextPublisher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.LibDatadog.OtelThreadContext;

internal interface IOtelThreadContextPublisher
{
    bool IsEnabled { get; }

    void Set(Span span);

    void Reset();
}
