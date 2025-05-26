// <copyright file="SafeInjector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Propagators;

/// <summary>
/// An injector we use to wrap customer extraction functions
/// to force nullable constraints and handle exceptions.
/// </summary>
internal readonly struct SafeInjector<TCarrier>
{
    private readonly Action<TCarrier, string, string> _injector;

    public SafeInjector(Action<TCarrier, string, string> injector)
    {
        _injector = injector;
    }

    public void SafeInject(TCarrier carrier, string key, string value)
    {
        try
        {
            _injector(carrier, key, value);
        }
        catch (Exception)
        {
            // This is a customer error, so we don't want to log it
            // There's a question of how/if we should throw _sometihng_ here to propagate the error
            // to the customer, but for now, just swallow it;
        }
    }
}
