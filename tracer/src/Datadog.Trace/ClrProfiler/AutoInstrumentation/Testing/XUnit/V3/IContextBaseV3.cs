// <copyright file="IContextBaseV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// ContextBase proxy
/// </summary>
internal interface IContextBaseV3 : IDuckType
{
    /// <summary>
    /// Gets the aggregator used for reporting exceptions.
    /// </summary>
    IExceptionAggregator? Aggregator { get; }

    /// <summary>
    /// Gets the message bus to send execution engine messages to.
    /// </summary>
    object MessageBus { get; }
}
