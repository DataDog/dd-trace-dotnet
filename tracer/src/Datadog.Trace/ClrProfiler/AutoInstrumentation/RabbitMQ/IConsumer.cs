// <copyright file="IConsumer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

/// <summary>
/// Consumer interface for duck typing
/// </summary>
[DuckCopy]
internal struct IConsumer
{
    /// <summary>
    /// Gets the model.
    /// </summary>
    [Duck]
    public IModelBase Model;
}
