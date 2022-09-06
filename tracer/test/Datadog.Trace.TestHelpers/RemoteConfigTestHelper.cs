// <copyright file="RemoteConfigTestHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    public abstract class RemoteConfigTestHelper : TestHelper
    {
        protected RemoteConfigTestHelper(string sampleAppName, string samplePathOverrides, ITestOutputHelper output)
            : this(new EnvironmentHelper(sampleAppName, typeof(TestHelper), output, samplePathOverrides), output)
        {
        }

        protected RemoteConfigTestHelper(string sampleAppName, string samplePathOverrides, ITestOutputHelper output, bool prependSamplesToAppName)
            : this(new EnvironmentHelper(sampleAppName, typeof(TestHelper), output, samplePathOverrides, prependSamplesToAppName: false), output)
        {
        }

        protected RemoteConfigTestHelper(string sampleAppName, ITestOutputHelper output)
            : this(new EnvironmentHelper(sampleAppName, typeof(TestHelper), output), output)
        {
        }

        protected RemoteConfigTestHelper(EnvironmentHelper environmentHelper, ITestOutputHelper output)
            : base(environmentHelper, output)
        {
            SetupRcm();
        }

        protected void SetupRcm(string path = null)
        {
            if (path == null)
            {
                path = Path.GetTempFileName();
                File.Delete(path);
                Directory.CreateDirectory(path);
                path = Path.Combine(path, "rcm_config.json");

                SetEnvironmentVariable(ConfigurationKeys.Rcm.RequestFilePath, Path.Combine(path, "rcm_request.json"));

                if (File.Exists(path)) { File.Delete(path); }
            }

            SetEnvironmentVariable(ConfigurationKeys.Rcm.FilePath, path);
        }

        protected void CleanupRcm()
        {
            if (EnvironmentHelper.CustomEnvironmentVariables.TryGetValue(ConfigurationKeys.Rcm.FilePath, out var rcmConfigPath))
            {
                if (File.Exists(rcmConfigPath)) { File.Delete(rcmConfigPath); }
                EnvironmentHelper.CustomEnvironmentVariables.Remove(ConfigurationKeys.Rcm.FilePath);
            }

            if (EnvironmentHelper.CustomEnvironmentVariables.TryGetValue(ConfigurationKeys.Rcm.RequestFilePath, out var rcmRequestPath))
            {
                if (File.Exists(rcmRequestPath)) { File.Delete(rcmRequestPath); }
                EnvironmentHelper.CustomEnvironmentVariables.Remove(ConfigurationKeys.Rcm.RequestFilePath);
            }
        }

        protected void WriteRcmFile(IEnumerable<(object Config, string Id)> configurations, string productName)
        {
            var targetFiles = new List<RcmFile>();
            var targets = new Dictionary<string, Target>();
            var clientConfigs = new List<string>();

            foreach (var configuration in configurations)
            {
                var path = $"datadog/2/{productName}/{configuration.Id}/config";
                var content = JsonConvert.SerializeObject(configuration.Config);

                clientConfigs.Add(path);

                targetFiles.Add(new RcmFile()
                {
                    Path = path,
                    Raw = Encoding.UTF8.GetBytes(content)
                });

                targets.Add(path, new Target()
                {
                    Hashes = new Dictionary<string, string> { { "guid", Guid.NewGuid().ToString() } }
                });
            }

            var root = new TufRoot()
            {
                Signed = new Signed()
                {
                    Targets = targets
                }
            };

            var response = new GetRcmResponse()
            {
                ClientConfigs = clientConfigs,
                TargetFiles = targetFiles,
                Targets = root
            };

            var json = JsonConvert.SerializeObject(response);
            if (EnvironmentHelper.CustomEnvironmentVariables.TryGetValue(ConfigurationKeys.Rcm.FilePath, out var rcmConfigPath))
            {
                File.WriteAllText(rcmConfigPath, json);
                Console.WriteLine($"Writing Remote Config at {rcmConfigPath}");
            }
            else
            {
                throw new InvalidOperationException("Path for remote configurations is not set.");
            }
        }
    }
}
