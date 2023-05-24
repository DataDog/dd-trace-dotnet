// <copyright file="ProcessStartCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Process
{
    internal static class ProcessStartCommon
    {
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.Process;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProcessStartCommon));
        internal const string OperationName = "command_execution";
        internal const string ServiceName = "command";
        internal const int MaxCommandLineLength = 4096;

        internal static Scope CreateScope(ProcessStartInfo info)
        {
            if (info != null)
            {
#if NETFRAMEWORK || NETSTANDARD2_0
                return CreateScope(info.FileName, info.UseShellExecute ? null : info.Environment, info.UseShellExecute, info.Arguments);
#else
                return CreateScope(info.FileName, info.UseShellExecute ? null : info.Environment, info.UseShellExecute, info.Arguments, info.ArgumentList);
#endif
            }

            return null;
        }

        internal static Scope CreateScope(string filename, IDictionary<string, string> environmentVariables, bool useShellExecute, string arguments, Collection<string> argumentList = null)
        {
            var tracer = Tracer.Instance;
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this span
                return null;
            }

            Scope scope = null;

            // Arguments > ArgumentList : If Arguments is used, ArgumentList is ignored. ArgumentsList is only used if Arguments is an empty string.

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

        internal static ProcessCommandStartTags PopulateTags(string filename, IDictionary<string, string> environmentVariables, bool useShellExecute, string arguments, Collection<string> argumentList)
        {
            // Environment variables
            var variablesTruncated = EnvironmentVariablesScrubber.ScrubEnvironmentVariables(environmentVariables);
            variablesTruncated = Truncate(variablesTruncated, MaxCommandLineLength, out _);

            var tags = new ProcessCommandStartTags
            {
                EnvironmentVariables = variablesTruncated,
                Component = "process",
            };

            // Don't populate further with command line information if shell collection is disabled
            if (!Security.Instance.EnableShellCollection)
            {
                return tags;
            }

            if (useShellExecute)
            {
                // cmd.shell
                var commandLine = filename;

                // Append the arguments to the command line
                if (!string.IsNullOrWhiteSpace(arguments))
                {
                    commandLine += " " + arguments;
                }
                else if (argumentList != null)
                {
                    foreach (var argument in argumentList)
                    {
                        commandLine += " " + argument;
                    }
                }

                // Truncate the command line if needed
                commandLine = Truncate(commandLine, MaxCommandLineLength, out var truncated);
                tags.Truncated = truncated ? "true" : null;

                tags.CommandShell = commandLine;
            }
            else
            {
                // cmd.exec
                var commandExec = new List<string> { filename };
                if (!string.IsNullOrWhiteSpace(arguments))
                {
                    // Arguments are provided in a raw strings, we need to split them
                    var split = arguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    commandExec.AddRange(split);
                }
                else if (argumentList is not null)
                {
                    // Arguments are provided as a list of strings
                    commandExec.AddRange(argumentList);
                }

                // The cumulated size of the strings in the array shall not exceed 4kB
                var size = MaxCommandLineLength;
                var truncated = false;
                var finalCommandExec = new Collection<string>();
                foreach (var arg in commandExec)
                {
                    if (truncated)
                    {
                        finalCommandExec.Add(string.Empty);
                        continue;
                    }

                    var truncatedArg = Truncate(arg, MaxCommandLineLength, out truncated);
                    finalCommandExec.Add(truncatedArg);
                    size -= truncatedArg.Length;

                    if (size <= 0)
                    {
                        truncated = true;
                    }
                }

                // Serialized as JSON array string because tracer only supports string values
                tags.CommandExec = JsonConvert.SerializeObject(finalCommandExec);
                tags.Truncated = truncated ? "true" : null;
            }

            return tags;
        }

        internal static string Truncate(string value, int maxLength, out bool truncated)
        {
            truncated = false;
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            truncated = true;
            return value.Substring(0, maxLength);
        }

        internal static void SetExitCode(object instance)
        {
            try
            {
                if (!instance.TryDuckCast<ProcessProxy>(out var process))
                {
                    return;
                }

                var scope = Tracer.Instance.InternalActiveScope;
                var span = scope?.Span;
                span?.Tags.SetTag(Trace.Tags.ProcessExitCode, process.ExitCode.ToString());
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error setting exit code.");
            }
        }
    }
}
