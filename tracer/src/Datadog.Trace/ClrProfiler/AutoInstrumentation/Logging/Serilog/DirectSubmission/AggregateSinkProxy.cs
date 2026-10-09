// <copyright file="AggregateSinkProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.DirectSubmission;

[DuckCopy]
[DuckCopy("Serilog.Core.Sinks.FilteringSink", "Serilog")]
[DuckCopy("Serilog.Core.Sinks.SafeAggregateSink", "Serilog")]
[DuckCopy("Serilog.Sinks.File.FileSink", "Serilog.Sinks.File")]
internal struct AggregateSinkProxy
{
    /// <summary>
    /// Gets the
    /// </summary>
    [DuckField(Name = "_sinks")]
    public ICollection LogEventSinks;
}
