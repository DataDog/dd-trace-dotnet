// <copyright file="IMessageBus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

/// <summary>
/// IMessageBus
/// </summary>
internal interface IMessageBus : IDisposable
{
    /// <summary>
    /// QueueMessage method
    /// </summary>
    /// <param name="message">Message instance</param>
    /// <returns>True if the message is queued</returns>
    bool QueueMessage(object? message);
}
