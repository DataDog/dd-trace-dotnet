// <copyright file="ITestDuckByRefConversionProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Tests;

internal interface ITestDuckByRefConversionProxy
{
    bool TryGetInner(out ITestDuckByRefConversionInnerProxy value);

    bool RoundtripInner(ref ITestDuckByRefConversionInnerProxy value);

    void Increment(ref object value);

    void GetNumber(out object value);
}
