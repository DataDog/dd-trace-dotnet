// <copyright file="IisTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Diagnostics;
using System.IO;
using System.Net;
using NUnit.Framework;

namespace Datadog.Trace.TestHelpers
{
    [NonParallelizable]
    public class IisTestsBase : GacTestsBase
    {
        private (Process Process, string ConfigFile) _iisExpress;

        public IisTestsBase(string sampleAppName, string samplePathOverrides, IisAppType appType, string shutdownPath = null)
            : base(sampleAppName, samplePathOverrides)
        {
            AppType = appType;
            ShutdownPath = shutdownPath;
        }

        public IisTestsBase(string sampleAppName, IisAppType appType, string shutdownPath = null)
            : base(sampleAppName)
        {
            AppType = appType;
            ShutdownPath = shutdownPath;
        }

        public MockTracerAgent Agent { get; private set; }

        public int HttpPort { get; private set; }

        public string ShutdownPath { get; }

        public IisAppType AppType { get; }

        [OneTimeSetUp]
        public void TryStartIis()
        {
            lock (this)
            {
                if (_iisExpress.Process == null)
                {
                    AddAssembliesToGac();

                    var initialAgentPort = TcpPortProvider.GetOpenPort();
                    Agent = new MockTracerAgent(initialAgentPort);

                    HttpPort = TcpPortProvider.GetOpenPort();
                    _iisExpress = StartIISExpress(Agent.Port, HttpPort, AppType);
                }
            }
        }

        [OneTimeTearDown]
        public void Shutdown()
        {
            if (ShutdownPath != null)
            {
                var request = WebRequest.CreateHttp($"http://localhost:{HttpPort}{ShutdownPath}");
                request.GetResponse().Close();
            }

            Agent?.Dispose();

            lock (this)
            {
                if (_iisExpress.Process != null)
                {
                    try
                    {
                        if (!_iisExpress.Process.HasExited)
                        {
                            // sending "Q" to standard input does not work because
                            // iisexpress is scanning console key press, so just kill it.
                            // maybe try this in the future:
                            // https://github.com/roryprimrose/Headless/blob/master/Headless.IntegrationTests/IisExpress.cs
                            _iisExpress.Process.Kill();
                            _iisExpress.Process.WaitForExit(8000);
                        }
                    }
                    catch
                    {
                        // in some circumstances the HasExited property throws, this means the process probably hasn't even started correctly
                    }

                    _iisExpress.Process.Dispose();

                    try
                    {
                        File.Delete(_iisExpress.ConfigFile);
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
