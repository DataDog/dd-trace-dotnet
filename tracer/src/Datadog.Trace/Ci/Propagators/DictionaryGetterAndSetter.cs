// <copyright file="DictionaryGetterAndSetter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Propagators;

internal readonly struct DictionaryGetterAndSetter : ICarrierGetter<IDictionary>, ICarrierSetter<IDictionary>
{
    public static readonly Func<string, string> EnvironmentVariableKeyProcessor = key => key
                                                                                        .Replace(".", "_")
                                                                                        .Replace("-", "_")
                                                                                        .ToUpperInvariant();

    private readonly Func<string, string>? _keyProcessor;

    public DictionaryGetterAndSetter(Func<string, string>? keyProcessor)
    {
        _keyProcessor = keyProcessor;
    }

    public IEnumerable<string?> Get(IDictionary carrier, string key)
    {
        key = _keyProcessor?.Invoke(key) ?? key;

        if (carrier?.TryGetValue<string>(key, out var value) == true)
        {
            return new[] { value };
        }

        return Enumerable.Empty<string?>();
    }

    public void Set(IDictionary carrier, string key, string value)
    {
        key = _keyProcessor?.Invoke(key) ?? key;
        carrier[key] = value;
    }
}
