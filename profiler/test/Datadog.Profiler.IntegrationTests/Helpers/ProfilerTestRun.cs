// <copyright file="ProfilerTestRun.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Profiler.IntegrationTests.Helpers.Logs;
using Datadog.Profiler.IntegrationTests.Helpers.Profiles;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    internal sealed class ProfilerTestRun : IDisposable
    {
        private MockDatadogAgent _agent;

        internal ProfilerTestRun(
            MockDatadogAgent agent,
            ProfileOutput profiles,
            ProfilerLogSet logs,
            int uploadCount,
            string processOutput,
            TimeSpan duration,
            string outputDirectory,
            string logDirectory,
            string profileDirectory)
        {
            _agent = agent;
            Profiles = profiles;
            Logs = logs;
            UploadCount = uploadCount;
            ProcessOutput = processOutput;
            Duration = duration;
            OutputDirectory = outputDirectory;
            LogDirectory = logDirectory;
            ProfileDirectory = profileDirectory;
        }

        public ProfileOutput Profiles { get; }

        public ProfilerLogSet Logs { get; }

        public int UploadCount { get; }

        public string ProcessOutput { get; }

        public TimeSpan Duration { get; }

        public string OutputDirectory { get; }

        public string LogDirectory { get; }

        public string ProfileDirectory { get; }

        public void Dispose()
        {
            var agent = _agent;
            _agent = null;
            agent?.Dispose();
        }

        internal static ProfilerTestRun From(TestApplicationRunner runner, MockDatadogAgent agent)
        {
            var logDirectory = runner.Environment.LogDir;
            var profileDirectory = runner.Environment.PprofDir;
            // An existing directory with no profiles is a real read (zero samples).
            // A missing directory means the profiler never started.
            if (!Directory.Exists(profileDirectory))
            {
                throw new XunitException(
                    "The profiler did not create the profile directory '" + profileDirectory + "'." + Environment.NewLine
                    + "Process output:" + Environment.NewLine
                    + runner.ProcessOutput);
            }

            var profiles = ProfileReader.Read(profileDirectory);

            return new ProfilerTestRun(
                agent,
                profiles,
                ProfilerLogSet.Read(logDirectory),
                agent.NbCallsOnProfilingEndpoint,
                runner.ProcessOutput,
                TimeSpan.FromMilliseconds(runner.TotalTestDurationInMilliseconds),
                runner.TestOutputDir,
                logDirectory,
                profileDirectory);
        }
    }
}
