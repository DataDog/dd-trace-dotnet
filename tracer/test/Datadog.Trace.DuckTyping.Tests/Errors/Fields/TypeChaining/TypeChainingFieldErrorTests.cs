// <copyright file="TypeChainingFieldErrorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.Valid;
using Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.WrongChainedReturnType;
using Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.WrongFieldName;
using Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.WrongReturnType;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining
{
    public class TypeChainingFieldErrorTests
    {
        public static IEnumerable<object> SourceObjects() => new[] { ObscureObject.GetFieldPublicObject(), ObscureObject.GetFieldInternalObject(), ObscureObject.GetFieldPrivateObject(), };

        public static IEnumerable<object[]> Valid() =>
            from source in SourceObjects()
            from type in typeof(IValid).GetNestedTypes()
                                       .Concat(typeof(ValidAbstractClass).GetNestedTypes())
                                       .Concat(typeof(ValidVirtualClass).GetNestedTypes())
                                       .Concat(typeof(ValidStruct).GetNestedTypes())
            select new[] { type, source };

        public static IEnumerable<object[]> WrongFieldNames() =>
            from source in SourceObjects()
            from type in typeof(IWrongFieldName).GetNestedTypes()
                                                .Concat(typeof(WrongFieldNameAbstractClass).GetNestedTypes())
                                                .Concat(typeof(WrongFieldNameVirtualClass).GetNestedTypes())
                                                .Concat(typeof(WrongFieldNameStruct).GetNestedTypes())
            select new[] { type, source };

        public static IEnumerable<object[]> WrongReturnTypes() =>
            from source in SourceObjects()
            from type in typeof(IWrongReturnType).GetNestedTypes()
                                                 .Concat(typeof(WrongReturnTypeAbstractClass).GetNestedTypes())
                                                 .Concat(typeof(WrongReturnTypeVirtualClass).GetNestedTypes())
                                                 .Concat(typeof(WrongReturnTypeStruct).GetNestedTypes())
            select new[] { type, source };

        public static IEnumerable<object[]> WrongChainedReturnTypes() =>
            from source in SourceObjects()
            from type in typeof(WrongReturnTypeAbstractClass).GetNestedTypes()
                                                             .Concat(typeof(WrongReturnTypeVirtualClass).GetNestedTypes())
                                                             .Concat(typeof(WrongReturnTypeStruct).GetNestedTypes())
            select new[] { type, source };

        public static IEnumerable<object[]> WrongChainedReturnTypesForInterfaces() =>
            from source in SourceObjects()
            from type in typeof(IWrongChainedReturnType).GetNestedTypes()
            select new[] { type, source };

        [Theory]
        [MemberData(nameof(Valid))]
        public void ValidCanCast(Type duckType, object obscureObject)
        {
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeTrue();

            var valid = obscureObject.DuckCast(duckType);
            valid.Should().NotBeNull();
            valid.Should().BeAssignableTo(duckType);
        }

        [Theory]
        [MemberData(nameof(WrongFieldNames))]
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
        [MemberData(nameof(WrongChainedReturnTypes))]
        public void WrongChainedReturnTypesThrow(Type duckType, object obscureObject)
        {
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeFalse();
            Action cast = () => obscureObject.DuckCast(duckType);
            cast.Should().Throw<TargetInvocationException>();
        }

        [Theory(Skip = "We can't currently detect incorrect return types in these cases")]
        [MemberData(nameof(WrongChainedReturnTypesForInterfaces))]
        public void WrongChainedReturnTypesForInterfacesThrow(Type duckType, object obscureObject)
        {
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeFalse();
            Action cast = () => obscureObject.DuckCast(duckType);
            cast.Should().Throw<TargetInvocationException>();
        }
    }
}
