// <copyright file="HotChocolateSecurityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class HotChocolateSecurityTests : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        private static readonly string[] _garphQlQueries =
        {
            @"{""query"":""query Book{book{title author{name}}}""}",
            @"{""query"":""query Book{book(name: \""<script>test\""){title author{name}}}""}",
            @"{""query"":""query Book{book{title author{name}}} query Book{book(name: \""<script>test\""){title author{name}}}""}",
            @"{""query"":""query Book{book(badArray: [\""hello world\"", true, 5, \""<script>test\""]){title author{name}}}""}",
            @"{""query"":""query Book{book(badObject: { title: \""<script>test\"" }){title author{name}}}""}",
            @"{""query"":""query Book{testAlias: book(name: \""<script>test\""){title author{name}}}""}",
            @"{""query"":""query Book($name: String!){book(name: $name){title author{name}}}"", ""variables"": { ""name"": ""<script>test""}}",
        };

        private readonly AspNetCoreTestFixture _fixture;

        public HotChocolateSecurityTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("HotChocolate", outputHelper, "/shutdown", samplesDir: "test/test-applications/integrations", changeDefaults: true)
        {
            _fixture = fixture;
            _fixture.SetOutput(outputHelper);
        }

        public static IEnumerable<object[]> TestData =>
            from packageVersionArray in
                EnvironmentTools.IsWindows()
                    ? new[] { new object[] { string.Empty } }
                    : PackageVersions.HotChocolate
            from query in _garphQlQueries
            select new[] { packageVersionArray[0], query };

        public override void Dispose()
        {
            base.Dispose();
            _fixture.SetOutput(null);
        }

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("RunOnWindows", "True")]
        public async Task TestQuerySecurity(string packageVersion, string query)
        {
            await _fixture.TryStartApp(this, enableSecurity: true, externalRulesFile: DefaultRuleFile, packageVersion: packageVersion);
            SetHttpPort(_fixture.HttpPort);

            var settings = VerifyHelper.GetSpanVerifierSettings(query);

            // On netcoreapp3.1 and net5.0 the response content type is application/json
            // So we change it to application/graphql-response+json to match the newer version
            settings.AddSimpleScrubber("http.response.headers.content-type: application/json; charset=utf-8", "http.response.headers.content-type: application/graphql-response+json; charset=utf-8");

            var spans = await SendRequestsAsync(_fixture.Agent, "/graphql", query, 1, 1, string.Empty);
            await VerifySpansNoMethodNameSettings(spans, settings)
                 .UseFileName($"{GetTestName()}.__query={VerifyHelper.SanitisePathsForVerifyWithDash(query)}")
                 .DisableRequireUniquePrefix(); // all package versions should be the same
        }
    }
}
#endif
