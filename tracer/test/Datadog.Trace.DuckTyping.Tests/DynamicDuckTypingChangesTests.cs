// <copyright file="DynamicDuckTypingChangesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1201 // Elements must appear in the correct order

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests
{
    /// <summary>
    /// The intentional changes of dynamic duck typing listed in docs/development/DuckTyping.NativeAOT.md ("Changes to Dynamic Duck
    /// Typing") that the other tests don't cover.
    /// </summary>
    public class DynamicDuckTypingChangesTests
    {
        [Fact]
        public void ClassProxyWithAStaticConstructorShouldCallItsInstanceConstructor()
        {
            var calls = StaticConstructorProxy.Calls;

            var proxy = DuckType.Create<StaticConstructorProxy>(new NameTarget())!;

            proxy.Name.Should().Be("name");
            (StaticConstructorProxy.Calls - calls).Should().Be(1);
        }

        [Fact]
        public void OmittedNativeIntegerParameterShouldReceiveItsDefaultValue()
        {
            DuckType.Create<IEchoProxy>(new NativeIntegerDefaultTarget())!.Echo(1).Should().Be(6);
        }

        [Fact]
        public void OmittedUnknownConstantParameterShouldReceiveAWrapperOfNull()
        {
            DuckType.Create<IEchoProxy>(new UnknownConstantDefaultTarget())!.Echo(1).Should().Be(11);
        }

        [Fact]
        public void MethodNamesShouldBeTrimmedButNotPropertyNamesWithoutFallbacks()
        {
            DuckType.Create<ITrimmedMethodNameProxy>(new NameTarget())!.Echo(7).Should().Be(7);

            Action createWithUntrimmedPropertyName = () => DuckType.Create<ITrimmedPropertyNameProxy>(new NameTarget());
            createWithUntrimmedPropertyName.Should().Throw<DuckTypePropertyOrFieldNotFoundException>();
        }

        public interface IEchoProxy
        {
            int Echo(int value);
        }

        public interface ITrimmedMethodNameProxy
        {
            [Duck(Name = " Echo ")]
            int Echo(int value);
        }

        public interface ITrimmedPropertyNameProxy
        {
            [Duck(Name = " Name ")]
            string Name { get; }
        }

        public class NameTarget
        {
            public string Name => "name";

            public int Echo(int value) => value;
        }

        public abstract class StaticConstructorProxy
        {
            private static int _staticCalls;

            static StaticConstructorProxy()
            {
                _staticCalls++;
            }

            protected StaticConstructorProxy()
            {
                Calls++;
            }

            public static int Calls { get; private set; }

            public abstract string Name { get; }

            public override string ToString() => _staticCalls.ToString();
        }

        public class NativeIntegerDefaultTarget
        {
            public int Echo(int value, nint offset = 5) => value + (int)offset;
        }

        public class UnknownConstantDefaultTarget
        {
#pragma warning disable CS0618 // UnknownWrapper is obsolete on .NET 6+, but it's what the C# compiler passes for this parameter.
            public int Echo(int value, [Optional, IUnknownConstant] object wrapper) => value + (wrapper is UnknownWrapper ? 10 : 0);
#pragma warning restore CS0618
        }
    }
}
