// <copyright file="AspNetFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetFixture : MockAgentTestFixture, IDisposable
    {
        public AspNetFixture(AspNetBase test, MockTracerAgent agent, int httpPort, Process sample, string shutdownPath)
            : base(test, agent, httpPort)
        {
            Sample = sample;
            ShutdownPath = shutdownPath;
        }

        public string ShutdownPath { get; }

        public Process Sample { get; }

        public string SampleProcessName
        {
            get { return Sample?.ProcessName; }
        }

        public int? SampleProcessId
        {
            get { return Sample?.Id; }
        }

        public override void Dispose()
        {
            if (!string.IsNullOrEmpty(ShutdownPath))
            {
                var request = WebRequest.CreateHttp($"http://localhost:{HttpPort}{ShutdownPath}");
                request.GetResponse().Close();
            }

            if (Sample is not null)
            {
                try
                {
                    if (!Sample.HasExited)
                    {
                        if (!Sample.WaitForExit(5000))
                        {
                            Sample.Kill();
                        }
                    }
                }
                catch
                {
                }

                Sample.Dispose();
            }

            base.Dispose();
        }
    }
}
