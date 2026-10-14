// <copyright file="NGenInlinerCollisionSmokeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    /// <summary>
    /// The app's module name hashes like mscorlib's, so looking up the NGen inliners of its instrumented methods
    /// also decodes NGen inline records of mscorlib methods. mscorlib is domain-neutral, and decoding it needs a
    /// current AppDomain, which the native ReJIT worker doesn't have.
    /// Expected behavior: the app runs to completion and its methods are instrumented.
    /// Wrong behavior: the app crashes with an access violation.
    /// </summary>
    public class NGenInlinerCollisionSmokeTest : SmokeTestBase
    {
        public NGenInlinerCollisionSmokeTest(ITestOutputHelper output)
            : base(output, "NGenInlinerCollision")
        {
            SetEnvironmentVariable("DD_TRACE_METHODS", "NGenInlinerCollision.Targets[*]");
        }

        [SkippableFact]
        [Trait("Category", "Smoke")]
        public async Task NoCrash()
        {
            await CheckForSmoke();

            // The lookup that crashed ran before the ReJIT of these methods, so a span shows the app got past it
            Spans.Should().Contain(span => span.Resource == "Targets.M0001");
        }
    }
}
#endif
