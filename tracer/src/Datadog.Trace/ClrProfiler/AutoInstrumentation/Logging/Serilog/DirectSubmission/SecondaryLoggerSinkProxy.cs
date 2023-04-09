// <copyright file="SecondaryLoggerSinkProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.DirectSubmission;

[DuckCopy]
internal struct SecondaryLoggerSinkProxy
{
    [DuckField(Name = "_logger")]
    public LoggerProxy Logger;
}
