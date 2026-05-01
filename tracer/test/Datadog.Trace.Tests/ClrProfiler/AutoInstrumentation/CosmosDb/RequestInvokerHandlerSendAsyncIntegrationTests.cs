// <copyright file="RequestInvokerHandlerSendAsyncIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.CosmosDb
{
    public class RequestInvokerHandlerSendAsyncIntegrationTests
    {
        [Theory]
        [InlineData("dbs/myDb", "dbs/myDb")]
        [InlineData("dbs/myDb/colls/myColl/docs", "dbs/myDb/colls/myColl/docs")]
        [InlineData("dbs/myDb/colls/myColl/docs/item1", "dbs/myDb/colls/myColl/docs/?")]
        [InlineData("dbs/myDb/clientencryptionkeys/key1", "dbs/myDb/clientencryptionkeys/?")]
        [InlineData("dbs/myDb/users/user1/permissions/perm1", "dbs/myDb/users/?/permissions/?")]
        public void NormalizeResourceUri_RedactsIdsKeepingDbAndContainerNames(string input, string expected)
        {
            var result = RequestInvokerHandlerSendAsyncIntegration.NormalizeResourceUri(input);

            result.Should().Be(expected);
        }
    }
}
