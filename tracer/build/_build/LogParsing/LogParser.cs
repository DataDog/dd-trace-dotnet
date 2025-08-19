using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

namespace LogParsing;

public static partial class LogParser
{
    /// <summary>
    /// Checks all the logs in the log directory for any unexpected errors
    /// </summary>
    /// <param name="logDirectory">The root directory containing the tracer logs. All child directories will be searched </param>
    /// <param name="knownPatterns">Known error patterns in the logs that we expect to see. These errors will be ignored</param>
    /// <param name="allFilesMustExist">Do we expect all the files to be created, or is it ok if some of the files are missing?</param>
    /// <param name="minLogLevel">The log level of messages to check. e.g. <see cref="LogLevel.Warning"/> means
    /// check for both warnings and errors </param>
    /// <param name="reportablePatterns">Patterns that we don't want to see, but we don't want to fail if we do, so
    /// we report them as a metric to Datadog for tracking</param>
    /// <returns><c>true</c> if problems are found in the log files, <c>false</c> otherwise</returns>
    public static async Task<bool> DoLogsContainErrors(
        AbsolutePath logDirectory,
        List<Regex> knownPatterns,
        bool allFilesMustExist,
        LogLevel minLogLevel,
        List<(string IgnoreReasonTag, Regex Regex)> reportablePatterns)
    {
        if (!logDirectory.Exists())
        {
            Log.Information("Skipping log parsing, directory '{LogDirectory}' not found", logDirectory);
            if (allFilesMustExist)
            {
                return true;
            }
        }

        Dictionary<string, int> reportableMetrics = new();

        var managedFiles = logDirectory.GlobFiles("**/dotnet-tracer-managed-*");
        var managedErrors = managedFiles
                           .SelectMany(ParseManagedLogFiles)
                           .Where(IsProblematic)
                           .ToList();

        var nativeTracerFiles = logDirectory.GlobFiles("**/dotnet-tracer-native-*");
        var nativeTracerErrors = nativeTracerFiles
                                .SelectMany(ParseNativeTracerLogFiles)
                                .Where(IsProblematic)
                                .ToList();

        var nativeProfilerFiles = logDirectory.GlobFiles("**/DD-DotNet-Profiler-Native-*");
        var nativeProfilerErrors = nativeProfilerFiles
                                  .SelectMany(ParseNativeProfilerLogFiles)
                                  .Where(IsProblematic)
                                  .ToList();

        var nativeLoaderFiles = logDirectory.GlobFiles("**/dotnet-native-loader-*");
        var nativeLoaderErrors = nativeLoaderFiles
                                .SelectMany(ParseNativeProfilerLogFiles) // native loader has same format as profiler
                                .Where(IsProblematic)
                                .ToList();

        var libdatadogFiles = logDirectory.GlobFiles("**/dotnet-tracer-libdatadog-*");
        var libdatadogErrors = libdatadogFiles
                              .SelectMany(ParseLibdatadogLogFiles) // native loader has same format as profiler
                              .Where(IsProblematic)
                              .ToList();

        var hasRequiredFiles = !allFilesMustExist
                            || (managedFiles.Count > 0
                                // && libdatadogFiles.Count > 0 Libdatadog exporter is off by default, so we don't require it to be there
                             && nativeTracerFiles.Count > 0
                             && (nativeProfilerFiles.Count > 0 || EnvironmentInfo.IsOsx || EnvironmentInfo.IsArm64) // profiler doesn't support mac or ARM64
                             && nativeLoaderFiles.Count > 0);
        var hasErrors = managedErrors.Count != 0
                     || libdatadogErrors.Count != 0
                     || nativeTracerErrors.Count != 0
                     || nativeProfilerErrors.Count != 0
                     || nativeLoaderErrors.Count != 0;

        if (reportableMetrics.Count > 0)
        {
            Log.Warning("Found reportable (but ignored) problems in the logs");
            await MetricHelper.SendReportableErrorMetrics(Log.Logger, reportableMetrics);
        }

        if (hasRequiredFiles && !hasErrors)
        {
            Log.Information("No problems found in managed or native logs");
            return false;
        }

        if (!hasRequiredFiles)
        {
            Log.Error(
                "Some log files were missing: managed: {ManagedFiles}, native tracer: {NativeTracerFiles}, native profiler: {NativeProfilerFiles}, native loader: {NativeLoaderFiles}, libdatadog {LibdatadogFiles}",
                managedFiles.Count, nativeTracerFiles.Count, nativeProfilerFiles.Count, nativeLoaderFiles.Count, libdatadogFiles.Count);
        }

        if (hasErrors)
        {
            Log.Warning("Found the following problems in log files:");
            var allErrors = managedErrors
                           .Concat(libdatadogErrors)
                           .Concat(nativeTracerErrors)
                           .Concat(nativeProfilerErrors)
                           .Concat(nativeLoaderErrors)
                           .GroupBy(x => x.FileName);

            foreach (var erroredFile in allErrors)
            {
                var errors = erroredFile.Where(x => !ContainsCanary(x)).ToList();
                if (errors.Any())
                {
                    Log.Information("");
                    Log.Error("Found errors in log file '{ErroredFileKey}':", erroredFile.Key);
                    foreach (var error in errors)
                    {
                        Log.Error("{ErrorTimestamp} [{ErrorLevel}] {ErrorMessage}", error.Timestamp, error.Level, error.Message);
                    }
                }

                var canaries = erroredFile.Where(ContainsCanary).ToList();
                if (canaries.Any())
                {
                    Log.Information("");
                    Log.Error("Found usage of canary environment variable in log file '{ErroredFileKey}':", erroredFile.Key);
                    foreach (var canary in canaries)
                    {
                        Log.Error("{CanaryTimestamp} [{CanaryLevel}] {CanaryMessage}", canary.Timestamp, canary.Level, canary.Message);
                    }
                }
            }
        }

        return true;

        bool IsProblematic(ParsedLogLine logLine)
        {
            if (ContainsCanary(logLine))
            {
                return true;
            }

            if (logLine.Level < minLogLevel)
            {
                return false;
            }

            foreach (var pattern in reportablePatterns)
            {
                if (pattern.Regex.IsMatch(logLine.Message))
                {
                    var previous = reportableMetrics.GetValueOrDefault(pattern.IgnoreReasonTag, 0);
                    reportableMetrics[pattern.IgnoreReasonTag] = previous + 1;
                    return false;
                }
            }

            foreach (var pattern in knownPatterns)
            {
                if (pattern.IsMatch(logLine.Message))
                {
                    return false;
                }
            }

            return true;
        }

        bool ContainsCanary(ParsedLogLine logLine)
            => logLine.Message.Contains("SUPER_SECRET_CANARY")
            || logLine.Message.Contains("MySuperSecretCanary");
    }


    /// <summary>
    /// Extracts the native tracer metrics from the logs and reports them to Datadog
    /// </summary>
    /// <param name="logDirectory">The root directory containing the tracer logs. All child directories will be searched </param>
    /// <returns></returns>
    public static async Task ReportNativeMetrics(AbsolutePath logDirectory)
    {
        if (!logDirectory.Exists())
        {
            Log.Information("Skipping metric extraction, directory '{LogDirectory}' not found", logDirectory);
            return;
        }

        // Matches log lines like this:
        // Total time: 12011ms | Total time in Callbacks: 530ms [Initialize=0ms, ModuleLoadFinished=301ms/139, CallTargetRequestRejit=54ms/56, CallTargetRewriter=14ms/7, AssemblyLoadFinished=0ms/139, ModuleUnloadStarted=0ms/0, JitCompilationStarted=59ms/9679, JitInlining=8ms/19931, JitCacheFunctionSearchStarted=89ms/8599, InitializeProfiler=2ms/3, EnqueueRequestRejitForLoadedModules=71ms/42]
        var nativeStatsRegex = new Regex(@"Total time:\s*(\d+)ms\s*\|\s*Total time in Callbacks:\s*(\d+)ms\s*\[(.*)\]\s*$", RegexOptions.Compiled);

        var nativeTracerFiles = logDirectory.GlobFiles("**/dotnet-tracer-native-*");
        var nativeTracerMetrics = nativeTracerFiles
                                 .SelectMany(ParseNativeTracerLogFiles)
                                 .SelectMany(line => ParseMetrics(nativeStatsRegex, line));

        await MetricHelper.SendTracerMetricDistributions(Log.Logger, nativeTracerMetrics);

        static IEnumerable<NativeFunctionMetrics> ParseMetrics(Regex nativeStatsRegex, ParsedLogLine line)
        {
            if (nativeStatsRegex.Match(line.Message) is not { Success: true } m)
            {
                return Enumerable.Empty<NativeFunctionMetrics>();
            }

            var entries = new List<NativeFunctionMetrics>();

            var totalTimeMs = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            entries.Add(new(line.Timestamp, NativeFunctionMetrics.TotalInitializationTime, totalTimeMs, Count: null));

            // We don't bother collecting total time in callbacks because it's the sum of the individual components
            // var totalTimeInCallbacks = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var innerItems = m.Groups[3].Value.AsSpan().Trim();

            // [Initialize=0ms, ModuleLoadFinished=301ms/139, CallTargetRequestRejit=54ms/56, CallTargetRewriter=14ms/7, AssemblyLoadFinished=0ms/139, ModuleUnloadStarted=0ms/0, JitCompilationStarted=59ms/9679, JitInlining=8ms/19931, JitCacheFunctionSearchStarted=89ms/8599, InitializeProfiler=2ms/3, EnqueueRequestRejitForLoadedModules=71ms/42]
            var remainingItems = innerItems;
            do
            {
                var nextIndex = remainingItems.IndexOf(' ');
                var item = remainingItems.Slice(0, nextIndex >= 0 ? nextIndex : remainingItems.Length);
                remainingItems = remainingItems.Slice(item.Length).Trim();

                // Initialize=0ms,
                // ModuleLoadFinished=301ms/139,
                // EnqueueRequestRejitForLoadedModules=71ms/42
                var equalsIndex = item.IndexOf('=');
                var name = item.Slice(0, equalsIndex);

                var remainder = item.Slice(equalsIndex + 1);
                var msIndex = remainder.IndexOf("ms");
                var timeMs = int.Parse(remainder.Slice(0, msIndex));

                int? count = null;
                var slashIndex = remainder.IndexOf('/');
                if (slashIndex != -1)
                {
                    remainder = remainder.Slice(slashIndex + 1);
                    var length = remainder.IndexOf(',') is var i and >= 0 ? i : remainder.Length;
                    count = int.Parse(remainder.Slice(0, length), CultureInfo.InvariantCulture);
                }

                entries.Add(new NativeFunctionMetrics(line.Timestamp, ToSnakeCase(name), timeMs, count));
            } while (remainingItems.Length > 0);

            return entries;
        }

        static string ToSnakeCase(ReadOnlySpan<char> span)
        {
            // assumes it starts with a capital, but skip it
            var capitalCount = 0;
            foreach (var c in span.Slice(1))
            {
                capitalCount += char.IsUpper(c) ? 1 : 0;
            }

            Span<char> dest = stackalloc char[span.Length + capitalCount];
            var pos = -1;
            foreach (var c in span)
            {
                pos++;
                var isUpper = char.IsUpper(c);
                var charToAdd = isUpper ? char.ToLowerInvariant(c) : c;

                if (isUpper && pos >0)
                {
                    dest[pos] = '_';
                    pos++;
                }

                dest[pos] = charToAdd;
            }

            return dest.ToString();
        }
    }

    static List<ParsedLogLine> ParseManagedLogFiles(AbsolutePath logFile)
    {
        var regex = new Regex(@"^(\d\d\d\d\-\d\d\-\d\d\W\d\d\:\d\d\:\d\d\.\d\d\d\W\+\d\d\:\d\d)\W\[(.*?)\]\W(.*)", RegexOptions.Compiled);
        var allLines = File.ReadAllLines(logFile);
        var allLogs = new List<ParsedLogLine>(allLines.Length);
        ParsedLogLine currentLine = null;

        foreach (var line in allLines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var match = regex.Match(line);

            if (match.Success)
            {
                if (currentLine is not null)
                {
                    allLogs.Add(currentLine);
                    currentLine = null;
                }

                try
                {
                    // start of a new log line
                    var timestamp = DateTimeOffset.Parse(match.Groups[1].Value);
                    var level = ParseManagedLogLevel(match.Groups[2].Value);
                    var message = match.Groups[3].Value;
                    currentLine = new ParsedLogLine(timestamp, level, message, logFile);
                }
                catch (Exception ex)
                {
                    Log.Information(ex, "Error parsing line: '{Line}'", line);
                }
            }
            else
            {
                if (currentLine is null)
                {
                    Log.Warning("Incomplete log line: {Line}", line);
                }
                else
                {
                    currentLine = currentLine with { Message = $"{currentLine.Message}{Environment.NewLine}{line}" };
                }
            }
        }

        if (currentLine is not null)
        {
            allLogs.Add(currentLine);
        }

        return allLogs;

        static LogLevel ParseManagedLogLevel(string value)
            => value switch
            {
                "VRB" => LogLevel.Trace,
                "DBG" => LogLevel.Trace,
                "INF" => LogLevel.Normal,
                "WRN" => LogLevel.Warning,
                "ERR" => LogLevel.Error,
                _ => LogLevel.Normal, // Concurrency issues can sometimes garble this so ignore it
            };
    }

    static List<ParsedLogLine> ParseNativeTracerLogFiles(AbsolutePath logFile)
    {
        var regex = new Regex(@"^(\d\d\/\d\d\/\d\d\W\d\d\:\d\d\:\d\d\.\d\d\d\W\w\w)\W\[.*?\]\W\[(.*?)\](.*)", RegexOptions.Compiled);
        return ParseNativeLogs(regex, "MM/dd/yy hh:mm:ss.fff tt", logFile);
    }

    static List<ParsedLogLine> ParseNativeProfilerLogFiles(AbsolutePath logFile)
    {
        var regex = new Regex(@"^\[(\d\d\d\d-\d\d-\d\d\W\d\d\:\d\d\:\d\d\.\d\d\d)\W\|\W([^ ]+)\W[^\]]+\W(.*)", RegexOptions.Compiled);
        return ParseNativeLogs(regex, "yyyy-MM-dd H:mm:ss.fff", logFile);
    }

    static List<ParsedLogLine> ParseNativeLogs(Regex regex, string dateFormat, AbsolutePath logFile)
        {
            var allLines = File.ReadAllLines(logFile);
            var allLogs = new List<ParsedLogLine>(allLines.Length);
            ParsedLogLine currentLine = null;

            foreach (var line in allLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var match = regex.Match(line);
                if (match.Success)
                {
                    if (currentLine is not null)
                    {
                        allLogs.Add(currentLine);
                        currentLine = null;
                    }

                    try
                    {
                        // native logs are on one line
                        var timestamp = DateTimeOffset.ParseExact(match.Groups[1].Value, dateFormat, CultureInfo.InvariantCulture);
                        var level = ParseNativeLogLevel(match.Groups[2].Value);
                        var message = match.Groups[3].Value;
                        currentLine = new ParsedLogLine(timestamp, level, message, logFile);
                    }
                    catch (Exception ex)
                    {
                        Log.Information(ex, "Error parsing line: '{Line}'", line);
                    }
                }
                else
                {
                    if (currentLine is null)
                    {
                        Log.Warning("Incomplete log line: {Line}", line);
                    }
                    else
                    {
                        currentLine = currentLine with { Message = $"{currentLine.Message}{Environment.NewLine}{line}" };
                    }
                }
            }

            if (currentLine is not null)
            {
                allLogs.Add(currentLine);
            }

            return allLogs;

            static LogLevel ParseNativeLogLevel(string value)
                => value switch
                {
                    "trace" => LogLevel.Trace,
                    "debug" => LogLevel.Trace,
                    "info" => LogLevel.Normal,
                    "warning" => LogLevel.Warning,
                    "error" => LogLevel.Error,
                    _ => LogLevel.Normal, // Concurrency issues can sometimes garble this so ignore it
                };
        }

    static List<ParsedLogLine> ParseLibdatadogLogFiles(AbsolutePath logFile)
    {
        var logs = new List<ParsedLogLine>();
        using var reader = new StreamReader(logFile);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var log = Build.LibdatadogLogParser.ParseEntry(line);
            if (log is null)
            {
                continue;
            }

            logs.Add(new ParsedLogLine(
                         log.Timestamp,
                         ParseLibdatadogLogLevel(log.Level),
                         string.Join(", ", log.Fields.Select(kvp => $"{kvp.Key}:{kvp.Value}")),
                         logFile));

        }

        return logs;

        static LogLevel ParseLibdatadogLogLevel(string value)
            => value switch
            {
                "TRACE" => LogLevel.Trace,
                "DEBUG" => LogLevel.Trace,
                "INFO" => LogLevel.Normal,
                "WARN" => LogLevel.Warning,
                "ERROR" => LogLevel.Error,
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown log level")
            };
    }

    private record ParsedLogLine(DateTimeOffset Timestamp, LogLevel Level, string Message, AbsolutePath FileName);
}
