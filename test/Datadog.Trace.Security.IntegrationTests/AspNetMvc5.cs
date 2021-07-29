// <copyright file="AspNetMvc5.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetMvc5CallTargetIntegratedWithSecurity : AspNetMvc5
    {
        public AspNetMvc5CallTargetIntegratedWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5CallTargetIntegratedWithoutSecurity : AspNetMvc5
    {
        public AspNetMvc5CallTargetIntegratedWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5CallTargetClassicWithSecurity : AspNetMvc5
    {
        public AspNetMvc5CallTargetClassicWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5CallTargetClassicWithoutSecurity : AspNetMvc5
    {
        public AspNetMvc5CallTargetClassicWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: false)
        {
        }
    }

    public abstract class AspNetMvc5 : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;
        private readonly bool _enableSecurity;
        private readonly string _testName;

        public AspNetMvc5(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity)
            : base(nameof(AspNetMvc5), @"test\test-applications\security\aspnet", output)
        {
            SetCallTargetSettings(true);
            SetSecurity(enableSecurity);
            _iisFixture = iisFixture;
            _enableSecurity = enableSecurity;
            _iisFixture.TryStartIis(this, classicMode);
            _testName = nameof(AspNetMvc5)
                     + (classicMode ? ".Classic" : ".Integrated")
                     + (RuntimeInformation.ProcessArchitecture == Architecture.X64 ? ".X64" : ".X86"); // assume that arm is the same
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [Fact]
        public async Task TestSecurity()
        {
            var path = "/Home?arg=[$slice]";
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"http://localhost:{_iisFixture.HttpPort}{path}");
            Output.WriteLine($"http://localhost:{_iisFixture.HttpPort}{path}");
            var responseText = await response.Content.ReadAsStringAsync();
            Output.WriteLine($"[http] {response.StatusCode} {responseText}");
            var expectedStatusCode = _enableSecurity ? HttpStatusCode.Forbidden : HttpStatusCode.OK;
            Assert.Equal(expectedStatusCode, response.StatusCode);
        }
    }
}
#endif
