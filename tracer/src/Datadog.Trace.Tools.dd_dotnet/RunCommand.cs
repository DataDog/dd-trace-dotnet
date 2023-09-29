// <copyright file="RunCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.Configuration;
using Spectre.Console;

namespace Datadog.Trace.Tools.dd_dotnet
{
    internal class RunCommand : CommandWithExamples
    {
        private readonly RunSettings _runSettings;

        public RunCommand()
            : base("run", "Run a command with the Datadog tracer enabled")
        {
            _runSettings = new RunSettings(this);

            AddExample("dd-trace run -- dotnet myApp.dll");
            AddExample("dd-trace run -- MyApp.exe");

            this.SetHandler(Execute);
        }

        /// <summary>
        /// Convert the arguments array to a string
        /// </summary>
        /// <remarks>
        /// This code is taken from https://source.dot.net/#System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/PasteArguments.cs,624678ba1465e776
        /// </remarks>
        /// <param name="args">Arguments array</param>
        /// <returns>String of arguments</returns>
        private static string GetArgumentsAsString(IEnumerable<string> args)
        {
            const char Quote = '\"';
            const char Backslash = '\\';
            var stringBuilder = new StringBuilder(100);

            foreach (var argument in args)
            {
                if (stringBuilder.Length != 0)
                {
                    stringBuilder.Append(' ');
                }

                // Parsing rules for non-argv[0] arguments:
                //   - Backslash is a normal character except followed by a quote.
                //   - 2N backslashes followed by a quote ==> N literal backslashes followed by unescaped quote
                //   - 2N+1 backslashes followed by a quote ==> N literal backslashes followed by a literal quote
                //   - Parsing stops at first whitespace outside of quoted region.
                //   - (post 2008 rule): A closing quote followed by another quote ==> literal quote, and parsing remains in quoting mode.
                if (argument.Length != 0 && ContainsNoWhitespaceOrQuotes(argument))
                {
                    // Simple case - no quoting or changes needed.
                    stringBuilder.Append(argument);
                }
                else
                {
                    stringBuilder.Append(Quote);
                    int idx = 0;
                    while (idx < argument.Length)
                    {
                        char c = argument[idx++];
                        if (c == Backslash)
                        {
                            int numBackSlash = 1;
                            while (idx < argument.Length && argument[idx] == Backslash)
                            {
                                idx++;
                                numBackSlash++;
                            }

                            if (idx == argument.Length)
                            {
                                // We'll emit an end quote after this so must double the number of backslashes.
                                stringBuilder.Append(Backslash, numBackSlash * 2);
                            }
                            else if (argument[idx] == Quote)
                            {
                                // Backslashes will be followed by a quote. Must double the number of backslashes.
                                stringBuilder.Append(Backslash, (numBackSlash * 2) + 1);
                                stringBuilder.Append(Quote);
                                idx++;
                            }
                            else
                            {
                                // Backslash will not be followed by a quote, so emit as normal characters.
                                stringBuilder.Append(Backslash, numBackSlash);
                            }

                            continue;
                        }

                        if (c == Quote)
                        {
                            // Escape the quote so it appears as a literal. This also guarantees that we won't end up generating a closing quote followed
                            // by another quote (which parses differently pre-2008 vs. post-2008.)
                            stringBuilder.Append(Backslash);
                            stringBuilder.Append(Quote);
                            continue;
                        }

                        stringBuilder.Append(c);
                    }

                    stringBuilder.Append(Quote);
                }
            }

            return stringBuilder.ToString();

            static bool ContainsNoWhitespaceOrQuotes(string s)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (char.IsWhiteSpace(c) || c == Quote)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private static int RunProcess(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            try
            {
                using var childProcess = new Process();
                childProcess.StartInfo = startInfo;
                childProcess.EnableRaisingEvents = true;
                childProcess.Start();

                using (cancellationToken.Register(() =>
                    {
                        try
                        {
                            childProcess.Kill();
                        }
                        catch
                        {
                            // .
                        }
                    }))
                {
                    childProcess.WaitForExit();
                    return cancellationToken.IsCancellationRequested ? 1 : childProcess.ExitCode;
                }
            }
            catch (Exception ex)
            {
                Utils.WriteException(ex);
            }

            return 1;
        }

        private static ProcessStartInfo GetProcessStartInfo(string filename, string currentDirectory, IDictionary<string, string>? environmentVariables)
        {
            var processInfo = new ProcessStartInfo(filename)
            {
                UseShellExecute = false,
                WorkingDirectory = currentDirectory,
            };

            var currentEnvVars = Environment.GetEnvironmentVariables();

            foreach (DictionaryEntry item in currentEnvVars)
            {
                processInfo.Environment[item.Key.ToString()!] = item.Value!.ToString();
            }

            if (environmentVariables != null)
            {
                foreach (var item in environmentVariables)
                {
                    processInfo.Environment[item.Key] = item.Value;
                }
            }

            return processInfo;
        }

        private static bool TryGetEnvironmentVariables(InvocationContext invocationContext, RunSettings settings, out Dictionary<string, string>? profilerEnvironmentVariables)
        {
            profilerEnvironmentVariables = GetProfilerEnvironmentVariables(
                invocationContext,
                settings);

            if (profilerEnvironmentVariables is null)
            {
                return false;
            }

            var additionalEnvironmentVariables = invocationContext.ParseResult.GetValueForOption(settings.AdditionalEnvironmentVariables);

            if (additionalEnvironmentVariables != null)
            {
                foreach (var env in additionalEnvironmentVariables)
                {
                    var values = env.Split('=', 2);
                    var (key, value) = (values[0], values[1]);

                    profilerEnvironmentVariables[key] = value;
                }
            }

            return true;
        }

        private static Dictionary<string, string>? GetProfilerEnvironmentVariables(InvocationContext context, RunSettings options)
        {
            var tracerHomeFolder = options.TracerHome.GetValue(context);

            var envVars = Utils.GetBaseProfilerEnvironmentVariables(tracerHomeFolder);

            if (envVars != null)
            {
                var environment = options.Environment.GetValue(context);

                if (!string.IsNullOrWhiteSpace(environment))
                {
                    envVars[ConfigurationKeys.Environment] = environment;
                }

                var service = options.Service.GetValue(context);

                if (!string.IsNullOrWhiteSpace(service))
                {
                    envVars[ConfigurationKeys.ServiceName] = service;
                }

                var version = options.Version.GetValue(context);

                if (!string.IsNullOrWhiteSpace(version))
                {
                    envVars[ConfigurationKeys.ServiceVersion] = version;
                }

                var agentUrl = options.AgentUrl.GetValue(context);

                if (!string.IsNullOrWhiteSpace(agentUrl))
                {
                    envVars[ConfigurationKeys.AgentUri] = agentUrl;
                }
            }

            return envVars;
        }

        private void Execute(InvocationContext context)
        {
            var args = _runSettings.Command.GetValue(context);
            var program = args[0];
            var arguments = args.Length > 1 ? GetArgumentsAsString(args.Skip(1)) : string.Empty;

            // Get profiler environment variables
            if (!TryGetEnvironmentVariables(context, _runSettings, out var profilerEnvironmentVariables))
            {
                context.ExitCode = 1;
                return;
            }

            if (Program.CallbackForTests != null)
            {
                Program.CallbackForTests(program, arguments, profilerEnvironmentVariables);
                return;
            }

            var processInfo = GetProcessStartInfo(program, Environment.CurrentDirectory, profilerEnvironmentVariables);

            if (!string.IsNullOrEmpty(arguments))
            {
                processInfo.Arguments = arguments;
            }

            context.ExitCode = RunProcess(processInfo, context.GetCancellationToken());
        }
    }
}
