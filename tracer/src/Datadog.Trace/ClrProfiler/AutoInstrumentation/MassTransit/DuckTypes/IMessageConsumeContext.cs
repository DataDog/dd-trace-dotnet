// <copyright file="IMessageConsumeContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// Duck type for MassTransit.Context.MessageConsumeContext&lt;TMessage&gt;
/// This class wraps a ConsumeContext in a private field _context, so we use DuckField to access it.
/// The _context field contains the actual ConsumeContext with all the properties we need.
/// </summary>
internal interface IMessageConsumeContext
{
    /// <summary>
    /// Gets the underlying ConsumeContext from the private _context field.
    /// This returns an object that should be duck-cast to IConsumeContext.
    /// </summary>
    [DuckField(Name = "_context")]
    object? Context { get; }
}
