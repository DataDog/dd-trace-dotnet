// <copyright file="DuckToStringByPassTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1201 // Elements must appear in the correct order

using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests
{
    public class DuckToStringByPassTests
    {
        [Fact]
        public void ToStringFromEmptyInterfaceProxyTest()
        {
            var instance = new TargetClass();

            var proxy = instance.DuckCast<IEmptyProxy>();

            proxy.ToString().Should().Be(instance.ToString());
        }

        [Fact]
        public void ToStringFromDefinedInterfaceProxyTest()
        {
            var instance = new TargetClass();

            var proxy = instance.DuckCast<IToStringProxy>();

            proxy.ToString().Should().Be(instance.ToString());
        }

        [Fact]
        public void ToStringFromEmptyClassProxyTest()
        {
            var instance = new TargetClass();

            var proxy = instance.DuckCast<EmptyProxyClass>();

            proxy.ToString().Should().Be(instance.ToString());
        }

        [Fact]
        public void ToStringFromDefinedClassProxyTest()
        {
            var instance = new TargetClass();

            var proxy = instance.DuckCast<ToStringProxyClass>();

            proxy.ToString().Should().Be(instance.ToString());
        }

        [Fact]
        public void ToStringFromEmptyAbstractProxyTest()
        {
            var instance = new TargetClass();

            var proxy = instance.DuckCast<EmptyAbstractProxyClass>();

            proxy.ToString().Should().Be(instance.ToString());
        }

        [Fact]
        public void ToStringFromDefinedAbstractProxyTest()
        {
            var instance = new TargetClass();

            var proxy = instance.DuckCast<ToStringAbstractProxyClass>();

            proxy.ToString().Should().Be(instance.ToString());
        }

        public class TargetClass
        {
            public override string ToString()
            {
                return "ToString from Target instance.";
            }
        }

        public interface IEmptyProxy
        {
        }

        public interface IToStringProxy
        {
            string ToString();
        }

        public class EmptyProxyClass
        {
        }

        public class ToStringProxyClass
        {
            public override string ToString() => null;
        }

        public abstract class EmptyAbstractProxyClass
        {
        }

        public abstract class ToStringAbstractProxyClass
        {
            public override string ToString() => null;
        }
    }
}
