// <copyright file="NamedPipeTestcs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.SmokeTests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.WindowsOnly
{
    [Trait("Category", "WindowsOnly")]
    public class NamedPipeTestcs
    {
        private readonly ITestOutputHelper _output;

        public NamedPipeTestcs(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void CheckProfilesSentThroughNamedPipe(string appName, string framework, string appAssembly)
        {
             new SmokeTestRunner(appName, framework, appAssembly, commandLine: "--scenario 1", _output, TransportType.NamedPipe).RunAndCheck();
        }
    }
}
