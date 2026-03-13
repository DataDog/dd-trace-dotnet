// <copyright file="IConsumeContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// Minimal duck-typing interface for MassTransit.BaseConsumeContext.
/// Only includes properties that are concretely public on BaseConsumeContext.
/// </summary>
internal interface IConsumeContext
{
    /// <summary>
    /// Gets the receive context — public concrete property on BaseConsumeContext.
    /// Used to access InputAddress for the process span resource name.
    /// </summary>
    IReceiveContext? ReceiveContext { get; }

    /// <summary>
    /// Gets the message-level headers containing injected trace context.
    /// Works for some context types; TryDuckCast handles types where it fails.
    /// </summary>
    IHeaders? Headers { get; }
}
