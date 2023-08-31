// <copyright file="CIConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner
{
    internal static class CIConfiguration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CIConfiguration));

        public static bool SetupCIEnvironmentVariables(Dictionary<string, string> environmentVariables, CIName? ci)
        {
            Log.Information("Detecting CI provider...");
            ci ??= AutodetectCi();

            switch (ci)
            {
                case CIName.AzurePipelines:
                    Log.Information("Setting up the environment variables for auto-instrumentation for Azure Pipelines.");
                    // Excluding powershell.exe due to interference in the azure agent making that subsequence script tasks fail.
                    if (environmentVariables.TryGetValue("DD_PROFILER_EXCLUDE_PROCESSES", out var excludeProcesses))
                    {
                        excludeProcesses += ";powershell.exe";
                    }
                    else
                    {
                        excludeProcesses = "powershell.exe";
                    }

                    environmentVariables["DD_PROFILER_EXCLUDE_PROCESSES"] = excludeProcesses;
                    SetupAzureEnvironmentVariables(environmentVariables);
                    return true;
            }

            Utils.WriteError($"Setting environment variable for '{ci}' CI is not supported.");
            return false;
        }

        private static void SetupAzureEnvironmentVariables(Dictionary<string, string> environmentVariables)
        {
            foreach (var item in environmentVariables)
            {
                // Declaring variables for Azure Pipelines
                // https://learn.microsoft.com/en-gb/azure/devops/pipelines/scripts/logging-commands?view=azure-devops&tabs=bash#setvariable-initialize-or-modify-the-value-of-a-variable

                // We cannot use `AnsiConsole.WriteLine` due to the word wrapping and text handling in spectre console that affects the azure command, so we use the normal `Console.WriteLine` instead.
                // See https://github.com/spectreconsole/spectre.console/issues/1122

                Console.WriteLine($"##vso[task.setvariable variable={item.Key};]{item.Value}");
            }
        }

        private static CIName AutodetectCi()
        {
            CIName ciName;

            if (Utils.GetEnvironmentVariable("TRAVIS") != null)
            {
                ciName = CIName.Travis;
            }
            else if (Utils.GetEnvironmentVariable("CIRCLECI") != null)
            {
                ciName = CIName.CircleCI;
            }
            else if (Utils.GetEnvironmentVariable("JENKINS_URL") != null)
            {
                ciName = CIName.Jenkins;
            }
            else if (Utils.GetEnvironmentVariable("GITLAB_CI") != null)
            {
                ciName = CIName.Gitlab;
            }
            else if (Utils.GetEnvironmentVariable("APPVEYOR") != null)
            {
                ciName = CIName.AppVeyor;
            }
            else if (Utils.GetEnvironmentVariable("TF_BUILD") != null)
            {
                ciName = CIName.AzurePipelines;
            }
            else if (Utils.GetEnvironmentVariable("BITBUCKET_COMMIT") != null)
            {
                ciName = CIName.Bitbucket;
            }
            else if (Utils.GetEnvironmentVariable("GITHUB_SHA") != null)
            {
                ciName = CIName.GithubActions;
            }
            else if (Utils.GetEnvironmentVariable("TEAMCITY_VERSION") != null)
            {
                ciName = CIName.Teamcity;
            }
            else if (Utils.GetEnvironmentVariable("BUILDKITE") != null)
            {
                ciName = CIName.Buildkite;
            }
            else
            {
                Utils.WriteError("Failed to autodetect CI.");
                return CIName.Unknown;
            }

            AnsiConsole.WriteLine($"Detected CI {ciName}.");

            return ciName;
        }
    }
}
