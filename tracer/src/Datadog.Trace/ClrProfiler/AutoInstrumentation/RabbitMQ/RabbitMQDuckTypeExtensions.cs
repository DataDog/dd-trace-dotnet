// <copyright file="RabbitMQDuckTypeExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

internal static class RabbitMQDuckTypeExtensions
{
    internal static string? GetValue<TCachedString>(this TCachedString cachedString)
        where TCachedString : ICachedStringProxy
        => cachedString.Instance is not null ? cachedString.Value : null;

    // RabbitMQ.Client rejects bodies longer than int.MaxValue, but this runs before that check
    internal static int GetSequenceLength<TBody>(this TBody body)
        where TBody : ISequenceBody, IDuckType
        => body.Instance is null ? 0 : (int)Math.Min(body.Length, int.MaxValue);

    internal static int GetLength<TBody>(this TBody body)
        where TBody : IBody, IDuckType
        => body.Instance is null ? 0 : body.Length;
}
