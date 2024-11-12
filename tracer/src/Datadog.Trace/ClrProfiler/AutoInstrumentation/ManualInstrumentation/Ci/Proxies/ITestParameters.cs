// <copyright file="ITestParameters.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;

/// <summary>
/// Duck type for Datadog.Trace.Ci.TestParameters in Datadog.Trace.Manual
/// </summary>
[DuckType("Datadog.Trace.Ci.TestParameters", "Datadog.Trace.Manual")]
internal interface ITestParameters : IDuckType
{
    public Dictionary<string, object>? Metadata { get; }

    public Dictionary<string, object>? Arguments { get; }
}
