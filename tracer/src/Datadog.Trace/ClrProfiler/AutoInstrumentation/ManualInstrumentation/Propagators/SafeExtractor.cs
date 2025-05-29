// <copyright file="SafeExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Propagators;

/// <summary>
/// An extractor we use to wrap customer extraction functions
/// to force nullable constraints and handle exceptions.
/// </summary>
internal readonly struct SafeExtractor<TCarrier>
{
    private readonly Func<TCarrier, string, IEnumerable<string?>> _extractor;

    public SafeExtractor(Func<TCarrier, string, IEnumerable<string?>> extractor)
    {
        _extractor = extractor;
    }

    public IEnumerable<string?> SafeExtract(TCarrier carrier, string key)
    {
        try
        {
            return _extractor(carrier, key) ?? [];
        }
        catch (Exception)
        {
            // This is a customer error, so we don't want to log it
            // There's a question of how/if we should throw _sometihng_ here to propagate the error
            // to the customer, but for now, just swallow it;
            return [];
        }
    }
}
