// <copyright file="HashHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class HashHelperTests
{
    [Theory]
    [InlineData("service-1", "env-1", "d:1")]
    [InlineData("service-1", "env-1", null)]
    [InlineData("service-1", "env-1", "d:1", "edge-1")]
    [InlineData("service-1", "env-1", "d:1", "edge-1", "edge-2")]
    public void NodeHashSanityCheck(string service, string env, string primaryTag, params string[] edgeArgs)
    {
        // naive implementation (similar to e.g. go/java)
        var sb = new StringBuilder()
                .Append(service)
                .Append(env);
        if (!string.IsNullOrEmpty(primaryTag))
        {
            sb.Append(primaryTag);
        }

        var sortedArgs = new List<string>(edgeArgs);
        sortedArgs.Sort(StringComparer.Ordinal);

        foreach (var sortedArg in sortedArgs)
        {
            sb.Append(sortedArg);
        }

        var expectedHash = FnvHash64.GenerateHash(sb.ToString(), FnvHash64.Version.V1);
        var baseHash = HashHelper.CalculateNodeHashBase(service, env, primaryTag);
        var actual = HashHelper.CalculateNodeHash(baseHash, sortedArgs);

        actual.Value.Should().Be(expectedHash);
    }

    [Fact]
    public void PathwayHashSanityCheck()
    {
        // naive implementation (similar to e.g. go/java)
        var random = new Random();
        var bytes = new byte[16];
        random.NextBytes(bytes);

        var nodeHash = BitConverter.ToUInt64(bytes, startIndex: 0);
        var parentHash = BitConverter.ToUInt64(bytes, startIndex: 8);

        var expectedHash = FnvHash64.GenerateHash(bytes, FnvHash64.Version.V1);
        var actual = HashHelper.CalculatePathwayHash(new NodeHash(nodeHash), new PathwayHash(parentHash));

        actual.Value.Should().Be(expectedHash);
    }
}
