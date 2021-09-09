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
            : base(iisFixture, output, classicMode: false, enableSecurity: true, blockingEnabled: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5CallTargetIntegratedWithSecurityWithoutBlocking : AspNetMvc5
    {
        public AspNetMvc5CallTargetIntegratedWithSecurityWithoutBlocking(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: true, blockingEnabled: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5CallTargetIntegratedWithoutSecurity : AspNetMvc5
    {
        public AspNetMvc5CallTargetIntegratedWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableSecurity: false, blockingEnabled: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5CallTargetClassicWithSecurity : AspNetMvc5
    {
        public AspNetMvc5CallTargetClassicWithSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true, blockingEnabled: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5CallTargetClassicWithSecurityWithoutBlocking : AspNetMvc5
    {
        public AspNetMvc5CallTargetClassicWithSecurityWithoutBlocking(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: true, blockingEnabled: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5CallTargetClassicWithoutSecurity : AspNetMvc5
    {
        public AspNetMvc5CallTargetClassicWithoutSecurity(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableSecurity: false, blockingEnabled: false)
        {
        }
    }

    public abstract class AspNetMvc5 : AspNetBase, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;
        private readonly bool _enableSecurity;
        private readonly bool _blockingEnabled;
        private readonly string _testName;

        public AspNetMvc5(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableSecurity, bool blockingEnabled)
            : base(nameof(AspNetMvc5), output, "/home/shutdown", @"test\test-applications\security\aspnet")
        {
            SetCallTargetSettings(true);
            SetSecurity(enableSecurity);
            SetAppSecBlockingEnabled(blockingEnabled);
            _iisFixture = iisFixture;
            _enableSecurity = enableSecurity;
            _blockingEnabled = blockingEnabled;
            _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            _testName = nameof(AspNetMvc5)
                     + (classicMode ? ".Classic" : ".Integrated")
                     + (RuntimeInformation.ProcessArchitecture == Architecture.X64 ? ".X64" : ".X86"); // assume that arm is the same
            SetHttpPort(iisFixture.HttpPort);
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [Fact]
        public Task TestSecurity()
        {
            // if blocking is enabled, request stops before reaching asp net mvc integrations intercepting before action methods, so no more spans are generated
            // NOTE: by integrating the latest version of the WAF, blocking was disabled, as it does not support blocking yet
            return TestBlockedRequestAsync(_iisFixture.Agent, _enableSecurity, _enableSecurity && _blockingEnabled ? HttpStatusCode.OK : HttpStatusCode.OK, _enableSecurity && _blockingEnabled ? 10 : 10, new Action<TestHelpers.MockTracerAgent.Span>[]
             {
             s => Assert.Matches("aspnet(-mvc)?.request", s.Name),
             s => Assert.Equal("sample", s.Service),
             s => Assert.Equal("web", s.Type)
             });
        }
    }
}
#endif
