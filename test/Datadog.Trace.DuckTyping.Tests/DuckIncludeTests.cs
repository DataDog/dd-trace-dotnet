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
            var instance = new SomeClass();

            var proxy = instance.DuckCast<IInterface>();

            proxy.ToString().Should().Be(instance.ToString());
        }

        public class SomeClass
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
