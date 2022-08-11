// <copyright file="DiscoveryService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Agent.DiscoveryService
{
    internal class DiscoveryService : IDiscoveryService
    {
        private static readonly string[] SupportedDebuggerEndpoints = { "debugger/v1/input" };
        private static readonly string[] SupportedConfigurationEndpoints = { "v0.7/config" };

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DiscoveryService>();
        private static readonly object GlobalLock = new();

        private readonly IApiRequestFactory _apiRequestFactory;

        private DiscoveryService(IApiRequestFactory apiRequestFactory)
        {
            _apiRequestFactory = apiRequestFactory;
        }

        public static DiscoveryService Instance { get; private set; }

        public static string[] AllSupportedEndpoints => SupportedDebuggerEndpoints.Concat(SupportedConfigurationEndpoints).ToArray();

        public string ConfigurationEndpoint { get; private set; }

        public string DebuggerEndpoint { get; private set; }

        public string AgentVersion { get; private set; }

        public static DiscoveryService Create(IApiRequestFactory apiRequestFactory)
        {
            lock (GlobalLock)
            {
                return Instance ??= new DiscoveryService(apiRequestFactory);
            }
        }

        public async Task<bool> DiscoverAsync()
        {
            try
            {
                // todo polling
                var uri = _apiRequestFactory.GetEndpoint("info");
                var api = _apiRequestFactory.Create(uri);
                using var response = await api.GetAsync().ConfigureAwait(false);
                if (response.StatusCode != 200)
                {
                    Log.Error("Failed to discover services");
                    return false;
                }

                var content = await response.ReadAsStringAsync().ConfigureAwait(false);
                ProcessDiscoveryResponse(content);

                return true;
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Failed to discover services");
                return false;
            }
        }

        private void ProcessDiscoveryResponse(string content)
        {
            var jObject = JsonConvert.DeserializeObject<JObject>(content);
            AgentVersion = jObject["version"]?.Value<string>();

            var discoveredEndpoints = (jObject["endpoints"] as JArray)?.Values<string>().ToArray();
            if (discoveredEndpoints == null || discoveredEndpoints.Length == 0)
            {
                return;
            }

            ConfigurationEndpoint = "v0.7/config";
               // SupportedConfigurationEndpoints
               // .FirstOrDefault(
               //     supportedEndpoint => discoveredEndpoints.Any(
               //         endpoint => endpoint.Trim('/').Equals(supportedEndpoint, StringComparison.OrdinalIgnoreCase)));

            DebuggerEndpoint = SupportedDebuggerEndpoints
               .FirstOrDefault(
                    supportedEndpoint => discoveredEndpoints.Any(
                        endpoint => endpoint.Trim('/').Equals(supportedEndpoint, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
