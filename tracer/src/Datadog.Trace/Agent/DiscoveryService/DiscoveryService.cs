// <copyright file="DiscoveryService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Agent.DiscoveryService
{
    internal class DiscoveryService : IDiscoveryService
    {
        private static readonly string[] SupportedDebuggerEndpoints = new[] { "debugger/v1/input" };
        private static readonly string[] SupportedProbeConfigurationEndpoints = new[] { "v0.7/config" };

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DiscoveryService>();

        private readonly IApiRequestFactory _apiRequestFactory;
        private readonly Uri _agentUri;

        private DiscoveryService(
            Uri agentUri,
            IApiRequestFactory apiRequestFactory)
        {
            _agentUri = agentUri;
            _apiRequestFactory = apiRequestFactory;
        }

        public static string[] AllSupportedEndpoints => SupportedDebuggerEndpoints.Concat(SupportedProbeConfigurationEndpoints).ToArray();

        public string ProbeConfigurationEndpoint { get; private set; }

        public string DebuggerEndpoint { get; private set; }

        public string AgentVersion { get; private set; }

        public static DiscoveryService Create(IConfigurationSource configurationSource, IApiRequestFactory apiRequestFactory)
        {
            var exporterSettings = new ExporterSettings(configurationSource);
            return new DiscoveryService(exporterSettings.AgentUri, apiRequestFactory);
        }

        public async Task<bool> DiscoverAsync()
        {
            var sleepDuration = 500; // milliseconds
            var sleepMaxDuration = 5000; // milliseconds

            while (!_cancellationSource.IsCancellationRequested)
            {
                try
                {
                    var uri = _apiRequestFactory.GetEndpoint("info");
                    var api = _apiRequestFactory.Create(uri);

                    using var response = await api.GetAsync().ConfigureAwait(false);
                    if (response.StatusCode == 200)
                    {
                        var content = await response.ReadAsStringAsync().ConfigureAwait(false);
                        ProcessDiscoveryResponse(content);

                        _cancellationSource = null;
                        return true;
                    }

                    Log.Warning("Failed to discover services");
                }
                catch (Exception exception)
                {
                    Log.Warning(exception, "Failed to discover services");
                }

                try
                {
                    await Task.Delay(sleepDuration, _cancellationSource.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return false;
                }

                sleepDuration = Math.Min(sleepDuration * 2, sleepMaxDuration);
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

            ProbeConfigurationEndpoint = SupportedProbeConfigurationEndpoints
               .FirstOrDefault(
                    supportedEndpoint => discoveredEndpoints.Any(
                        endpoint => endpoint.Trim('/').Equals(supportedEndpoint, StringComparison.OrdinalIgnoreCase)));

            DebuggerEndpoint = SupportedDebuggerEndpoints
               .FirstOrDefault(
                    supportedEndpoint => discoveredEndpoints.Any(
                        endpoint => endpoint.Trim('/').Equals(supportedEndpoint, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
