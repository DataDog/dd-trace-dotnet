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
        public static IEnumerable<object> PublicObjects() => new[]
        {
            ObscureObject.GetPropertyPublicObject(),
        };

        public static IEnumerable<object> PrivateObjects() => new[]
        {
            ObscureObject.GetPropertyInternalObject(),
            ObscureObject.GetPropertyPrivateObject(),
        };

        public static IEnumerable<object> SourceObjects() => PublicObjects().Concat(PrivateObjects());

        public static IEnumerable<object[]> Valid() =>
            from source in PublicObjects()
                          .Select(obj => new { obj, isPublic = true })
                          .Concat(PrivateObjects().Select(obj => new { obj, isPublic = false }))
            from type in typeof(IValid).GetNestedTypes()
                                       .Concat(typeof(ValidAbstractClass).GetNestedTypes())
                                       .Concat(typeof(ValidVirtualClass).GetNestedTypes())
            select new[] { type, source.obj, source.isPublic };

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
            select new[] { type, source };

        public static IEnumerable<object[]> WrongNumberOfArguments() =>
            from source in SourceObjects()
            from type in typeof(IWrongNumberOfArguments).GetNestedTypes()
                                                        .Concat(typeof(WrongNumberOfArgumentsAbstractClass).GetNestedTypes())
                                                        .Concat(typeof(WrongNumberOfArgumentsVirtualClass).GetNestedTypes())
            select new[] { type, source };

        [Theory]
        [MemberData(nameof(Valid))]
#pragma warning disable xUnit1026 // isPublic is used, just not in all frameworks
        public void ValidCanCastUnlessNet45AndPrivate(Type duckType, object obscureObject, bool isPublic)
#pragma warning restore xUnit1026
        {
            using var scope = new AssertionScope();
#if NET452
            if (!isPublic && duckType.Methods().Any(x => x.IsGenericMethod))
            {
                obscureObject.DuckIs(duckType).Should().BeFalse();
                Action cast = () => obscureObject.DuckCast(duckType);
                cast.Should().Throw<TargetInvocationException>();
                return;
            }
#endif
            obscureObject.DuckIs(duckType).Should().BeTrue();
            var valid = obscureObject.DuckCast(duckType);
        }

        [Theory]
        [MemberData(nameof(WrongMethodNames))]
        public void WrongNamesThrow(Type duckType, object obscureObject)
        {
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeFalse();
            Action cast = () => obscureObject.DuckCast(duckType);
            cast.Should().Throw<TargetInvocationException>();
        }

        [Theory]
        [MemberData(nameof(WrongReturnTypes))]
        public void WrongReturnTypesThrow(Type duckType, object obscureObject)
        {
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeFalse();
            Action cast = () => obscureObject.DuckCast(duckType);
            cast.Should().Throw<TargetInvocationException>();
        }

        [Theory]
        [MemberData(nameof(WrongArgumentTypes))]
        public void WrongArgumentTypesThrow(Type duckType, object obscureObject)
        {
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeFalse();
            Action cast = () => obscureObject.DuckCast(duckType);
            cast.Should().Throw<TargetInvocationException>();
        }

        [Theory]
        [MemberData(nameof(WrongNumberOfArguments))]
        public void WrongNumberOfArgumentsThrow(Type duckType, object obscureObject)
        {
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeFalse();
            Action cast = () => obscureObject.DuckCast(duckType);
            cast.Should().Throw<TargetInvocationException>();
        }
    }
}
