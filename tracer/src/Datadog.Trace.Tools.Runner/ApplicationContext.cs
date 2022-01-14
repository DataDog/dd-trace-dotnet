// <copyright file="ApplicationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;

namespace Datadog.Trace.Tools.Runner
{
    internal class ApplicationContext
    {
        public ApplicationContext(string runnerFolder, Platform platform)
        {
            RunnerFolder = runnerFolder;
            Platform = platform;
        }

        public string RunnerFolder { get; }

        public Platform Platform { get; }

        public CancellationTokenSource TokenSource { get; } = new();
    }
}
