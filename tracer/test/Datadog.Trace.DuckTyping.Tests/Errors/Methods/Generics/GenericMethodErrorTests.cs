// <copyright file="GenericMethodErrorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.Valid;
using Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.WrongArgumentType;
using Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.WrongMethodName;
using Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.WrongNumberOfArguments;
using Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics.ProxiesDefinitions.WrongReturnType;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Methods.Generics
{
    public class GenericMethodErrorTests
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
            select new[] { type, source };

        public static IEnumerable<object[]> WrongArgumentTypes() =>
            from source in SourceObjects()
            from type in typeof(IWrongArgumentType).GetNestedTypes()
                                                   .Concat(typeof(WrongArgumentTypeAbstractClass).GetNestedTypes())
                                                   .Concat(typeof(WrongArgumentTypeVirtualClass).GetNestedTypes())
            where !type.Name.Contains("ForEachScope") // TODO: This doesn't currently work as we can't detect issues in the type conversion
            select new[] { type, source };

        public static IEnumerable<object[]> WrongNumberOfArguments() =>
            from source in SourceObjects()
            from type in typeof(IWrongNumberOfArguments).GetNestedTypes()
                                                        .Concat(typeof(WrongNumberOfArgumentsAbstractClass).GetNestedTypes())
                                                        .Concat(typeof(WrongNumberOfArgumentsVirtualClass).GetNestedTypes())
            select new[] { type, source };

        [Theory]
        [MemberData(nameof(Valid))]
        public void ValidCanCastUnlessNet45AndPrivate(Type duckType, string obscureObjectName)
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

        [Theory(Skip = "We can't currently correctly detect incorrect return types")]
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
    }
}
