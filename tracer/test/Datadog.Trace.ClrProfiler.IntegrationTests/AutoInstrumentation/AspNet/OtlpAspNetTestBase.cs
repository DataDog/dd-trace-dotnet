// <copyright file="OtlpAspNetTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    /// <summary>
    /// The <see cref="OtlpServerTestBase"/> harness, hosting the ASP.NET sample in IIS
    /// Express. Shared by <see cref="OtlpAspNetMvc5Tests"/> and <see cref="OtlpAspNetWebApi2Tests"/>,
    /// which exercise the same site through the MVC and the Web API pipelines; everything that isn't
    /// specific to that hosting model lives in the base class.
    /// </summary>
    public abstract class OtlpAspNetTestBase : OtlpServerTestBase, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        protected OtlpAspNetTestBase(IisFixture iisFixture, ITestOutputHelper output, string testName, bool openTelemetrySemanticsEnabled)
            : base("AspNetMvc5", iisFixture, @"test\test-applications\aspnet", output, testName, openTelemetrySemanticsEnabled)
        {
            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
        }

        /// <inheritdoc />
        protected override string WarmupPath => "/home/index";

        /// <inheritdoc />
        protected override Task StartServerAsync()
            => _iisFixture.TryStartIis(this, IisAppType.AspNetIntegrated, sendHealthCheck: false);

        /// <inheritdoc />
        protected override string GetRequestUrl(string path)
            => $"http://localhost:{_iisFixture.HttpPort}{_iisFixture.VirtualApplicationPath}{path}";
    }
}
#endif
