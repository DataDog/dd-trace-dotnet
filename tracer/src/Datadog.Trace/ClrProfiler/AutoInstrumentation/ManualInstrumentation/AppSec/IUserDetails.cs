// <copyright file="IUserDetails.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.AppSec;

internal interface IUserDetails
{
    string Id { get; }

    string? Name { get; }

    string? Email { get; }

    string? SessionId { get; }

    string? Role { get; }

    string? Scope { get; }

    bool PropagateId { get; }
}
