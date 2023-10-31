// <copyright file="LogLevelStaticsProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;

/// <summary>
/// Proxy for calling the static LogLevel properties
/// </summary>
[DuckCopy]
internal struct LogLevelStaticsProxy
{
    public object MaxLevel;

    public object MinLevel;
}
