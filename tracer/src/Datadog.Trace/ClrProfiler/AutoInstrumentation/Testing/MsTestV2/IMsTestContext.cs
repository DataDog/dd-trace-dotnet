// <copyright file="IMsTestContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal interface IMsTestContext : IDuckType
{
    string? TestDisplayName { get; }

    int TestRunCount { get; set; }

    object CloneForDataDrivenIteration();

    void SetTestData(object[]? arguments);

    void SetDisplayName(string? displayName);

    IDisposable SetCurrentTestContext(object context);

    void Dispose();
}
