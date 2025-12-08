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
        public (ProcessHelper Process, string ConfigFile) IisExpress { get; private set; }

        public MockTracerAgent Agent { get; private set; }

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
                Agent = MockTracerAgent.Create(null, initialAgentPort);

                HttpPort = TcpPortProvider.GetOpenPort();
                IisExpress = await helper.StartIISExpress(Agent, HttpPort, appType, VirtualApplicationPath, UsePartialTrust, UseLegacyCasModel);

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
                    request.GetResponse().Close();
                }
                catch
                {
                    // Ignore
                }
            }

            Agent?.Dispose();

            if (IisExpress.Process != null)
            {
                try
                {
                    // sending "Q" to standard input does not work because
                    // iisexpress is scanning console key press, so just kill it.
                    // maybe try this in the future:
                    // https://github.com/roryprimrose/Headless/blob/master/Headless.IntegrationTests/IisExpress.cs
                    IisExpress.Process.Dispose(8000);
                }
                catch
                {
                    // in some circumstances the HasExited property throws, this means the process probably hasn't even started correctly
                }

                // The ProcessHelper doesn't dispose the process automatically, so we need to do it manually
                IisExpress.Process.Process.Dispose();

                try
                {
                    File.Delete(IisExpress.ConfigFile);
                }
                catch
                {
                }

                // If the operation fails, it could leave files in the GAC and impact the next tests
                // Therefore, we don't wrap this in a try/catch
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

            // if we end up retrying, accept spans from previous attempts
            var dateTime = DateTime.UtcNow;

            // wait for server to be ready to receive requests
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

                        if (responseCode == HttpStatusCode.OK)
                        {
                            await Agent.WaitForSpansAsync(1, minDateTime: dateTime);
                            serverReady = true;
                        }
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
