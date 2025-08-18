using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

namespace LogParsing;

public static class LogParser
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
                        var timestamp = DateTimeOffset.ParseExact(match.Groups[1].Value, dateFormat, null);
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
