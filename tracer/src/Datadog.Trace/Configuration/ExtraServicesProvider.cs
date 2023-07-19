// <copyright file="ExtraServicesProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Datadog.Trace.Configuration;

internal class ExtraServicesProvider : IExtraServicesProvider
{
    private const int MaxExtraServices = 64;
    private const string FakeValue = null;

    private readonly ConcurrentDictionary<string, string> _extraServices = new();

    public void AddService(string serviceName)
    {
        if (_extraServices.Count < MaxExtraServices)
        {
            _extraServices.AddOrUpdate(serviceName, FakeValue, (string _, string _) => null!);
        }
    }

    public string[]? GetExtraServices()
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
