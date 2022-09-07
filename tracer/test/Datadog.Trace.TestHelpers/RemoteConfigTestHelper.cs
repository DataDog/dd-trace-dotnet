// <copyright file="RemoteConfigTestHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    public static class RemoteConfigTestHelper
    {
        public static void SetupRcm(this MockTracerAgent agent, ITestOutputHelper output, IEnumerable<(object Config, string Id)> configurations, string productName)
        {
            var response = BuildRcmResponse(configurations, productName);
            agent.RcmResponse = response;
            output.WriteLine("Using RCM response: " + response);
        }

        private static string BuildRcmResponse(IEnumerable<(object Config, string Id)> configurations, string productName)
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

            return JsonConvert.SerializeObject(response);
        }
    }
}
