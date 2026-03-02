// <copyright file="DuckTypeAotMultiTargetContractTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Linq;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class DuckTypeAotMultiTargetContractTests
{
    [Fact]
    public void AttributeDiscoveryShouldSupportForwardReverseAndCopyMultiTargetMappings()
    {
        var assemblyPath = typeof(DuckTypeAotMultiTargetContractTests).Assembly.Location;

        var result = DuckTypeAotAttributeDiscovery.Discover(new[] { assemblyPath });

        result.Errors.Should().BeEmpty();

        var forwardMappings = result.Mappings.Where(m => m.ProxyTypeName.EndsWith("+TestContracts+IMultiForwardProxyContract", StringComparison.Ordinal)).ToList();
        var reverseMappings = result.Mappings.Where(m => m.ProxyTypeName.EndsWith("+TestContracts+IMultiReverseProxyContract", StringComparison.Ordinal)).ToList();
        var copyMappings = result.Mappings.Where(m => m.ProxyTypeName.EndsWith("+TestContracts+MultiCopyProxyContract", StringComparison.Ordinal)).ToList();

        forwardMappings.Should().HaveCount(2);
        forwardMappings.All(m => m.Mode == DuckTypeAotMappingMode.Forward).Should().BeTrue();
        forwardMappings.Select(m => m.TargetTypeName).Should().Contain(new[] { typeof(TestContracts.MultiForwardProxyTargetA).FullName!, typeof(TestContracts.MultiForwardProxyTargetB).FullName! });

        reverseMappings.Should().HaveCount(2);
        reverseMappings.All(m => m.Mode == DuckTypeAotMappingMode.Reverse).Should().BeTrue();
        reverseMappings.Select(m => m.TargetTypeName).Should().Contain(new[] { typeof(TestContracts.MultiReverseProxyTargetA).FullName!, typeof(TestContracts.MultiReverseProxyTargetB).FullName! });

        copyMappings.Should().HaveCount(2);
        copyMappings.All(m => m.Mode == DuckTypeAotMappingMode.Forward).Should().BeTrue();
        copyMappings.Select(m => m.TargetTypeName).Should().Contain(new[] { typeof(TestContracts.MultiCopyProxyTargetA).FullName!, typeof(TestContracts.MultiCopyProxyTargetB).FullName! });
    }

    private static class TestContracts
    {
        [DuckType("Datadog.Trace.Tools.Runner.Tests.DuckTypeAotMultiTargetContractTests+TestContracts+MultiForwardProxyTargetA", "Datadog.Trace.Tools.Runner.Tests")]
        [DuckType("Datadog.Trace.Tools.Runner.Tests.DuckTypeAotMultiTargetContractTests+TestContracts+MultiForwardProxyTargetB", "Datadog.Trace.Tools.Runner.Tests")]
        internal interface IMultiForwardProxyContract
        {
            int Value { get; }
        }

        [DuckReverse("Datadog.Trace.Tools.Runner.Tests.DuckTypeAotMultiTargetContractTests+TestContracts+MultiReverseProxyTargetA", "Datadog.Trace.Tools.Runner.Tests")]
        [DuckReverse("Datadog.Trace.Tools.Runner.Tests.DuckTypeAotMultiTargetContractTests+TestContracts+MultiReverseProxyTargetB", "Datadog.Trace.Tools.Runner.Tests")]
        internal interface IMultiReverseProxyContract
        {
            [DuckReverseMethod]
            int Sum(int value);
        }

        [DuckCopy("Datadog.Trace.Tools.Runner.Tests.DuckTypeAotMultiTargetContractTests+TestContracts+MultiCopyProxyTargetA", "Datadog.Trace.Tools.Runner.Tests")]
        [DuckCopy("Datadog.Trace.Tools.Runner.Tests.DuckTypeAotMultiTargetContractTests+TestContracts+MultiCopyProxyTargetB", "Datadog.Trace.Tools.Runner.Tests")]
        internal struct MultiCopyProxyContract
        {
            [DuckField]
            public int Value { get; set; }
        }

        internal class MultiForwardProxyTargetA
        {
            public int Value { get; } = 1;
        }

        internal class MultiForwardProxyTargetB
        {
            public int Value { get; } = 2;
        }

        internal class MultiReverseProxyTargetA
        {
            public int Sum(int value) => value + 1;
        }

        internal class MultiReverseProxyTargetB
        {
            public int Sum(int value) => value + 2;
        }

        internal class MultiCopyProxyTargetA
        {
            public int Value { get; } = 10;
        }

        internal class MultiCopyProxyTargetB
        {
            public int Value { get; } = 20;
        }
    }
}
