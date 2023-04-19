// <copyright file="GraphQLSecurityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class GraphQLSecurityTests : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
        private readonly AspNetCoreTestFixture fixture;

        public GraphQLSecurityTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base("GraphQL7", outputHelper, "/shutdown", samplesDir: "test/test-applications/integrations", changeDefaults: true)
        {
            this.fixture = fixture;
            this.fixture.SetOutput(outputHelper);
        }

        public override void Dispose()
        {
            base.Dispose();
            fixture.SetOutput(null);
        }

        [SkippableTheory]
        [InlineData(@"{""query"":""mutation objectNameNormal{createHuman(human: { name: \""Alice\"" }){id name}}""}")]
        [InlineData(@"{""query"":""mutation objectName{createHuman(human: { name: \""<script>test\"" }){id name}}""}")]
        [InlineData(@"{""query"":""mutation objectVarName($humanName:String!){createHuman(human: { name: $humanName }){id name}}"",""variables"":{""humanName"": ""<script>test""}}")]
        [InlineData(@"{""query"":""mutation objectVar($human:HumanInput!){createHuman(human: $human){id name}}"",""variables"":{""human"":{""name"": ""<script>test""}}}")]
        [InlineData(@"{""query"":""mutation createArray{createHumans(names: [\""Alice\"", \""<script>test\""]){name}}""}")]
        [InlineData(@"{""query"":""mutation createArrayVar($humanNames:[String!]!){createHumans(names: $humanNames){name}}"",""variables"":{""humanNames"": [""Alice"", ""<script>test""]}}")]
        [InlineData(@"{""query"":""mutation createVarInArray($bobName:String!){createHumans(names: [\""Alice\"", $bobName]){name}}"",""variables"":{""bobName"": ""<script>test""}}")]
        [Trait("RunOnWindows", "True")]
        public async Task TestQuerySecurity(string query)
        {
            EnableDebugMode();
            await fixture.TryStartApp(this, enableSecurity: true, externalRulesFile: DefaultRuleFile);
            SetHttpPort(fixture.HttpPort);

            var settings = VerifyHelper.GetSpanVerifierSettings(query);

            await TestAppSecRequestWithVerifyAsync(fixture.Agent, "/graphql", query, 1, 1, settings);
        }
    }
}
#endif
