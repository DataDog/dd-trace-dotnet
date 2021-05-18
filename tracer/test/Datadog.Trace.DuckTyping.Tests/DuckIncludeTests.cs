// <copyright file="DuckIncludeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1201 // Elements must appear in the correct order

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests
{
    public class DuckIncludeTests
    {
        [Fact]
        public void ShouldOverrideToString()
        {
            var instance = new SomeClassWithDuckInclude();

            var proxy = instance.DuckCast<IInterface>();

            proxy.ToString().Should().Be(instance.ToString());
        }

        [Fact]
        public void ShouldNotOverrideToString()
        {
            var instance = new SomeClassWithoutDuckInclude();

            var proxy = instance.DuckCast<IInterface>();

            proxy.ToString().Should().NotBe(instance.ToString());
        }

        public class SomeClassWithDuckInclude
        {
            [DuckInclude]
            public override string ToString()
            {
                return "OK";
            }
        }

        public class SomeClassWithoutDuckInclude
        {
            public override string ToString()
            {
                return "OK";
            }
        }

        public interface IInterface
        {
        }
    }
}
