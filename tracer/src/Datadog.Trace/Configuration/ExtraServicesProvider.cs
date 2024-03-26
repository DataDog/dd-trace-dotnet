// <copyright file="ExtraServicesProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Datadog.Trace.Configuration;

internal class ExtraServicesProvider
{
    private const int MaxExtraServices = 64;
    private const string? FakeValue = null;

    public static readonly ExtraServicesProvider Instance = new();

    // no concurrent hash set, so use a dictionary with empty values
    private readonly ConcurrentDictionary<string, string?> _extraServices = new(StringComparer.OrdinalIgnoreCase);
    private int _serviceCount = 0;

    internal void AddService(string serviceName)
    {
        // Several threads entering simultaneously can cause MaxExtraService to be exceeded.
        // As long as the list doesn't grow much beyond MaxExtraService we don't care.
        if (_serviceCount >= MaxExtraServices ||
            _extraServices.ContainsKey(serviceName))
        {
            return;
        }

        Interlocked.Increment(ref _serviceCount);
        _extraServices.AddOrUpdate(serviceName, FakeValue, (_, _) => null!);
    }

    internal string[]? GetExtraServices()
    {
        // once extracted the key collection is frozen, no need to worry about an add changing it
        var keysToCopy = _extraServices.Keys;
        var count = keysToCopy.Count;

        if (count > 0)
        {
            var result = new string[count];
            keysToCopy.CopyTo(result, 0);
            return result;
        }

        return null;
    }
}
