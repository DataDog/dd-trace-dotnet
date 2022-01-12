// <copyright file="AgentConnectivityCheck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner.Checks
{
    internal class AgentConnectivityCheck
    {
        public static Task<bool> Run(ProcessInfo process)
        {
            // Extract the agent information from the environment variables
            string url;

            if (!process.EnvironmentVariables.TryGetValue(ConfigurationKeys.AgentUri, out url))
            {
                process.EnvironmentVariables.TryGetValue(ConfigurationKeys.AgentHost, out var rawHost);
                process.EnvironmentVariables.TryGetValue(ConfigurationKeys.AgentPort, out var rawPort);

                var host = rawHost ?? ExporterSettings.DefaultAgentHost;

                int port;

                if (!int.TryParse(rawPort, out port))
                {
                    port = ExporterSettings.DefaultAgentPort;
                }

                url = $"http://{host}:{port}";
            }

            url ??= $"http://{ExporterSettings.DefaultAgentHost}:{ExporterSettings.DefaultAgentPort}";

            AnsiConsole.WriteLine($"Detected agent url: {url}. Note: this url may be incorrect if you configured the application through a configuration file.");

            return Run(url);
        }

        public static async Task<bool> Run(string url)
        {
            var payload = Vendors.MessagePack.MessagePackSerializer.Serialize(Array.Empty<Span[]>());

            using var client = new HttpClient();

            var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/msgpack");

            try
            {
                var response = await client.PostAsync($"{url}/v0.4/traces", content).ConfigureAwait(false);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Utils.WriteError($"Agent replied with wrong status code: {response.StatusCode}");
                    return false;
                }

                if (response.Headers.Contains("Datadog-Agent-Version"))
                {
                    AnsiConsole.WriteLine("Detected agent version " + response.Headers.GetValues("Datadog-Agent-Version").First());
                }
                else
                {
                    Utils.WriteWarning("Could not detect the agent version. It may be running with a version older than 7.27.0.");
                }
            }
            catch (Exception ex)
            {
                Utils.WriteError($"Error while trying to reach agent at {url}: {ex.Message}");
                return false;
            }

            return true;
        }
    }
}
