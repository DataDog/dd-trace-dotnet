// <copyright file="ILogEventPropertyValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.IO;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog.DirectSubmission.Formatting;

/// <summary>
/// Duck type for LogEventPropertyValue
/// https://github.com/serilog/serilog/blob/5e93d5045585095ebcb71ef340d6accd61f01670/src/Serilog/Events/LogEventPropertyValue.cs
/// </summary>
internal interface ILogEventPropertyValue
{
    void Render(TextWriter output, string? format, IFormatProvider? formatProvider);
}
