// <copyright file="IXunitTestMethodRunnerV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// XunitTestMethodRunner proxy
/// </summary>
internal interface IXunitTestMethodRunnerV3
{
    /// <summary>
    /// Runs the test case.
    /// </summary>
    /// <param name="context">Test context</param>
    /// <param name="testCase">Test case</param>
    IDuckTypeTask<object> RunTestCase(object context, object testCase);
}
