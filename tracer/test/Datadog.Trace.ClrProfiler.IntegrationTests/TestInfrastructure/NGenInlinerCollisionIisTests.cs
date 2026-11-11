// <copyright file="NGenInlinerCollisionIisTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    /// <summary>
    /// The site's assembly name, `Samples.AspNet.NGenNameHash`, hashes to the same value as `System`. ASP.NET loads System domain-neutral, so looking up the NGen
    /// inliners of the site's instrumented methods also decodes NGen inline records of System methods, and decoding
    /// them needs a current AppDomain, which the native ReJIT worker doesn't have.
    /// Expected behavior: every request succeeds, and the site's methods are instrumented.
    /// Wrong behavior: IIS Express crashes with an access violation.
    /// </summary>
    [Collection("IisTests")]
    public class NGenInlinerCollisionIisTests : TestHelper, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;

        public NGenInlinerCollisionIisTests(IisFixture iisFixture, ITestOutputHelper output)
            : base("AspNet.NGenNameHash", @"test\test-applications\aspnet", output)
        {
            SetEnvironmentVariable("DD_TRACE_METHODS", "NGenInlinerCollision.Targets[*]");

            _iisFixture = iisFixture;
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        public async Task NoCrash()
        {
            var url = $"http://localhost:{_iisFixture.HttpPort}/";
            using var client = new HttpClient();

            // The lookup that crashed ran on the ReJIT worker before it requested the ReJIT of the site's methods, and a
            // method is only rewritten on its next call after that request, so keep calling the site until a span shows
            // the rewrite, then once more. IIS Express doesn't restart after a crash, so a crash makes a request fail.
            // The fixture skips its health check, so the first request here also starts the site.
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!_iisFixture.Agent.Spans.Any(span => span.Resource == "Targets.M0001"))
            {
                DateTime.UtcNow.Should().BeBefore(deadline, "a span should show that the site's methods were rewritten");
                (await client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
                await Task.Delay(500);
            }

            (await client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        public Task InitializeAsync() => _iisFixture.TryStartIis(this, IisAppType.AspNetIntegrated, sendHealthCheck: false);

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
#endif
