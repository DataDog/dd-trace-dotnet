// <copyright file="CodeDuration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Util;

internal readonly struct CodeDuration : IDisposable
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CodeDuration));
    private readonly long _started;
    private readonly string? _memberName;
    private readonly string? _sourceFilePath;
    private readonly int _sourceLineNumber;

    private CodeDuration(string memberName, string sourceFilePath, int sourceLineNumber)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            _memberName = memberName;
            _sourceFilePath = sourceFilePath;
            _sourceLineNumber = sourceLineNumber;
            Log.Debug<string?, string?, int>(
                "[CodeDuration - Start: {MemberName} | {SourceFilePath}:{SourceLineNumber}]",
                _memberName,
                _sourceFilePath,
                _sourceLineNumber);
            _started = Stopwatch.GetTimestamp();
        }
    }

    public static CodeDuration Create([CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        return new(memberName, sourceFilePath, sourceLineNumber);
    }

    public void Dispose()
    {
        if (_started > 0)
        {
            Log.Debug<double, string?, string?, int>(
                "[CodeDuration - End: {Milliseconds}ms | {MemberName} | {SourceFilePath}:{SourceLineNumber}]",
                StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _started).TotalMilliseconds,
                _memberName,
                _sourceFilePath,
                _sourceLineNumber);
        }
    }
}

internal readonly ref struct CodeDurationRef
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CodeDurationRef));
    private readonly long _started;
    private readonly string? _memberName;
    private readonly string? _sourceFilePath;
    private readonly int _sourceLineNumber;

    private CodeDurationRef(string memberName, string sourceFilePath, int sourceLineNumber)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            _memberName = memberName;
            _sourceFilePath = sourceFilePath;
            _sourceLineNumber = sourceLineNumber;
            Log.Debug<string?, string?, int>(
                "[CodeDuration - Start: {MemberName} | {SourceFilePath}:{SourceLineNumber}]",
                _memberName,
                _sourceFilePath,
                _sourceLineNumber);
            _started = Stopwatch.GetTimestamp();
        }
    }

    public static CodeDurationRef Create([CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        return new(memberName, sourceFilePath, sourceLineNumber);
    }

    public void Dispose()
    {
        if (_started > 0)
        {
            Log.Debug<double, string?, string?, int>(
                "[CodeDuration - End: {Milliseconds}ms | {MemberName} | {SourceFilePath}:{SourceLineNumber}]",
                StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _started).TotalMilliseconds,
                _memberName,
                _sourceFilePath,
                _sourceLineNumber);
        }
    }
}
