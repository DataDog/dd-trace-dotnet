// <copyright file="MsTestV2NativeRetriesMtpTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET8_0_OR_GREATER
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

public class MsTestV2NativeRetriesMtpTests(ITestOutputHelper output)
    : MsTestV2NativeRetriesTests("MSTestTestsNativeRetriesMtp", output)
{
    protected override bool UseMtp => true;
}

#endif
