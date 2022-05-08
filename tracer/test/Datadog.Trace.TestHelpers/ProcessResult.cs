// <copyright file="ProcessResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;

namespace Datadog.Trace.TestHelpers
{
    public class ProcessResult : IDisposable
    {
        public ProcessResult(
            Process process,
            string standardOutput,
            string standardError,
            int exitCode)
        {
            Process = process;
            StandardOutput = standardOutput;
            StandardError = standardError;
            ExitCode = exitCode;
        }

        public Process Process { get; }

        public string StandardOutput { get; }

        public string StandardError { get; }

        public int ExitCode { get; }

        public void Dispose()
        {
            Process?.Dispose();
        }
    }
}
