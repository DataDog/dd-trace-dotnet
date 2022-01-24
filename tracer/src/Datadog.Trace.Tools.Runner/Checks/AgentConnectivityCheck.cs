// <copyright file="AgentConnectivityCheck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
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

            return Run(new ImmutableExporterSettings(settings));
        }

        public static async Task<bool> Run(ImmutableExporterSettings settings)
        {
            var payload = Vendors.MessagePack.MessagePackSerializer.Serialize(Array.Empty<Span[]>());

            var requestFactory = TracesTransportStrategy.Get(settings);

            DisplayInfoMessage(settings);

            var request = requestFactory.Create(new Uri(settings.AgentUri, "/v0.4/traces"));

            var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/msgpack");

            try
            {
                var response = await request.PostAsync(new ArraySegment<byte>(payload), "application/msgpack").ConfigureAwait(false);

                if (response.StatusCode != 200)
                {
                    Utils.WriteError(WrongStatusCodeFormat((HttpStatusCode)response.StatusCode));
                    return false;
                }

                var versionHeader = response.GetHeader("Datadog-Agent-Version");

                if (versionHeader != null)
                {
                    AnsiConsole.WriteLine(DetectedAgentVersionFormat(versionHeader));
                }
                else
                {
                    Utils.WriteWarning(AgentDetectionFailed);
                }
            }
            catch (Exception ex)
            {
                Utils.WriteError(ErrorDetectingAgent(settings.AgentUri.ToString(), ex.Message));
                return false;
            }

            return true;
        }

        private static void DisplayInfoMessage(ImmutableExporterSettings settings)
        {
            var transport = "HTTP";
            var endpoint = settings.AgentUri?.ToString();

            if (settings.TracesTransport == TracesTransportType.UnixDomainSocket)
            {
                transport = "domain sockets";
                endpoint = settings.TracesUnixDomainSocketPath;
            }

            AnsiConsole.WriteLine(ConnectToEndpointFormat(endpoint!, transport));
        }
    }
}
