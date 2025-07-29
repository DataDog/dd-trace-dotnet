// <copyright file="LibDatadogAvailableResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.LibDatadog;

internal struct LibDatadogAvailableResult(bool isAvailable, Exception? exception = null)
{
    public bool IsAvailable { get; } = isAvailable;

    public Exception? Exception { get; } = exception;
}
