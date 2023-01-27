// <copyright file="WafLibraryInvokerFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.Security.Unit.Tests.Utils;

public class WafLibraryInvokerFixture : IDisposable
{
    private WafLibraryInvoker? _wafLibraryInvoker;

    /// <summary>
    /// GC would sometimes dispose of this invoker beforehand, but we have a waf callback registered on it..
    /// </summary>
    public void Dispose()
    {
        if (_wafLibraryInvoker != null)
        {
            GC.KeepAlive(_wafLibraryInvoker);
        }
    }

    internal WafLibraryInvoker Initialize(string? version = null)
    {
        var libInitResult = WafLibraryInvoker.Initialize(version);
        if (!libInitResult.Success)
        {
            throw new ArgumentException("Waf could not load");
        }

        _wafLibraryInvoker = libInitResult.WafLibraryInvoker!;
        return _wafLibraryInvoker!;
    }
}
