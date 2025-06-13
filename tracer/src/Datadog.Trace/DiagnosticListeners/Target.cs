// <copyright file="Target.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;

#nullable enable

namespace Datadog.Trace.DiagnosticListeners;

/// <summary>
/// RequestDelegate.Target for duck typing
/// </summary>
[DuckCopy]
internal struct Target
{
    /// <summary>
    /// Delegate to RequestDelegate.Target.handler
    /// </summary>
    [DuckField(Name = "handler")]
    internal Delegate? Handler;
}
