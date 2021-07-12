// <copyright file="DuckExplicitInterfaceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1201 // Elements must appear in the correct order

using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests
{
    public class DuckExplicitInterfaceTests
    {
        [Fact]
        public void NormalTest()
        {
            var targetObject = new TargetObject();
            var proxy = targetObject.DuckCast<IProxyDefinition>();

            proxy.SayHi().Should().Be("Hello World");
            proxy.SayHiWithWildcard().Should().Be("Hello World (*)");
        }

        [Fact]
        public void GenericTest()
        {
            var targetObject = new TargetGenericObject();
            var proxy = targetObject.DuckCast<IGenericProxyDefinition>();

            proxy.Sum(1, 1).Should().Be(2);
            proxy.Sum(1.0f, 1.0f).Should().Be(2.0f);
        }

        [Fact]
        public void NormalGenericInstanceTest()
        {
            var targetObject = new TargetObject<object>();
            var proxy = targetObject.DuckCast<IProxyDefinition>();

            proxy.SayHi().Should().Be("Hello World");
            proxy.SayHiWithWildcard().Should().Be("Hello World (*)");
        }

        [Fact]
        public void GenericWithGenericInstanceTest()
        {
            var targetObject = new TargetGenericObject<object>();
            var proxy = targetObject.DuckCast<IGenericProxyDefinition>();

            proxy.Sum(1, 1).Should().Be(2);
            proxy.Sum(1.0f, 1.0f).Should().Be(2.0f);
        }

        [Fact]
        public void NormalGenericPrivateInstanceTest()
        {
            var targetObject = new TargetObject<PrivateObject>();
            var proxy = targetObject.DuckCast<IProxyDefinition>();

            proxy.SayHi().Should().Be("Hello World");
            proxy.SayHiWithWildcard().Should().Be("Hello World (*)");
        }

        [Fact]
        public void GenericWithGenericPrivateInstanceTest()
        {
            var targetObject = new TargetGenericObject<PrivateObject>();
            var proxy = targetObject.DuckCast<IGenericProxyDefinition>();

            proxy.Sum(1, 1).Should().Be(2);
            proxy.Sum(1.0f, 1.0f).Should().Be(2.0f);
        }

        public class TargetObject : ITarget
        {
            string ITarget.SayHi()
            {
                return "Hello World";
            }

            string ITarget.SayHiWithWildcard()
            {
                return "Hello World (*)";
            }
        }

        public class TargetGenericObject : IGenericTarget
        {
            T IGenericTarget.Sum<T>(T a, T b)
            {
                if (a is int aInt && b is int bInt)
                {
                    return (T)(object)(aInt + bInt);
                }
                else if (a is float aFloat && b is float bFloat)
                {
                    return (T)(object)(aFloat + bFloat);
                }

                return default;
            }
        }

        public class TargetObject<TInstance> : ITarget<TInstance>
        {
            string ITarget.SayHi()
            {
                return "Hello World";
            }

            string ITarget.SayHiWithWildcard()
            {
                return "Hello World (*)";
            }
        }

        public class TargetGenericObject<TInstance> : IGenericTarget<TInstance>
        {
            T IGenericTarget.Sum<T>(T a, T b)
            {
                if (a is int aInt && b is int bInt)
                {
                    return (T)(object)(aInt + bInt);
                }
                else if (a is float aFloat && b is float bFloat)
                {
                    return (T)(object)(aFloat + bFloat);
                }

                return default;
            }
        }

        public interface ITarget
        {
            string SayHi();

            string SayHiWithWildcard();
        }

        public interface ITarget<out TCategoryName> : ITarget
        {
        }

        public interface IGenericTarget
        {
            T Sum<T>(T a, T b);
        }

        public interface IGenericTarget<out TCategoryName> : IGenericTarget
        {
        }

        public interface IProxyDefinition
        {
            [Duck(ExplicitInterfaceTypeName = "Datadog.Trace.DuckTyping.Tests.DuckExplicitInterfaceTests+ITarget")]
            string SayHi();

            [Duck(ExplicitInterfaceTypeName = "*")]
            string SayHiWithWildcard();
        }

        public interface IGenericProxyDefinition
        {
            [Duck(ExplicitInterfaceTypeName = "*")]
            T Sum<T>(T a, T b);
        }

        private class PrivateObject
        {
        }
    }
}
