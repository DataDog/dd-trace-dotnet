// <copyright file="AgentConnectivityCheck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Spectre.Console;
using static Datadog.Trace.Tools.dd_dotnet.Checks.Resources;

namespace Datadog.Trace.Tools.dd_dotnet.Checks
{
    internal class AgentConnectivityCheck
    {
        public static Task<bool> RunAsync(IConfigurationSource? configurationSource)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine(DdAgentChecks);

            var settings = new ExporterSettings(configurationSource);

            var url = settings.AgentUri.ToString();

            AnsiConsole.WriteLine(DetectedAgentUrlFormat(url));

            return RunAsync(settings);
        }

        public static async Task<bool> RunAsync(ExporterSettings settings)
        {
            var payload = new byte[] { 0x90 };

            DisplayInfoMessage(settings);

            using var httpClient = CreateHttpClient(settings);

            var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/msgpack");

            var baseEndpoint = settings.AgentUri;

            if (settings.TracesTransport is TracesTransportType.WindowsNamedPipe or TracesTransportType.UnixDomainSocket)
            {
                baseEndpoint = new Uri("http://localhost");
            }

            try
            {
                var response = await httpClient.PostAsync(Combine(baseEndpoint, "/v0.4/traces"), content).ConfigureAwait(false);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Utils.WriteError(WrongStatusCodeFormat((int)response.StatusCode));
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
                Utils.WriteError(ErrorDetectingAgent(settings.AgentUri.ToString(), ex.Message));
                return false;
            }

            return true;
        }

        private static Uri Combine(Uri baseUri, string relativePath)
        {
            var builder = new UriBuilder(baseUri);
            builder.Path = Combine(builder.Path, relativePath);
            return builder.Uri;
        }

        private static string Combine(string baseUri, string relativePath)
            => baseUri.EndsWith("/")
                   ? (relativePath.StartsWith("/")
                          ? $"{baseUri.Substring(0, baseUri.Length - 1)}{relativePath}"
                          : $"{baseUri}{relativePath}")
                   : (relativePath.StartsWith("/")
                          ? $"{baseUri}{relativePath}"
                          : $"{baseUri}/{relativePath}");

        private static HttpClient CreateHttpClient(ExporterSettings settings)
        {
            switch (settings.TracesTransport)
            {
                case TracesTransportType.Default:
                    return new HttpClient();

                case TracesTransportType.UnixDomainSocket:
                    {
                        var handler = new SocketsHttpHandler
                        {
                            ConnectCallback = async (context, token) =>
                            {
                                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                                var endpoint = new UnixDomainSocketEndPoint(settings.TracesUnixDomainSocketPathInternal!);
                                await socket.ConnectAsync(endpoint, token).ConfigureAwait(false);
                                return new NetworkStream(socket, ownsSocket: false);
                            }
                        };

                        return new HttpClient(handler);
                    }

                case TracesTransportType.WindowsNamedPipe:
                    {
                        var handler = new SocketsHttpHandler
                        {
                            ConnectCallback = async (context, token) =>
                            {
                                var pipeStream = new NamedPipeClientStream(".", settings.TracesPipeNameInternal!, PipeDirection.InOut, PipeOptions.Asynchronous);
                                await pipeStream.ConnectAsync(500, token).ConfigureAwait(false);
                                return pipeStream;
                            }
                        };

                        return new HttpClient(handler);
                    }

                default:
                    throw new InvalidOperationException("Unexpected transport type: " + settings.TracesTransport);
            }
        }

        private static void DisplayInfoMessage(ExporterSettings settings)
        {
            string transport;
            string endpoint;

            if (settings.TracesTransport == TracesTransportType.UnixDomainSocket)
            {
                transport = "domain sockets";
                endpoint = settings.TracesUnixDomainSocketPathInternal ?? "<not set>";
            }
            else
            {
                transport = "HTTP";
                endpoint = settings.AgentUri.ToString();
            }

            AnsiConsole.WriteLine(ConnectToEndpointFormat(endpoint, transport));
        }
    }
}
