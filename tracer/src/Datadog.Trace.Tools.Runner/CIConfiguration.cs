// <copyright file="CIConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
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

            // Excluding powershell.exe and DTAExecutionHost.exe due to interference in the azure agent making that subsequence script tasks fail.
            if (environmentVariables.TryGetValue("DD_PROFILER_EXCLUDE_PROCESSES", out var excludeProcesses))
            {
                excludeProcesses += ";DTAExecutionHost.exe;powershell.exe";
            }
            else
            {
                excludeProcesses = "DTAExecutionHost.exe;powershell.exe";
            }

            environmentVariables["DD_PROFILER_EXCLUDE_PROCESSES"] = excludeProcesses;

            switch (ci)
            {
                case CIName.AzurePipelines:
                    Log.Information("Setting up the environment variables for auto-instrumentation for Azure Pipelines.");
                    SetupAzureEnvironmentVariables(environmentVariables);
                    return true;
                case CIName.Jenkins:
                    Log.Information("Setting up the environment variables for auto-instrumentation in Jenkins.");
                    SetupJenkinsEnvironmentVariables(environmentVariables);
                    return true;
                case CIName.GithubActions:
                    Log.Information("Setting up the environment variables for auto-instrumentation in Github Actions.");
                    SetupGithubActionsEnvironmentVariables(environmentVariables);
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

        private static void SetupJenkinsEnvironmentVariables(Dictionary<string, string> environmentVariables)
        {
            foreach (var item in environmentVariables)
            {
                Console.WriteLine($@"{item.Key}={item.Value}");
            }
        }

        private static void SetupGithubActionsEnvironmentVariables(Dictionary<string, string> environmentVariables)
        {
            // https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions#setting-an-environment-variable
            var environmentVariableFile = Utils.GetEnvironmentVariable("GITHUB_ENV");
            if (string.IsNullOrEmpty(environmentVariableFile))
            {
                Utils.WriteError("Failed to get the environment variable file for Github Actions.");
                return;
            }

            Utils.WriteInfo($"Writing environment variables to {environmentVariableFile}.");
            using var fs = new FileStream(environmentVariableFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var sw = new StreamWriter(fs, EncodingHelpers.Utf8NoBom);
            foreach (var item in environmentVariables)
            {
                Utils.WriteInfo($"Writing: {item.Key}");
                sw.WriteLine($@"{item.Key}={item.Value}");
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
