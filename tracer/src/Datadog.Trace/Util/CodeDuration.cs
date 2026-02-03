// <copyright file="CodeDuration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Util;

/// <summary>
/// Value-type duration helper that can be used in async methods (it can flow across awaits).
/// </summary>
/// <remarks>
/// Avoid copying; use as a local variable only (copying can lead to multiple Dispose calls).
/// </remarks>
/// <example>
/// <code>
/// using var duration = CodeDuration.Create();
/// await DoWorkAsync();
/// </code>
/// </example>
internal struct CodeDuration : IDisposable
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CodeDuration));
    private readonly string? _prefix;
    private readonly string? _memberName;
    private readonly string? _sourceFilePath;
    private readonly int _sourceLineNumber;
    private long _started;

    private CodeDuration(string memberName, string sourceFilePath, int sourceLineNumber)
    {
        this = default;
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            _prefix = CodeDurationBase.GetPrefix(Interlocked.Increment(ref CodeDurationBase.Count) - 1);
            _memberName = memberName;
            _sourceFilePath = sourceFilePath;
            _sourceLineNumber = sourceLineNumber;
            Log.Debug<string?, string?, string?, int>("{Prefix}[CodeDuration - Start: {MemberName} | {SourceFilePath}:{SourceLineNumber}]", _prefix, _memberName, _sourceFilePath, _sourceLineNumber);
            _started = Stopwatch.GetTimestamp();
        }
    }

    public static CodeDuration Create([CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        return new(memberName, sourceFilePath, sourceLineNumber);
    }

    public void Debug(string message)
    {
        if (_started > 0)
        {
            var currentTime = Stopwatch.GetTimestamp();
            Log.Debug<string?, string?, double, string?>(
                "{Prefix}[CodeDuration - Message: {Message} | {Duration}ms | {MemberName}]",
                _prefix,
                message,
                StopwatchHelpers.GetElapsedMilliseconds(currentTime - _started),
                _memberName);
        }
    }

    public void Dispose()
    {
        var started = _started;
        if (started > 0)
        {
            _started = 0;
            var endTime = Stopwatch.GetTimestamp();
            Interlocked.Decrement(ref CodeDurationBase.Count);
            Log.Debug<string?, double, string?, string?, int>(
                "{Prefix}[CodeDuration - End: {Duration}ms | {MemberName} | {SourceFilePath}:{SourceLineNumber}]",
                _prefix,
                StopwatchHelpers.GetElapsedMilliseconds(endTime - started),
                _memberName,
                _sourceFilePath,
                _sourceLineNumber);
        }
    }
}

/// <summary>
/// Ref-struct duration helper for synchronous <c>using</c> scopes only.
/// </summary>
/// <remarks>
/// Cannot be captured or used across <c>await</c>.
/// </remarks>
/// <example>
/// <code>
/// using var duration = CodeDurationRef.Create();
/// DoWork();
/// </code>
/// </example>
internal ref struct CodeDurationRef
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CodeDurationRef));
    private readonly string? _prefix;
    private readonly string? _memberName;
    private readonly string? _sourceFilePath;
    private readonly int _sourceLineNumber;
    private long _started;

    private CodeDurationRef(string memberName, string sourceFilePath, int sourceLineNumber)
    {
        this = default;
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            _prefix = CodeDurationBase.GetPrefix(Interlocked.Increment(ref CodeDurationBase.Count) - 1);
            _memberName = memberName;
            _sourceFilePath = sourceFilePath;
            _sourceLineNumber = sourceLineNumber;
            Log.Debug<string?, string?, string?, int>("{Prefix}[CodeDuration - Start: {MemberName} | {SourceFilePath}:{SourceLineNumber}]", _prefix, _memberName, _sourceFilePath, _sourceLineNumber);
            _started = Stopwatch.GetTimestamp();
        }
    }

    public static CodeDurationRef Create([CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        return new(memberName, sourceFilePath, sourceLineNumber);
    }

    public void Debug(string message)
    {
        if (_started > 0)
        {
            var currentTime = Stopwatch.GetTimestamp();
            Log.Debug<string?, string?, double, string?>(
                "{Prefix}[CodeDuration - Message: {Message} | {Duration}ms | {MemberName}]",
                _prefix,
                message,
                StopwatchHelpers.GetElapsedMilliseconds(currentTime - _started),
                _memberName);
        }
    }

    public void Dispose()
    {
        var started = _started;
        if (started > 0)
        {
            _started = 0;
            var endTime = Stopwatch.GetTimestamp();
            Interlocked.Decrement(ref CodeDurationBase.Count);
            Log.Debug<string?, double, string?, string?, int>(
                "{Prefix}[CodeDuration - End: {Duration}ms | {MemberName} | {SourceFilePath}:{SourceLineNumber}]",
                _prefix,
                StopwatchHelpers.GetElapsedMilliseconds(endTime - started),
                _memberName,
                _sourceFilePath,
                _sourceLineNumber);
        }
    }
}

internal static class CodeDurationBase
{
    private const int IndentSize = 4;
    private static readonly string[] PrefixCache = BuildPrefixCache();

#pragma warning disable SA1401
    public static int Count;
#pragma warning restore SA1401

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetPrefix(int depth)
    {
        // Fast-path cached prefixes for small nesting depths.
        if ((uint)depth < (uint)PrefixCache.Length)
        {
            return PrefixCache[depth];
        }

        // Fallback for deep nesting.
        return new string(' ', depth * IndentSize);
    }

    private static string[] BuildPrefixCache()
    {
        // 0..10 levels should cover most practical nesting without large static init costs.
        var cache = new string[11];
        cache[0] = string.Empty;
        for (var i = 1; i < cache.Length; i++)
        {
            cache[i] = new string(' ', i * IndentSize);
        }

        return cache;
    }
}
