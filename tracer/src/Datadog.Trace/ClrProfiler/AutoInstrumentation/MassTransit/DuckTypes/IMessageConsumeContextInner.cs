// <copyright file="IMessageConsumeContextInner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// Accesses the inner <c>_context</c> field on <c>MassTransit.Context.MessageConsumeContext&lt;T&gt;</c>.
/// That wrapper type uses explicit interface implementations for all properties, so duck casts against
/// it always fail. The inner context is a <c>BaseConsumeContext</c>-derived type with public properties
/// that duck-cast successfully to <see cref="IConsumeContext"/>.
/// </summary>
internal interface IMessageConsumeContextInner
{
    [DuckField(Name = "_context")]
    IConsumeContext? Context { get; }
}
