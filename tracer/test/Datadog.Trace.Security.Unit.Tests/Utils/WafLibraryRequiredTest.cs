// <copyright file="WafLibraryRequiredTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.Utils;

[Collection(nameof(SecuritySequentialTests))]
public class WafLibraryRequiredTest
{
    /// <summary>
    /// 15 seconds timeout for the waf. It shouldn't happen, but with a 1sec timeout, the tests are flaky.
    /// </summary>
    public const int TimeoutMicroSeconds = 15_000_000;

    static WafLibraryRequiredTest()
    {
        var result = WafLibraryInvoker.Initialize();
        WafLibraryInvoker = result.WafLibraryInvoker;
    }

    internal static WafLibraryInvoker? WafLibraryInvoker { get; }
}
