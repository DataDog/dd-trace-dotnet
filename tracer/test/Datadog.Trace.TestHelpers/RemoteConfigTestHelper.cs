// <copyright file="RemoteConfigTestHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Protocol.Tuf;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    public static class RemoteConfigTestHelper
    {
        public const int WaitForAcknowledgmentTimeout = 50000;

        internal static GetRcmResponse SetupRcm(this MockTracerAgent agent, ITestOutputHelper output, IEnumerable<(object Config, string ProductName, string Id)> configurations)
        {
            var response = BuildRcmResponse(configurations.Select(c => (JsonConvert.SerializeObject(c.Config), c.ProductName, c.Id)));
            agent.CustomResponses[MockTracerResponseType.RemoteConfig] = new(JsonConvert.SerializeObject(response));
            output.WriteLine($"{DateTime.UtcNow}: Using RCM response: {response}");
            return response;
        }

        internal static async Task<GetRcmRequest> SetupRcmAndWait(this MockTracerAgent agent, ITestOutputHelper output, IEnumerable<(object Config, string ProductName, string Id)> configurations, int timeoutInMilliseconds = WaitForAcknowledgmentTimeout)
        {
            var response = BuildRcmResponse(configurations.Select(c => (JsonConvert.SerializeObject(c.Config), c.ProductName, c.Id)));
            agent.CustomResponses[MockTracerResponseType.RemoteConfig] = new(JsonConvert.SerializeObject(response));
            output.WriteLine($"{DateTime.UtcNow}: Using RCM response: {response} with custom opaque state {response.Targets.Signed.Custom.OpaqueBackendState}");
            var res = await agent.WaitRcmRequestAndReturnMatchingRequest(response, timeoutInMilliseconds: timeoutInMilliseconds);
            return res;
        }

        // doing multiple things here, waiting for the request and return the latest one
        internal static async Task<GetRcmRequest> WaitRcmRequestAndReturnMatchingRequest(this MockTracerAgent agent, GetRcmResponse response, int timeoutInMilliseconds = WaitForAcknowledgmentTimeout)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutInMilliseconds);

            GetRcmRequest request = null;
            while (DateTime.UtcNow < deadline)
            {
                while (agent.RemoteConfigRequests.IsEmpty && DateTime.UtcNow < deadline)
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
                    if (request.Matches(response))
                    {
                        return request;
                    }
                }
            }

            // eventually return a request that might be null or has no config states
            return request;
        }

        internal static async Task<GetRcmRequest> WaitForAcknowledgment(this MockTracerAgent agent, string product, int timeoutInMilliseconds = WaitForAcknowledgmentTimeout)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutInMilliseconds);

            GetRcmRequest request = null;
            while (DateTime.UtcNow < deadline)
            {
                while (agent.RemoteConfigRequests.IsEmpty && DateTime.UtcNow < deadline)
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

                    var configState = request.Client.State.ConfigStates.SingleOrDefault(s => s.Product == product);

                    if (configState != null && configState.ApplyState != ApplyStates.UNACKNOWLEDGED)
                    {
                        return request;
                    }
                }
            }

            // eventually return a request that might be null or has no config states
            return request;
        }

        private static GetRcmResponse BuildRcmResponse(IEnumerable<(string Config, string ProductName, string Id)> configurations)
        {
            var targetFiles = new List<RcmFile>();
            var targets = new Dictionary<string, Target>();
            var clientConfigs = new List<string>();

            foreach (var configuration in configurations)
            {
                var path = $"datadog/2/{configuration.ProductName}/{configuration.Id}/config";

                clientConfigs.Add(path);

                targetFiles.Add(new RcmFile { Path = path, Raw = Encoding.UTF8.GetBytes(configuration.Config) });
                var guid = Guid.NewGuid().ToString();
                targets.Add(path, new Target { Hashes = new Dictionary<string, string> { { "guid", guid } } });
            }

            var root = new TufRoot { Signed = new Signed { Targets = targets, Custom = new TargetsCustom { OpaqueBackendState = Guid.NewGuid().ToString() } } };

            var response = new GetRcmResponse { ClientConfigs = clientConfigs, TargetFiles = targetFiles, Targets = root };
            return response;
        }
    }
}
