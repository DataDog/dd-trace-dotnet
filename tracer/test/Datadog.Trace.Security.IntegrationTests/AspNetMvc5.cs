// <copyright file="AspNetMvc5.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetMvc5CallTargetIntegratedWithSecurity : AspNetMvc5
    {
        public AspNetMvc5CallTargetIntegratedWithSecurity()
            : base(classicMode: false, enableSecurity: true, blockingEnabled: true)
        {
        }
    }

    public class AspNetMvc5CallTargetIntegratedWithSecurityWithoutBlocking : AspNetMvc5
    {
        public AspNetMvc5CallTargetIntegratedWithSecurityWithoutBlocking()
            : base(classicMode: false, enableSecurity: true, blockingEnabled: false)
        {
        }
    }

    public class AspNetMvc5CallTargetIntegratedWithoutSecurity : AspNetMvc5
    {
        public AspNetMvc5CallTargetIntegratedWithoutSecurity()
            : base(classicMode: false, enableSecurity: false, blockingEnabled: false)
        {
        }
    }

    public class AspNetMvc5CallTargetClassicWithSecurity : AspNetMvc5
    {
        public AspNetMvc5CallTargetClassicWithSecurity()
            : base(classicMode: true, enableSecurity: true, blockingEnabled: true)
        {
        }
    }

    public class AspNetMvc5CallTargetClassicWithSecurityWithoutBlocking : AspNetMvc5
    {
        public AspNetMvc5CallTargetClassicWithSecurityWithoutBlocking()
            : base(classicMode: true, enableSecurity: true, blockingEnabled: false)
        {
        }
    }

    public class AspNetMvc5CallTargetClassicWithoutSecurity : AspNetMvc5
    {
        public AspNetMvc5CallTargetClassicWithoutSecurity()
            : base(classicMode: true, enableSecurity: false, blockingEnabled: false)
        {
        }
    }

    [NonParallelizable]
    public abstract class AspNetMvc5 : AspNetBase
    {
        private readonly IisTestsBase _iisFixture;
        private readonly bool _enableSecurity;
        private readonly bool _blockingEnabled;

        public AspNetMvc5(bool classicMode, bool enableSecurity, bool blockingEnabled)
            : base(nameof(AspNetMvc5), "/home/shutdown", @"test\test-applications\security\aspnet")
        {
            SetCallTargetSettings(true);
            SetSecurity(enableSecurity);
            SetAppSecBlockingEnabled(blockingEnabled);
            _iisFixture = new IisTestsBase(nameof(AspNetMvc5), classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            _enableSecurity = enableSecurity;
            _blockingEnabled = blockingEnabled;
        }

        [OneTimeSetUp]
        public void Setup()
        {
            _iisFixture.TryStartIis();
            SetHttpPort(_iisFixture.HttpPort);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _iisFixture.Shutdown();
        }

        [Property("Category", "EndToEnd")]
        [Property("RunOnWindows", "True")]
        [Property("LoadFromGAC", "True")]
        [Test]
        public Task TestSecurity()
        {
            // if blocking is enabled, request stops before reaching asp net mvc integrations intercepting before action methods, so no more spans are generated
            // NOTE: by integrating the latest version of the WAF, blocking was disabled, as it does not support blocking yet
            return TestBlockedRequestAsync(_iisFixture.Agent, _enableSecurity, _enableSecurity && _blockingEnabled ? HttpStatusCode.OK : HttpStatusCode.OK, _enableSecurity && _blockingEnabled ? 10 : 10, new Action<TestHelpers.MockTracerAgent.Span>[]
             {
             s => StringAssert.IsMatch("aspnet(-mvc)?.request", s.Name),
             s => Assert.AreEqual("sample", s.Service),
             s => Assert.AreEqual("web", s.Type)
             });
        }
    }
}
#endif
