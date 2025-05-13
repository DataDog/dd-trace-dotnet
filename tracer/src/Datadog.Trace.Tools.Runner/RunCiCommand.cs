// <copyright file="RunCiCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner
{
    internal class RunCiCommand : CommandWithExamples
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RunCiCommand));

        private readonly ApplicationContext _applicationContext;
        private readonly RunSettings _runSettings;
        private readonly Option<string> _apiKeyOption = new("--api-key", "Enables agentless with the Api Key");

        public RunCiCommand(ApplicationContext applicationContext)
            : base("run", "Run a command in the CI and instrument the tests")
        {
            _applicationContext = applicationContext;

            _runSettings = new(this);
            AddOption(_apiKeyOption);

            AddExample("dd-trace ci run -- dotnet test");

            this.SetHandler(ExecuteAsync);
        }

        private async Task ExecuteAsync(InvocationContext context)
        {
            var args = _runSettings.Command.GetValue(context);
            var program = args[0];

            // Initialize and configure CI Visibility for this command
            var initResults = await CiUtils.InitializeCiCommandsAsync(_applicationContext, context, _runSettings, _apiKeyOption, program, args, false).ConfigureAwait(false);
            if (!initResults.Success)
            {
                return;
            }

            // Final command to execute
            var arguments = Utils.GetArgumentsAsString(initResults.Arguments);
            var command = $"{program} {arguments}".Trim();

            // Propagate original test command and working directory
            if (initResults.ProfilerEnvironmentVariables is { } profilerEnvironmentVariables)
            {
                profilerEnvironmentVariables[TestSuiteVisibilityTags.TestSessionCommandEnvironmentVariable] = Environment.CommandLine;
                profilerEnvironmentVariables[TestSuiteVisibilityTags.TestSessionWorkingDirectoryEnvironmentVariable] = Environment.CurrentDirectory;
            }

            // Run child process
            if (GlobalSettings.Instance.DebugEnabledInternal)
            {
                Console.WriteLine("Running: {0}", command);
            }

            if (initResults.TestSkippingEnabled || Program.CallbackForTests is not null)
            {
                // Awaiting git repository task before running the command if ITR test skipping is enabled.
                // Test skipping requires the git upload metadata information before hand
                await initResults.UploadRepositoryChangesTask().ConfigureAwait(false);
            }

            if (Program.CallbackForTests is { } callbackForTests)
            {
                callbackForTests(program, arguments, initResults.ProfilerEnvironmentVariables);
                return;
            }

            Log.Debug("RunCiCommand: Launching: {Value}", command);
            var processInfo = Utils.GetProcessStartInfo(program, Environment.CurrentDirectory, initResults.ProfilerEnvironmentVariables);
            foreach (var arg in initResults.Arguments)
            {
                processInfo.ArgumentList.Add(arg);
            }

            var exitCode = Utils.RunProcess(processInfo, _applicationContext.TokenSource.Token);
            Log.Debug<int>("RunCiCommand: Finished with exit code: {Value}", exitCode);

            if (!initResults.TestSkippingEnabled)
            {
                // Awaiting git repository task after running the command if ITR test skipping is disabled.
                await initResults.UploadRepositoryChangesTask().ConfigureAwait(false);
            }

            context.ExitCode = exitCode;
        }
    }
}
