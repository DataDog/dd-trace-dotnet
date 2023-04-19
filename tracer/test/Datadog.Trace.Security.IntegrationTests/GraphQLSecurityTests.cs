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
            => await Test(query);
    }

    public class GraphQL4SecurityTests : GraphQLSecurityTestsBase
    {
        public GraphQL4SecurityTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, "GraphQL4")
        {
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
            => await Test(query);
    }

    public class GraphQL3SecurityTests : GraphQLSecurityTestsBase
    {
        public GraphQL3SecurityTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, "GraphQL3")
        {
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
            => await Test(query);
    }

    public class GraphQL2SecurityTests : GraphQLSecurityTestsBase
    {
        public GraphQL2SecurityTests(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, "GraphQL")
        {
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
            => await Test(query);
    }

    public class GraphQLSecurityTestsBase : AspNetBase, IClassFixture<AspNetCoreTestFixture>
    {
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

        protected async Task Test(string query)
        {
            await _fixture.TryStartApp(this, enableSecurity: true, externalRulesFile: DefaultRuleFile);
            SetHttpPort(_fixture.HttpPort);

            var settings = VerifyHelper.GetSpanVerifierSettings(query);

            await TestAppSecRequestWithVerifyAsync(_fixture.Agent, "/graphql", query, 1, 1, settings);
        }
    }
}
#endif
