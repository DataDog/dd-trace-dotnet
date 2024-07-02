// <copyright file="NullScope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Stubs;

internal class NullScope : IScope
{
    public static readonly NullScope Instance = new();

    private NullScope()
    {
    }

    public ISpan Span => NullSpan.Instance;

    public void Dispose()
    {
    }

    public void Close()
    {
    }
}
