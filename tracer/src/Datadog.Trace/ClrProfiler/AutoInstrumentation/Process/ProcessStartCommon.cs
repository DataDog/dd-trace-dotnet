// <copyright file="ProcessStartCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

#nullable enable
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Process
{
    internal static class ProcessStartCommon
    {
        private const IntegrationId IntegrationId = Configuration.IntegrationId.Process;
        private const string OperationName = "command_execution";
        private const string ServiceName = "command";
        internal const int MaxCommandLineLength = 4096;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProcessStartCommon));

        internal static Scope? CreateScope(ProcessStartInfo? info)
        {
            if (info is not null)
            {
#if NETCOREAPP3_1_OR_GREATER
                return CreateScope(info.FileName, info.UseShellExecute ? null : info.Environment, info.UseShellExecute, info.Arguments, info.ArgumentList);
#else
                return CreateScope(info.FileName, info.UseShellExecute ? null : info.Environment, info.UseShellExecute, info.Arguments);
#endif
            }

            return null;
        }

        private static Scope? CreateScope(string filename, IDictionary<string, string?>? environmentVariables, bool useShellExecute, string arguments, Collection<string>? argumentList = null)
        {
            var tracer = Tracer.Instance;
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this span
                return null;
            }

            Scope? scope = null;

            try
            {
                var tags = PopulateTags(filename, environmentVariables, useShellExecute, arguments, argumentList);

                var serviceName = tracer.CurrentTraceSettings.GetServiceName(tracer, ServiceName);
                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
                scope = tracer.StartActiveInternal(OperationName, serviceName: serviceName, tags: tags);
                scope.Span.ResourceName = filename;
                scope.Span.Type = SpanTypes.System;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating execute command scope.");
            }

            return scope;
        }

        /// <summary>
        /// Arguments > ArgumentList : If Arguments is used, ArgumentList is ignored. ArgumentsList is only used if Arguments is an empty string.
        /// </summary>
        private static ProcessCommandStartTags PopulateTags(string filename, IDictionary<string, string?>? environmentVariables, bool useShellExecute, string arguments, Collection<string>? argumentList)
        {
            // Environment variables
            var variablesTruncated = EnvironmentVariablesScrubber.ScrubEnvironmentVariables(environmentVariables);
            variablesTruncated = Truncate(variablesTruncated, MaxCommandLineLength, out _);

            var tags = new ProcessCommandStartTags
            {
                EnvironmentVariables = variablesTruncated,
            };

            // Don't populate further with command line information if shell collection is disabled
            if (!Tracer.Instance.Settings.CommandsCollectionEnabled)
            {
                return tags;
            }

            if (useShellExecute)
            {
                // cmd.shell
                string commandLine;
                var truncated = false;

                // Append the arguments to the command line
                if (!string.IsNullOrWhiteSpace(arguments))
                {
                    commandLine = Truncate($"{filename} {arguments}", MaxCommandLineLength, out truncated);
                }
                else if (argumentList is { Count: > 0 })
                {
                    var maxCommandLineLength = MaxCommandLineLength - filename.Length;

                    var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
                    sb.Append(filename);
                    foreach (var arg in argumentList)
                    {
                        if (maxCommandLineLength - arg.Length - 1 < 0)
                        {
                            // Truncate the argument if needed
                            var truncatedArg = $" {arg}".Substring(0, maxCommandLineLength);
                            sb.Append(truncatedArg);
                            truncated = true;

                            break;
                        }

                        sb.Append(' ').Append(arg);
                        maxCommandLineLength -= arg.Length + 1;
                    }

                    commandLine = StringBuilderCache.GetStringAndRelease(sb);
                }
                else
                {
                    commandLine = filename;
                }

                tags.Truncated = truncated ? "true" : null;
                tags.CommandShell = commandLine;
            }
            else
            {
                // cmd.exec

                // Truncate filename if needed
                filename = Truncate(filename, MaxCommandLineLength, out var truncated);
                var maxCommandLineLength = MaxCommandLineLength - filename.Length;

                Collection<string> finalCommandExec;

                if (!string.IsNullOrWhiteSpace(arguments))
                {
                    if (!truncated)
                    {
                        // Arguments are provided in a raw strings, we need to lex them
                        finalCommandExec = SplitStringIntoArguments(arguments, maxCommandLineLength, out truncated);
                    }
                    else
                    {
                        finalCommandExec = new Collection<string>();
                    }

                    // Add the filename at the beginning of the array
                    finalCommandExec.Insert(0, filename);

                    // Serialized as JSON array string because tracer only supports string values
                    tags.CommandExec = JsonConvert.SerializeObject(finalCommandExec);
                }
                else if (argumentList is not null && argumentList.Count > 0)
                {
                    // The cumulated size of the strings in the array shall not exceed 4kB
                    finalCommandExec = new Collection<string> { filename };
                    foreach (var arg in argumentList)
                    {
                        if (truncated)
                        {
                            // finalCommandExec.Add(string.Empty);
                            // continue;
                            break;
                        }

                        var truncatedArg = Truncate(arg, maxCommandLineLength, out truncated);
                        finalCommandExec.Add(truncatedArg);
                        maxCommandLineLength -= truncatedArg.Length;

                        if (maxCommandLineLength <= 0)
                        {
                            truncated = true;
                        }
                    }

                    // Serialized as JSON array string because tracer only supports string values
                    tags.CommandExec = JsonConvert.SerializeObject(finalCommandExec);
                }
                else
                {
                    tags.CommandExec = JsonConvert.SerializeObject(new[] { filename });
                }

                tags.Truncated = truncated ? "true" : null;
            }

            return tags;
        }

        private static string Truncate(string value, int maxLength, out bool truncated)
        {
            truncated = false;
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            truncated = true;
            return value.Substring(0, maxLength);
        }

        internal static Collection<string> SplitStringIntoArguments(string input, int maxLength, out bool truncated)
        {
            var result = new Collection<string>();
            var currentArgument = StringBuilderCache.Acquire(0);
            var inSingleQuotes = false;
            var inDoubleQuotes = false;
            var escapeNextCharacter = false;
            var currentLength = 0;
            truncated = false;

            bool AddArgument(StringBuilder argument)
            {
                // Check if the max length is reached when we add the argument
                // Split the argument if needed to not exceed the max length
                // Return true if the max length is reached and the argument is truncated
                var argumentLength = argument.Length;
                if (currentLength + argumentLength > maxLength)
                {
                    var nbrCharToKeep = maxLength - currentLength;
                    if (nbrCharToKeep <= 0)
                    {
                        return true;
                    }

                    var truncatedArgument = argument.ToString(0, nbrCharToKeep);
                    result.Add(truncatedArgument);
                    currentLength += truncatedArgument.Length;
                    return true;
                }

                result.Add(argument.ToString());
                currentLength += argumentLength;
                return false;
            }

            foreach (var currentChar in input)
            {
                if (escapeNextCharacter)
                {
                    currentArgument.Append(currentChar);
                    escapeNextCharacter = false;
                }
                else if (currentChar == '\\')
                {
                    escapeNextCharacter = true;
                }
                else if (currentChar == '"' && !inSingleQuotes)
                {
                    inDoubleQuotes = !inDoubleQuotes;
                }
                else if (currentChar == '\'' && !inDoubleQuotes)
                {
                    inSingleQuotes = !inSingleQuotes;
                }
                else if (currentChar == ' ' && !inSingleQuotes && !inDoubleQuotes)
                {
                    if (currentArgument.Length > 0)
                    {
                        if (AddArgument(currentArgument))
                        {
                            truncated = true;
                            return result;
                        }

                        currentArgument.Clear();
                    }
                }
                else
                {
                    currentArgument.Append(currentChar);
                }
            }

            // Add the last argument if it's not empty
            if (currentArgument.Length > 0 && AddArgument(currentArgument))
            {
                truncated = true;
            }

            // Release the StringBuilder
            StringBuilderCache.Release(currentArgument);

            return result;
        }
    }
}
