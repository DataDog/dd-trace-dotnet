// <copyright file="GraphQLSecurityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests;
using Datadog.Trace.TestHelpers;
using VerifyTests;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // file may only contain a single type
#pragma warning disable SA1649 // file name should match first type name

namespace Datadog.Trace.Security.IntegrationTests
{
    public class GraphQL7SecurityTests : GraphQLSecurityTestsBase
    {
        public GraphQL7SecurityTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, "GraphQL7")
        {
        }

        public static IEnumerable<object[]> TestData => GetTestData(PackageVersions.GraphQL7);

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("RunOnWindows", "True")]
        public async Task TestQuerySecurity(string packageVersion, string query)
            => await Test(packageVersion, query);
    }

    public class GraphQL4SecurityTests : GraphQLSecurityTestsBase
    {
        public GraphQL4SecurityTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, "GraphQL4")
        {
        }

        public static IEnumerable<object[]> TestData => GetTestData(PackageVersions.GraphQL4);

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("RunOnWindows", "True")]
        public async Task TestQuerySecurity(string packageVersion, string query)
            => await Test(packageVersion, query);
    }

    public class GraphQL3SecurityTests : GraphQLSecurityTestsBase
    {
        public GraphQL3SecurityTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, "GraphQL3")
        {
        }

        public static IEnumerable<object[]> TestData => GetTestData(PackageVersions.GraphQL3);

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("RunOnWindows", "True")]
        public async Task TestQuerySecurity(string packageVersion, string query)
            => await Test(packageVersion, query);
    }

    public class GraphQL2SecurityTests : GraphQLSecurityTestsBase
    {
        public GraphQL2SecurityTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, "GraphQL")
        {
        }

        public static IEnumerable<object[]> TestData => GetTestData(PackageVersions.GraphQL);

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("RunOnWindows", "True")]
        public async Task TestQuerySecurity(string packageVersion, string query)
            => await Test(packageVersion, query);
    }

    public class GraphQLSecurityTestsBase : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        private static readonly string[] _garphQlQueries =
            {
                @"{""query"":""mutation objectNameNormal{createHuman(human: { name: \""Alice\"" }){id name}}""}",
                @"{""query"":""mutation objectName{createHuman(human: { name: \""<script>test\"" }){id name}}""}",
                @"{""query"":""mutation objectVarName($humanName:String!){createHuman(human: { name: $humanName }){id name}}"",""variables"":{""humanName"": ""<script>test""}}",
                @"{""query"":""mutation objectVar($human:HumanInput!){createHuman(human: $human){id name}}"",""variables"":{""human"":{""name"": ""<script>test""}}}",
                @"{""query"":""mutation createArray{createHumans(names: [\""Alice\"", \""<script>test\""]){name}}""}",
                @"{""query"":""mutation createArrayVar($humanNames:[String!]!){createHumans(names: $humanNames){name}}"",""variables"":{""humanNames"": [""Alice"", ""<script>test""]}}",
                @"{""query"":""mutation createVarInArray($bobName:String!){createHumans(names: [\""Alice\"", $bobName]){name}}"",""variables"":{""bobName"": ""<script>test""}}",
            };

        private readonly AspNetCoreTestFixture _fixture;

        protected GraphQLSecurityTestsBase(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, string sampleName)
            : base(sampleName, outputHelper, "/shutdown", samplesDir: "test/test-applications/integrations", changeDefaults: true)
        {
            _fixture = fixture;
            _fixture.SetOutput(outputHelper);
        }

        public override void Dispose()
        {
            base.Dispose();
            _fixture.SetOutput(null);
        }

        protected static IEnumerable<object[]> GetTestData(IEnumerable<object[]> packageVersions) =>
            from packageVersionArray in GetSafePackageVersion(packageVersions)
            from query in _garphQlQueries
            select new[] { packageVersionArray[0], query };

        protected async Task Test(string packageVersion, string query)
        {
            await _fixture.TryStartApp(this, enableSecurity: true, externalRulesFile: DefaultRuleFile, packageVersion: packageVersion);
            SetHttpPort(_fixture.HttpPort);

            var settings = VerifyHelper.GetSpanVerifierSettings();
            settings.AddSimpleScrubber("\"human\",\"0\"", "\"human\",\"xxxx\"");
            settings.AddSimpleScrubber("\"human\",\"name\"", "\"human\",\"xxxx\"");

            var spans = await SendRequestsAsync(_fixture.Agent, "/graphql", query, 1, 1, string.Empty);
            await VerifySpansNoMethodNameSettings(spans, settings)
                 .UseFileName($"{GetTestName()}.__query={VerifyHelper.SanitisePathsForVerifyWithDash(query)}")
                 .DisableRequireUniquePrefix(); // all package versions should be the same
        }

        private static IEnumerable<object[]> GetSafePackageVersion(IEnumerable<object[]> packageVersions) =>
            EnvironmentTools.IsWindows()
                ? new[] { new object[] { string.Empty } }
                : packageVersions;
    }
}
#endif
