// <copyright file="RemoteConfigTestHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    public static class RemoteConfigTestHelper
    {
        public static void SetupRcm(this MockTracerAgent agent, ITestOutputHelper output, IEnumerable<(object Config, string Id)> configurations, string productName, string opaqueBackEndSate = null)
        {
            var response = BuildRcmResponse(configurations.Select(c => (JsonConvert.SerializeObject(c.Config), c.Id)), productName, opaqueBackEndSate);
            agent.RcmResponse = response;
            output.WriteLine("Using RCM response: " + response);
        }

        internal static async Task<GetRcmRequest> SetupRcmAndWait(this MockTracerAgent agent, ITestOutputHelper output, IEnumerable<(object Config, string Id)> configurations, string productName, string opaqueBackEndSate = null)
        {
            var response = BuildRcmResponse(configurations.Select(c => (JsonConvert.SerializeObject(c.Config), c.Id)), productName, opaqueBackEndSate);
            agent.RcmResponse = response;
            output.WriteLine("Using RCM response: " + response);
            var res = await agent.WaitRcmRequestAndReturnLast();
            await Task.Delay(1000);
            return res;
        }

        internal static async Task<GetRcmRequest> SetupRcmAndWait(this MockTracerAgent agent, ITestOutputHelper output, IEnumerable<(string Config, string Id)> configurations, string productName, string opaqueBackEndSate = null)
        {
            var response = BuildRcmResponse(configurations, productName, opaqueBackEndSate);
            agent.RcmResponse = response;
            output.WriteLine("Using RCM response: " + response);
            var res = await agent.WaitRcmRequestAndReturnLast();
            await Task.Delay(1000);
            return res;
        }

        // doing multiple things here, waiting for the request and return the latest one
        internal static async Task<GetRcmRequest> WaitRcmRequestAndReturnLast(this MockTracerAgent agent, int timeoutInMilliseconds = 50000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutInMilliseconds);

            GetRcmRequest request = null;
            while (DateTime.UtcNow < deadline)
            {
                while (agent.RemoteConfigRequests.IsEmpty)
                {
                    await Task.Delay(200);
                }

                string lastRemoteConfigPayload = null;
                while (agent.RemoteConfigRequests.TryDequeue(out var next))
                {
                    lastRemoteConfigPayload = next;
                }

                if (lastRemoteConfigPayload != null)
                {
                    // prefer a request that has been processed, meaning the config states have been filled in
                    request = JsonConvert.DeserializeObject<GetRcmRequest>(lastRemoteConfigPayload);
                    if (request?.Client?.State?.ConfigStates?.Count is not null and > 0)
                    {
                        return request;
                    }
                }
            }

            // eventually return a request that might be null or has no config states
            return request;
        }

        private static string BuildRcmResponse(IEnumerable<(string Config, string Id)> configurations, string productName, string opaqueBackEndSate = null)
        {
            var targetFiles = new List<RcmFile>();
            var targets = new Dictionary<string, Target>();
            var clientConfigs = new List<string>();

            foreach (var configuration in configurations)
            {
                var path = $"datadog/2/{productName}/{configuration.Id}/config";

                clientConfigs.Add(path);

                targetFiles.Add(new RcmFile()
                {
                    Path = path,
                    Raw = Encoding.UTF8.GetBytes(configuration.Config)
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
                    Targets = targets,
                    Custom = new TargetsCustom()
                    {
                        OpaqueBackendState = opaqueBackEndSate
                    }
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
