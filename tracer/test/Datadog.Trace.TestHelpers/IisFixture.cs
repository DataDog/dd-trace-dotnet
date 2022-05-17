// <copyright file="IisFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace Datadog.Trace.TestHelpers
{
    public sealed class IisFixture : GacFixture, IDisposable
    {
        public (Process Process, string ConfigFile) IisExpress { get; private set; }

        public MockTracerAgent Agent { get; private set; }

        public int HttpPort { get; private set; }

        public string ShutdownPath { get; set; }

        public string VirtualApplicationPath { get; set; } = string.Empty;

        public void TryStartIis(TestHelper helper, IisAppType appType)
        {
            lock (this)
            {
                if (IisExpress.Process == null)
                {
                    AddAssembliesToGac();

                    var initialAgentPort = TcpPortProvider.GetOpenPort();
                    Agent = MockTracerAgent.Create(initialAgentPort);

                    HttpPort = TcpPortProvider.GetOpenPort();
                    IisExpress = helper.StartIISExpress(Agent, HttpPort, appType, VirtualApplicationPath);
                }
            }
        }

        public void Dispose()
        {
            if (IisExpress.Process != null && ShutdownPath != null)
            {
                var request = WebRequest.CreateHttp($"http://localhost:{HttpPort}{ShutdownPath}");
                request.GetResponse().Close();
            }

            Agent?.Dispose();

            lock (this)
            {
                if (IisExpress.Process != null)
                {
                    try
                    {
                        if (!IisExpress.Process.HasExited)
                        {
                            // sending "Q" to standard input does not work because
                            // iisexpress is scanning console key press, so just kill it.
                            // maybe try this in the future:
                            // https://github.com/roryprimrose/Headless/blob/master/Headless.IntegrationTests/IisExpress.cs
                            IisExpress.Process.Kill();
                            IisExpress.Process.WaitForExit(8000);
                        }
                    }
                    catch
                    {
                        // in some circumstances the HasExited property throws, this means the process probably hasn't even started correctly
                    }

                    IisExpress.Process.Dispose();

                    try
                    {
                        File.Delete(IisExpress.ConfigFile);
                    }
                    catch
                    {
                    }

                    // If the operation fails, it could leave files in the GAC and impact the next tests
                    // Therefore, we don't wrap this in a try/catch
                    RemoveAssembliesFromGac();
                }
            }
        }
    }
}
