// <copyright file="CarrierWithDelegate.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Headers;

/// <summary>
/// Builds an IHeadersCollection from a carrier and a delegate to get or set data.
/// IHeadersCollection is used for performance gains compared to a delegate, gains which a negated when using this class.
/// As such, this is only intended to be used to provide a user-friendly public interface, not as a convenience in internal tracer methods.
/// </summary>
internal readonly struct CarrierWithDelegate<TCarrier> : IHeadersCollection
{
    private readonly TCarrier _carrier;
    private readonly Func<TCarrier, string, IEnumerable<string?>>? _getter;
    private readonly Action<TCarrier, string, string>? _setter;

    public CarrierWithDelegate(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>>? getter = null, Action<TCarrier, string, string>? setter = null)
    {
        _carrier = carrier;
        _getter = getter;
        _setter = setter;
    }

    public IEnumerable<string?> GetValues(string name)
    {
        if (_getter != null)
        {
            return _getter(_carrier, name);
        }

        throw new NotImplementedException();
    }

    public void Set(string name, string value)
    {
        if (_setter != null)
        {
            _setter(_carrier, name, value);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public void Add(string name, string value)
    {
        if (_setter != null)
        {
            _setter(_carrier, name, value);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public void Remove(string name)
    {
        throw new NotImplementedException();
    }
}
