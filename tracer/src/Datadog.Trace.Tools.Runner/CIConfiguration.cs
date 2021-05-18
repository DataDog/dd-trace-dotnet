// <copyright file="CIConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Tools.Runner
{
    internal static class CIConfiguration
    {
        static CIConfiguration()
        {
            if (Utils.GetEnvironmentVariable("TRAVIS") != null)
            {
                CI = CIName.Travis;
            }
            else if (Utils.GetEnvironmentVariable("CIRCLECI") != null)
            {
                CI = CIName.CircleCI;
            }
            else if (Utils.GetEnvironmentVariable("JENKINS_URL") != null)
            {
                CI = CIName.Jenkins;
            }
            else if (Utils.GetEnvironmentVariable("GITLAB_CI") != null)
            {
                CI = CIName.Gitlab;
            }
            else if (Utils.GetEnvironmentVariable("APPVEYOR") != null)
            {
                CI = CIName.AppVeyor;
            }
            else if (Utils.GetEnvironmentVariable("TF_BUILD") != null)
            {
                CI = CIName.AzurePipelines;
            }
            else if (Utils.GetEnvironmentVariable("BITBUCKET_COMMIT") != null)
            {
                CI = CIName.Bitbucket;
            }
            else if (Utils.GetEnvironmentVariable("GITHUB_SHA") != null)
            {
                CI = CIName.GithubActions;
            }
            else if (Utils.GetEnvironmentVariable("TEAMCITY_VERSION") != null)
            {
                CI = CIName.Teamcity;
            }
            else if (Utils.GetEnvironmentVariable("BUILDKITE") != null)
            {
                CI = CIName.Buildkite;
            }
        }

        public enum CIName
        {
            Unknown,
            Travis,
            CircleCI,
            Jenkins,
            Gitlab,
            AppVeyor,
            AzurePipelines,
            Bitbucket,
            GithubActions,
            Teamcity,
            Buildkite
        }

        public static CIName CI { get; private set; }

        public static void SetupCIEnvironmentVariables(Dictionary<string, string> environmentVariables)
        {
            switch (CI)
            {
                case CIName.AzurePipelines:
                    SetupAzureEnvironmentVariables(environmentVariables);
                    break;
                default:
                    Console.Error.WriteLine($"Setting environment variable for '{CI}' CI is not supported.");
                    break;
            }
        }

        private static void SetupAzureEnvironmentVariables(Dictionary<string, string> environmentVariables)
        {
            foreach (KeyValuePair<string, string> item in environmentVariables)
            {
                Console.WriteLine($"##vso[task.setvariable variable={item.Key}]{item.Value}");
            }
        }
    }
}
