// <copyright file="DuckIncludeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1201 // Elements must appear in the correct order

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests
{
    public class DuckIncludeTests
    {
        [Fact]
        public void ShouldOverrideGetHashCode()
        {
            var instance = new SomeClassWithDuckInclude();

            var proxy = instance.DuckCast<IInterface>();

            proxy.GetHashCode().Should().Be(instance.GetHashCode());
        }

        [Fact]
        public void ShouldIncludeTheOverrideOfADuckIncludeMethod()
        {
            // The attribute is inherited by the override, which the proxy calls.
            var proxy = DuckType.Create<IDescribeProxy>(new DescribeOverride())!;
            proxy.GetType().GetMethod(nameof(DescribeBase.Describe), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!.Invoke(proxy, null).Should().Be("override");
        }

        [Fact]
        public void ShouldNotIncludeASealedOverrideOfADuckIncludeMethod()
        {
            // Like any final method: the proxy doesn't implement it, nor the base method it overrides.
            var proxy = DuckType.Create<IDescribeProxy>(new DescribeSealedOverride())!;
            proxy.GetType().GetMethod(nameof(DescribeBase.Describe), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Should().BeNull();
        }

        [Fact]
        public void ShouldNotOverrideGetHashCode()
        {
            var instance = new SomeClassWithoutDuckInclude();

            var proxy = instance.DuckCast<IInterface>();

            proxy.GetHashCode().Should().NotBe(instance.GetHashCode());
        }

        public class SomeClassWithDuckInclude
        {
            [DuckInclude]
            public override int GetHashCode()
            {
                return 42;
            }
        }

        public class SomeClassWithoutDuckInclude
        {
            public override int GetHashCode()
            {
                return 42;
            }
        }

        public interface IInterface
        {
        }

        public interface IDescribeProxy
        {
        }

        public class DescribeBase
        {
            [DuckInclude]
            public virtual string Describe() => "base";

            public override string ToString() => Describe();
        }

        public class DescribeOverride : DescribeBase
        {
            public override string Describe() => "override";
        }

        public class DescribeSealedOverride : DescribeBase
        {
            public sealed override string Describe() => "sealed";
        }
    }
}
