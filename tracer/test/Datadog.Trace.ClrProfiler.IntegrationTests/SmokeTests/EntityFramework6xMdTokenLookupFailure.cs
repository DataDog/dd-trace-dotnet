// <copyright file="EntityFramework6xMdTokenLookupFailure.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET452
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.SmokeTests
{
    public class EntityFramework6xMdTokenLookupFailure : SmokeTestBase
    {
        public EntityFramework6xMdTokenLookupFailure(ITestOutputHelper output)
            : base(output, "EntityFramework6x.MdTokenLookupFailure", maxTestRunSeconds: 480)
        {
        }

        [Fact]
        [Trait("Category", "Smoke")]
        public void NoExceptions()
        {
            CheckForSmoke(shouldDeserializeTraces: false);
        }
    }
}
#endif
