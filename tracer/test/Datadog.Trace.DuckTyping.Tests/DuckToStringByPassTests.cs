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

        [Fact]
        public void ProxyWithSealedToStringCanBeCreated()
        {
            var instance = new TargetWithDefaultToString();

            var proxy = instance.DuckCast<ProxyWithSealedToString>();

            proxy.GetValue().Should().Be(42);
            proxy.ToString().Should().Be("ToString from the proxy base.");
            ((IDuckType)proxy).ToString().Should().Be(instance.ToString());
        }

        [Fact]
        public void ToStringIgnoresHiddenMethodWithNonStringReturnType()
        {
            var instance = new TargetWithHiddenNonStringToString();

            var proxy = instance.DuckCast<IEmptyProxy>();

            ((IDuckType)proxy).ToString().Should().Be("ToString from the base target.");
        }

        [Fact]
        public void ToStringIgnoresHiddenStaticMethod()
        {
            var instance = new TargetWithHiddenStaticToString();

            var proxy = instance.DuckCast<IEmptyProxy>();

            ((IDuckType)proxy).ToString().Should().Be("ToString from the base target.");
        }

        [Fact]
        public void ToStringUsesHiddenInstanceMethodWithStringReturnType()
        {
            var instance = new TargetWithHiddenStringToString();

            var proxy = instance.DuckCast<IEmptyProxy>();

            ((IDuckType)proxy).ToString().Should().Be("ToString from the hidden target method.");
        }

        [Theory]
        [InlineData(42, "42")]
        [InlineData(TargetEnum.Value, "Value")]
        public void ToStringFromValueTypeTarget(object instance, string expected)
        {
            var proxy = instance.DuckCast<IEmptyProxy>();

            proxy.ToString().Should().Be(expected);
        }

        [Fact]
        public void ToStringIgnoresOpenGenericMethod()
        {
            var instance = new TargetWithGenericToString();

            var proxy = instance.DuckCast<IEmptyProxy>();

            ((IDuckType)proxy).ToString().Should().Be(instance.ToString());
        }

        [Fact]
        public void ToStringUsesNonGenericMethodWhenGenericOverloadExists()
        {
            var instance = new TargetWithGenericAndNonGenericToString();

            var proxy = instance.DuckCast<IEmptyProxy>();

            ((IDuckType)proxy).ToString().Should().Be("ToString from the non-generic target method.");
        }

        [Fact]
        public void ToStringIgnoresByRefReturnType()
        {
            var instance = new TargetWithByRefToString();

            var proxy = instance.DuckCast<IEmptyProxy>();

            ((IDuckType)proxy).ToString().Should().Be("ToString from the base target.");
        }

        public class TargetClass
        {
            public override string ToString()
            {
                return "ToString from Target instance.";
            }
        }

        public class TargetWithDefaultToString
        {
            public int GetValue() => 42;
        }

        public class TargetBaseClass
        {
            public override string ToString() => "ToString from the base target.";
        }

        public class TargetWithHiddenNonStringToString : TargetBaseClass
        {
            public new virtual int ToString() => 0;
        }

        public class TargetWithHiddenStaticToString : TargetBaseClass
        {
            public static new string ToString() => "ToString from the hidden static target method.";
        }

        public class TargetWithHiddenStringToString : TargetBaseClass
        {
            public new string ToString() => "ToString from the hidden target method.";
        }

        public class TargetWithGenericToString
        {
            public string ToString<T>() => typeof(T).Name;
        }

        public class TargetWithGenericAndNonGenericToString
        {
            public new string ToString() => "ToString from the non-generic target method.";

            public string ToString<T>() => typeof(T).Name;
        }

        public class TargetWithByRefToString : TargetBaseClass
        {
            private string _value = "ToString from the by-ref target method.";

            public new ref string ToString() => ref _value;
        }

        public enum TargetEnum
        {
            Value,
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

        public abstract class ProxyWithSealedToString
        {
            public abstract int GetValue();

            public sealed override string ToString() => "ToString from the proxy base.";
        }
    }
}
