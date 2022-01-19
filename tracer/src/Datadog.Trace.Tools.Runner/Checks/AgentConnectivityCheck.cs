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

using static Datadog.Trace.Tools.Runner.Checks.Resources;

namespace Datadog.Trace.Tools.Runner.Checks
{
    internal class AgentConnectivityCheck
    {
        public static Task<bool> Run(ProcessInfo process)
        {
            var settings = new ExporterSettings(process.Configuration);

            var url = settings.AgentUri.ToString();

            AnsiConsole.WriteLine(DetectedAgentUrlFormat(url));

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
                    Utils.WriteError(WrongStatusCodeFormat(response.StatusCode));
                    return false;
                }

                if (response.Headers.Contains("Datadog-Agent-Version"))
                {
                    AnsiConsole.WriteLine(DetectedAgentVersionFormat(response.Headers.GetValues("Datadog-Agent-Version").First()));
                }
                else
                {
                    Utils.WriteWarning(AgentDetectionFailed);
                }
            }
            catch (Exception ex)
            {
                Utils.WriteError(ErrorDetectingAgent(url, ex.Message));
                return false;
            }

            return true;
        }
    }
}
