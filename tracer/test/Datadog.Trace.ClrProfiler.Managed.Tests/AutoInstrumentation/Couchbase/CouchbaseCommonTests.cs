// <copyright file="CouchbaseCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase;
using Datadog.Trace.DuckTyping;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Couchbase;

public class CouchbaseCommonTests
{
    [Fact]
    public void GetNormalizedSeedNodesFromClientConfiguration_ExtractsHostAndPortsFromServers()
    {
        List<Uri> servers = new() { new Uri("http://localhost:8091/pools"), new Uri("http://127.0.0.1/pools") };
        var testObject = new TestClientConfiguration { Servers = servers };
        var proxyObject = testObject.DuckAs<IClientConfiguration>();

        CouchbaseCommon.GetNormalizedSeedNodesFromClientConfiguration(proxyObject).Should().Be("localhost:8091,127.0.0.1:80");

        // Run it again for good measure
        CouchbaseCommon.GetNormalizedSeedNodesFromClientConfiguration(proxyObject).Should().Be("localhost:8091,127.0.0.1:80");
    }

    [Fact]
    public void GetNormalizedSeedNodesFromConnectionString_ExtractsHostAndPortsFromServers()
    {
        var testObject = new TestConnectionString();
        testObject.Hosts.Add(new TestHostEndpoint("localhost", 8091));
        testObject.Hosts.Add(new TestHostEndpoint("127.0.0.1", null));
        var proxyObject = testObject.DuckAs<IConnectionString>();

        CouchbaseCommon.GetNormalizedSeedNodesFromConnectionString(proxyObject).Should().Be("localhost:8091,127.0.0.1");

        // Run it again for good measure
        CouchbaseCommon.GetNormalizedSeedNodesFromConnectionString(proxyObject).Should().Be("localhost:8091,127.0.0.1");
    }

    public readonly struct TestHostEndpoint
    {
        public TestHostEndpoint(string host, int? port)
        {
            Host = host;
            Port = port;
        }

        public string Host { get; }

        public int? Port { get; }

        public override string ToString() => Port != null ? $"{Host}:{Port}" : Host;
    }

    public class TestConnectionString
    {
        public IList<TestHostEndpoint> Hosts { get; } = new List<TestHostEndpoint>();
    }

    public class TestClientConfiguration
    {
        public List<Uri> Servers { get; set; }
    }
}
