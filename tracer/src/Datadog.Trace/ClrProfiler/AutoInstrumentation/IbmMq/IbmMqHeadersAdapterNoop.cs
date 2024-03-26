// <copyright file="IbmMqHeadersAdapterNoop.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.IbmMq;

/// <summary>
/// Noop adapter used to disable context propagation
/// </summary>
internal readonly struct IbmMqHeadersAdapterNoop : IHeadersCollection
{
    private static readonly string[] EmptyValue = [];

    public IEnumerable<string> GetValues(string name)
    {
        return EmptyValue;
    }

    public void Set(string name, string value)
    {
    }

    public void Add(string name, string value)
    {
    }

    public void Remove(string name)
    {
    }
}
