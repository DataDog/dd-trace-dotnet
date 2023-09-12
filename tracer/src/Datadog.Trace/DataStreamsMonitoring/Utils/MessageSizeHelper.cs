// <copyright file="MessageSizeHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Text;

namespace Datadog.Trace.DataStreamsMonitoring.Utils;

internal static class MessageSizeHelper
{
    internal static long TryGetSize(object? obj)
        => obj switch
        {
            null => 0,
            byte[] bytes => bytes.Length,
            string str => Encoding.UTF8.GetByteCount(str),
            _ => 0,
        };
}
