// <copyright file="IXunitTestMethodRunnerBaseContextV4.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V4;

internal interface IXunitTestMethodRunnerBaseContextV4 : IContextBaseV3
{
    IDuckTypeTask<object> RunTestCase(object testCase);
}
