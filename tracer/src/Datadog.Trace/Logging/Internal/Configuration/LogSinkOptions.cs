// <copyright file="LogSinkOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Logging.Internal.Configuration;

/// <summary>
/// Available log sinks for the Datadog logging system.
/// </summary>
internal static class LogSinkOptions
{
    /// <summary>
    /// The file log sink.
    /// </summary>
    public const string File = "file";

    /// <summary>
    /// The console log sink.
    /// </summary>
    /// <remarks>
    /// The console log sink is experimental and unsupported. It may be removed or replaced at any time.
    /// </remarks>
    public const string Console = "console-experimental";
}
