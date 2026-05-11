// <copyright file="IisFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;

namespace Datadog.Trace.TestHelpers
{
    [CollectionDefinition("IisTests", DisableParallelization = false)]
    public sealed class IisFixture : GacFixture, IDisposable
    {
        // The agent is needed only so the IIS process can connect to it and flush traces without
        // hanging; it is never read by tests. Kept private intentionally.
        private MockTracerAgent _agent;

        public (ProcessHelper Process, string ConfigFile) IisExpress { get; private set; }

        public int HttpPort { get; private set; }

        public string ShutdownPath { get; set; }

        public string VirtualApplicationPath { get; set; } = string.Empty;

        public bool UseGac { get; set; } = true;

        public bool UsePartialTrust { get; set; } = false;

        public bool UseLegacyCasModel { get; set; } = false;

        public async Task TryStartIis(TestHelper helper, IisAppType appType, bool sendHealthCheck = true, string url = "")
        {
            if (IisExpress.Process == null)
            {
                if (UseGac)
                {
                    AddAssembliesToGac();
                }

                var initialAgentPort = TcpPortProvider.GetOpenPort();
                _agent = MockTracerAgent.Create(null, initialAgentPort);

                HttpPort = TcpPortProvider.GetOpenPort();
                IisExpress = await helper.StartIISExpress(_agent, HttpPort, appType, VirtualApplicationPath, UsePartialTrust, UseLegacyCasModel);

                await EnsureServerStarted(sendHealthCheck, url);
            }
        }

        public void Dispose()
        {
            if (IisExpress.Process != null && ShutdownPath != null)
            {
                try
                {
                    var request = WebRequest.CreateHttp($"http://localhost:{HttpPort}{ShutdownPath}");
                    request.Timeout = 2_000;
                    request.GetResponse().Close();
                }
                catch
                {
                    // best effort — fall through to process kill
                }
            }

            _agent?.Dispose();

            if (IisExpress.Process != null)
            {
                try
                {
                    IisExpress.Process.Dispose(8000);
                }
                catch
                {
                    // in some circumstances the HasExited property throws
                }

                IisExpress.Process.Process.Dispose();

                try
                {
                    File.Delete(IisExpress.ConfigFile);
                }
                catch
                {
                }

                if (UseGac)
                {
                    RemoveAssembliesFromGac();
                }
            }
        }

        private async Task EnsureServerStarted(bool sendHealthCheck, string url)
        {
            var maxMillisecondsToWait = 30_000;
            var intervalMilliseconds = 500;
            var intervals = maxMillisecondsToWait / intervalMilliseconds;
            var serverReady = false;

            while (intervals-- > 0)
            {
                DateTime startTime = DateTime.Now;
                try
                {
                    if (sendHealthCheck)
                    {
                        var request = WebRequest.CreateHttp($"http://localhost:{HttpPort}{url}");
                        var response = request.GetResponse();
                        var responseCode = ((HttpWebResponse)response).StatusCode;
                        response.Close();
                        serverReady = responseCode == HttpStatusCode.OK;
                    }
                    else
                    {
                        serverReady = await IsPortListeningAsync(HttpPort);
                    }
                }
                catch
                {
                    // ignore
                }

                if (serverReady)
                {
                    break;
                }

                var milisecondsElapsed = (DateTime.Now - startTime).TotalMilliseconds;

                if (milisecondsElapsed < intervalMilliseconds)
                {
                    await Task.Delay((int)(intervalMilliseconds - milisecondsElapsed));
                }
            }

            if (!serverReady)
            {
                throw new Exception("Couldn't verify the application is ready to receive requests.");
            }
        }

        private async Task<bool> IsPortListeningAsync(int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var task = client.ConnectAsync("127.0.0.1", port);
                    if (await Task.WhenAny(task, Task.Delay(1000)) == task)
                    {
                        return client.Connected;
                    }
                }
            }
            catch
            {
                // If there's an exception, the server is not listening on this port
            }

            return false;
        }
    }
}
