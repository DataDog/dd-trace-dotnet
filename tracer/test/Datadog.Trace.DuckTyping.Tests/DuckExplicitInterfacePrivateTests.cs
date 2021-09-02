// <copyright file="DuckExplicitInterfacePrivateTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1201 // Elements must appear in the correct order

using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests
{
    public class DuckExplicitInterfacePrivateTests
    {
        [Fact]
        public void PrivateNormalTest()
        {
            var targetObject = new PrivateTargetObject();
            var proxy = targetObject.DuckCast<IProxyDefinition>();

            proxy.SayHi().Should().Be("Hello World");
            proxy.SayHiWithWildcard().Should().Be("Hello World (*)");
        }

        [Fact]
        public void PrivateGenericTest()
        {
            var targetObject = new PrivateTargetGenericObject();
            var proxy = targetObject.DuckCast<IPrivateGenericProxyDefinition>();

            proxy.Sum(1, 1).Should().Be(2);
            proxy.Sum(1.0f, 1.0f).Should().Be(2.0f);
        }

        [Fact]
        public void PrivateNormalGenericInstanceTest()
        {
            var targetObject = new PrivateTargetObject<object>();
            var proxy = targetObject.DuckCast<IProxyDefinition>();

            proxy.SayHi().Should().Be("Hello World");
            proxy.SayHiWithWildcard().Should().Be("Hello World (*)");
        }

        [Fact]
        public void PrivateGenericWithGenericInstanceTest()
        {
            var targetObject = new PrivateTargetGenericObject<object>();
            var proxy = targetObject.DuckCast<IPrivateGenericProxyDefinition>();

            proxy.Sum(1, 1).Should().Be(2);
            proxy.Sum(1.0f, 1.0f).Should().Be(2.0f);
        }

        [Fact]
        public void PrivateNormalGenericPrivateInstanceTest()
        {
            var targetObject = new PrivateTargetObject<PrivateObject>();
            var proxy = targetObject.DuckCast<IProxyDefinition>();

            proxy.SayHi().Should().Be("Hello World");
            proxy.SayHiWithWildcard().Should().Be("Hello World (*)");
        }

        [Fact]
        public void PrivateGenericWithGenericPrivateInstanceTest()
        {
            var targetObject = new PrivateTargetGenericObject<PrivateObject>();
            var proxy = targetObject.DuckCast<IPrivateGenericProxyDefinition>();

            proxy.Sum(1, 1).Should().Be(2);
            proxy.Sum(1.0f, 1.0f).Should().Be(2.0f);
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
            [Duck(ExplicitInterfaceTypeName = "Datadog.Trace.DuckTyping.Tests.DuckExplicitInterfacePrivateTests+ITarget")]
            string SayHi();

            [Duck(ExplicitInterfaceTypeName = "*")]
            string SayHiWithWildcard();
        }

        public interface IPrivateGenericProxyDefinition
        {
            [Duck(ExplicitInterfaceTypeName = "*", GenericParameterTypeNames = new string[] { "System.Int32" })]
            int Sum(int a, int b);

            [Duck(ExplicitInterfaceTypeName = "*", GenericParameterTypeNames = new string[] { "System.Single" })]
            float Sum(float a, float b);
        }

        private class PrivateTargetObject : ITarget
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

        private class PrivateTargetObject<TInstance> : ITarget<TInstance>
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

        private class PrivateTargetGenericObject : IGenericTarget
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

        private class PrivateTargetGenericObject<TInstance> : IGenericTarget<TInstance>
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

        private class PrivateObject
        {
        }
    }
}
