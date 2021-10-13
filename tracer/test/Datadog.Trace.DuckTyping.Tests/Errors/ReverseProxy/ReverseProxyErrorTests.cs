// <copyright file="ReverseProxyErrorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.Valid;
using Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.WrongArgumentType;
using Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.WrongMethodName;
using Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.WrongNumberOfArguments;
using Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.WrongReturnType;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy
{
    public class ReverseProxyErrorTests
    {
        public static IEnumerable<object[]> Valid() =>
            typeof(ValidInterfaceImplementations)
               .GetNestedTypes()
               .Concat(typeof(ValidAbstractClassImplementations).GetNestedTypes())
               .Concat(typeof(ValidVirtualClassImplementations).GetNestedTypes())
               .Select(type => new object[] { type });

        public static IEnumerable<object[]> WrongMethodNames() =>
            typeof(WrongMethodNameInterfaceImplementations)
               .GetNestedTypes()
               .Concat(typeof(WrongMethodNameAbstractClassImplementations).GetNestedTypes())
               .Concat(typeof(WrongMethodNameVirtualClassImplementations).GetNestedTypes())
               .Select(type => new object[] { type });

        public static IEnumerable<object[]> WrongReturnTypes() =>
            typeof(WrongReturnTypeInterfaceImplementations)
               .GetNestedTypes()
               .Concat(typeof(WrongReturnTypeAbstractClassImplementations).GetNestedTypes())
               .Concat(typeof(WrongReturnTypeVirtualClassImplementations).GetNestedTypes())
               .Select(type => new object[] { type });

        public static IEnumerable<object[]> WrongNumberOfArguments() =>
            typeof(WrongNumberOfArgumentsInterfaceImplementations)
               .GetNestedTypes()
               .Concat(typeof(WrongNumberOfArgumentsAbstractClassImplementations).GetNestedTypes())
               .Concat(typeof(WrongNumberOfArgumentsVirtualClassImplementations).GetNestedTypes())
               .Select(type => new object[] { type });

        public static IEnumerable<object[]> WrongArgumentTypes() =>
            typeof(WrongArgumentTypeInterfaceImplementations)
               .GetNestedTypes()
               .Concat(typeof(WrongArgumentTypeAbstractClassImplementations).GetNestedTypes())
               .Concat(typeof(WrongArgumentTypeVirtualClassImplementations).GetNestedTypes())
               .Where(type => !type.Name.Contains("DuckChain")) // We can't detect wrong argument types in explicit DuckReverse methods
               .Select(type => new object[] { type });

        [Theory]
        [MemberData(nameof(Valid))]
        public void ValidCanReverseDuckType(Type reversedType)
        {
            var typeToImplement = GetTypeToImplement(reversedType);

            var instance = Activator.CreateInstance(reversedType);
            using var scope = new AssertionScope();

#if NET452
            Action cast = () =>  instance.DuckImplement(typeToImplement);
            cast.Should().Throw<DuckTypeTypeIsNotPublicException>();
#else
            var proxy = instance.DuckImplement(typeToImplement);
            Assert.NotNull(proxy);
            proxy.GetType().Should().BeAssignableTo(typeToImplement);
#endif
        }

        [Theory(Skip = "These fail, because they fail on net45, and we want consistency between frameworks")]
        [MemberData(nameof(Valid))]
        public void ValidCanTryReverseDuckType(Type reversedType)
        {
            var typeToImplement = GetTypeToImplement(reversedType);

            var instance = Activator.CreateInstance(reversedType);
            using var scope = new AssertionScope();

            var canCast = instance.TryDuckImplement(typeToImplement, out var proxy);
            Assert.True(canCast);
            Assert.NotNull(proxy);
            proxy.GetType().Should().BeAssignableTo(typeToImplement);
        }

        [Theory]
        [MemberData(nameof(WrongMethodNames))]
        public void WrongNamesThrow(Type reversedType)
        {
            var typeToImplement = GetTypeToImplement(reversedType);

            var instance = Activator.CreateInstance(reversedType);
            using var scope = new AssertionScope();

            Action cast = () =>  instance.DuckImplement(typeToImplement);
#if NET452
            cast.Should().Throw<DuckTypeTypeIsNotPublicException>();
#else
            cast.Should().Throw<TargetInvocationException>();
#endif
        }

        [Theory]
        [MemberData(nameof(WrongReturnTypes))]
        public void WrongReturnTypesThrow(Type reversedType)
        {
            var typeToImplement = GetTypeToImplement(reversedType);

            var instance = Activator.CreateInstance(reversedType);
            using var scope = new AssertionScope();

            Action cast = () =>  instance.DuckImplement(typeToImplement);
#if NET452
            cast.Should().Throw<DuckTypeTypeIsNotPublicException>();
#else
            cast.Should().Throw<TargetInvocationException>();
#endif
        }

        [Theory]
        [MemberData(nameof(WrongArgumentTypes))]
        public void WrongArgumentTypesThrow(Type reversedType)
        {
            var typeToImplement = GetTypeToImplement(reversedType);

            var instance = Activator.CreateInstance(reversedType);
            using var scope = new AssertionScope();

            Action cast = () =>  instance.DuckImplement(typeToImplement);
#if NET452
            cast.Should().Throw<DuckTypeTypeIsNotPublicException>();
#else
            cast.Should().Throw<TargetInvocationException>();
#endif
        }

        [Theory]
        [MemberData(nameof(WrongNumberOfArguments))]
        public void WrongNumberOfArgumentsThrow(Type reversedType)
        {
            var typeToImplement = GetTypeToImplement(reversedType);

            var instance = Activator.CreateInstance(reversedType);
            using var scope = new AssertionScope();

            Action cast = () =>  instance.DuckImplement(typeToImplement);
#if NET452
            cast.Should().Throw<DuckTypeTypeIsNotPublicException>();
#else
            cast.Should().Throw<TargetInvocationException>();
#endif
        }

        private static Type GetTypeToImplement(Type reversedType)
        {
            var typeToImplement = reversedType
                                 .GetCustomAttribute<ReverseTypeToTestAttribute>()
                                ?.TypeToTest;
            if (typeToImplement is null)
            {
                throw new ArgumentException($"Could not find referenced type for {reversedType}");
            }

            return typeToImplement;
        }
    }
}
