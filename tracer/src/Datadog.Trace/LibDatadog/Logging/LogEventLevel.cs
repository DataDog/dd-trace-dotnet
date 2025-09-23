// <copyright file="LogEventLevel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.LibDatadog.Logging;

/// <summary>
/// Represents the level of a log event.
/// <remarks>
/// The levels are mapped to the levels defined in the libdatadog library.
/// In case of a missing direct mapping, the closest level should be used.
/// </remarks>
/// </summary>
internal enum LogEventLevel
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warn = 3,
    Error = 4,
}
