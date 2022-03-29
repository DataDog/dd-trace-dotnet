// <copyright file="NonGenericMethodErrorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.Valid;
using Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongArgumentModifier;
using Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongArgumentType;
using Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongMethodName;
using Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongNumberOfArguments;
using Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics.ProxiesDefinitions.WrongReturnType;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.NonGenerics
{
    public class NonGenericMethodErrorTests
    {
        public static IEnumerable<object> SourceObjects() => new object[]
        {
            nameof(ObscureObject.GetPropertyPublicObject),
            nameof(ObscureObject.GetPropertyInternalObject),
            nameof(ObscureObject.GetPropertyPrivateObject),
        };

        public static IEnumerable<object[]> Valid() =>
            from source in SourceObjects()
            from type in typeof(IValid).GetNestedTypes()
                                       .Concat(typeof(ValidAbstractClass).GetNestedTypes())
                                       .Concat(typeof(ValidVirtualClass).GetNestedTypes())
            select new[] { type, source };

        public static IEnumerable<object[]> WrongMethodNames() =>
            from source in SourceObjects()
            from type in typeof(IWrongMethodName).GetNestedTypes()
                                                 .Concat(typeof(WrongMethodNameAbstractClass).GetNestedTypes())
                                                 .Concat(typeof(WrongMethodNameVirtualClass).GetNestedTypes())
            select new[] { type, source };

        public static IEnumerable<object[]> WrongReturnTypes() =>
            from source in SourceObjects()
            from type in typeof(IWrongReturnType).GetNestedTypes()
                                                 .Concat(typeof(WrongReturnTypeAbstractClass).GetNestedTypes())
                                                 .Concat(typeof(WrongReturnTypeVirtualClass).GetNestedTypes())
            where !type.Name.Contains("Bypass") // TODO: this doesn't currently work for the bypass types due to type chaining issues
            select new[] { type, source };

        public static IEnumerable<object[]> WrongArgumentTypes() =>
            from source in SourceObjects()
            from type in typeof(IWrongArgumentType).GetNestedTypes()
                                                   .Concat(typeof(WrongArgumentTypeAbstractClass).GetNestedTypes())
                                                   .Concat(typeof(WrongArgumentTypeVirtualClass).GetNestedTypes())
            select new[] { type, source };

        public static IEnumerable<object[]> WrongNumberOfArguments() =>
            from source in SourceObjects()
            from type in typeof(IWrongNumberOfArguments).GetNestedTypes()
                                                        .Concat(typeof(WrongNumberOfArgumentsAbstractClass).GetNestedTypes())
                                                        .Concat(typeof(WrongNumberOfArgumentsVirtualClass).GetNestedTypes())
            select new[] { type, source };

        public static IEnumerable<object[]> WrongArgumentModifiers() =>
            from source in SourceObjects()
            from type in typeof(IWrongArgumentModifier).GetNestedTypes()
                                                       .Concat(typeof(WrongArgumentModifierAbstractClass).GetNestedTypes())
                                                       .Concat(typeof(WrongArgumentModifierVirtualClass).GetNestedTypes())
            select new[] { type, source };

        [Theory]
        [MemberData(nameof(Valid))]
        public void ValidCanCast(Type duckType, string obscureObjectName)
        {
            var obscureObject = ObscureObject.GetObject(obscureObjectName);
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeTrue();
            var valid = obscureObject.DuckCast(duckType);
        }

        [Theory]
        [MemberData(nameof(WrongMethodNames))]
        public void WrongNamesThrow(Type duckType, string obscureObjectName)
        {
            var obscureObject = ObscureObject.GetObject(obscureObjectName);
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeFalse();
            Action cast = () => obscureObject.DuckCast(duckType);
            cast.Should().Throw<TargetInvocationException>();
        }

        [Theory]
        [MemberData(nameof(WrongReturnTypes))]
        public void WrongReturnTypesThrow(Type duckType, string obscureObjectName)
        {
            var obscureObject = ObscureObject.GetObject(obscureObjectName);
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeFalse();
            Action cast = () => obscureObject.DuckCast(duckType);
            cast.Should().Throw<TargetInvocationException>();
        }

        [Theory]
        [MemberData(nameof(WrongArgumentTypes))]
        public void WrongArgumentTypesThrow(Type duckType, string obscureObjectName)
        {
            var obscureObject = ObscureObject.GetObject(obscureObjectName);
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeFalse();
            Action cast = () => obscureObject.DuckCast(duckType);
            cast.Should().Throw<TargetInvocationException>();
        }

        [Theory]
        [MemberData(nameof(WrongNumberOfArguments))]
        public void WrongNumberOfArgumentsThrow(Type duckType, string obscureObjectName)
        {
            var obscureObject = ObscureObject.GetObject(obscureObjectName);
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeFalse();
            Action cast = () => obscureObject.DuckCast(duckType);
            cast.Should().Throw<TargetInvocationException>();
        }

        [Theory]
        [MemberData(nameof(WrongArgumentModifiers))]
        public void WrongArgumentModifierThrow(Type duckType, string obscureObjectName)
        {
            var obscureObject = ObscureObject.GetObject(obscureObjectName);
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeFalse();
            Action cast = () => obscureObject.DuckCast(duckType);
            cast.Should().Throw<TargetInvocationException>();
        }
    }
}
