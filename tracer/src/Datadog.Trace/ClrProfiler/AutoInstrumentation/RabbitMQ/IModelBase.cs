// <copyright file="IModelBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

/// <summary>
/// ModelBase interface for duck typing
/// </summary>
internal interface IModelBase : IDuckType
{
    /// <summary>
    /// Gets the session associated to this model
    /// </summary>
    [Duck]
    public ISession? Session { get; }
}
