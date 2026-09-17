// <copyright file="TestAttemptResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.Ci;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal struct TestAttemptResult
{
    public TimeSpan Duration { get; set; }

    public bool IsEfdTest { get; set; }

    public bool IsAttemptToFix { get; set; }

    public bool AllowRetries { get; set; }

    public TestStatus ResultStatus { get; set; }

    public bool InitialExecutionPassed { get; set; }

    public bool InitialExecutionFailed { get; set; }
}
