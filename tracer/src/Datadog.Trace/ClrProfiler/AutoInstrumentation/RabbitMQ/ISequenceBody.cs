// <copyright file="ISequenceBody.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

/// <summary>
/// Duck type for a System.Buffers.ReadOnlySequence&lt;byte&gt; message body (RabbitMQ.Client 7.3.0+)
/// </summary>
internal interface ISequenceBody
{
    /// <summary>
    /// Gets the length of the message body
    /// </summary>
    long Length { get; }
}
