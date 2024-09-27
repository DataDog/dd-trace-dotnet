// <copyright file="XUnitRetriesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP3_1_OR_GREATER

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Ci;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI;

[Collection(nameof(TransportTestsCollection))]
public class XUnitRetriesTests : TestingFrameworkRetriesTests
{
    public XUnitRetriesTests(ITestOutputHelper output)
        : base("XUnitTestsRetries", output)
    {
        SetServiceName("xunit-retries");
    }

    protected override string AlwaysFails => "Samples.XUnitTestsRetries.TestSuite.AlwaysFails";

    protected override string AlwaysPasses => "Samples.XUnitTestsRetries.TestSuite.AlwaysPasses";

    protected override string TrueAtLastRetry => "Samples.XUnitTestsRetries.TestSuite.TrueAtLastRetry";

    protected override string TrueAtThirdRetry => "Samples.XUnitTestsRetries.TestSuite.TrueAtThirdRetry";

    [SkippableTheory]
    [MemberData(nameof(PackageVersions.XUnitRetries), MemberType = typeof(PackageVersions))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "TestIntegrations")]
    [Trait("Category", "FlakyRetries")]
    public override Task FlakyRetries(string packageVersion)
    {
        return base.FlakyRetries(packageVersion);
    }
}
#endif
