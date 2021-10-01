// <copyright file="LoaderOptimizationStartup.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.IIS
{
    public class LoaderOptimizationStartup
    {
        private const string Url = "http://localhost:8080";

        [Test]
        [Property("RunOnWindows", "True")]
        [Property("IIS", "True")]
        public async Task ApplicationDoesNotReturnErrors()
        {
            var intervalMilliseconds = 500;
            var intervals = 5;
            var serverReady = false;
            var client = new HttpClient();

            // wait for server to be ready to receive requests
            while (intervals-- > 0)
            {
                try
                {
                    var serverReadyResponse = await client.GetAsync(Url);
                    serverReady = serverReadyResponse.StatusCode == HttpStatusCode.OK;
                }
                catch
                {
                    // ignore
                }

                if (serverReady)
                {
                    Console.WriteLine("The server is ready.");
                    break;
                }

                Thread.Sleep(intervalMilliseconds);
            }

            // Server is ready to receive requests
            var responseMessage = await client.GetAsync(Url);
            Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);
        }
    }
}
#endif
