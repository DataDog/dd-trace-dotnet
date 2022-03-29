// <copyright file="TypeChainingPropertyErrorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.Valid;
using Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.WrongChainedReturnType;
using Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.WrongPropertyName;
using Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.WrongReturnType;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining
{
    public class TypeChainingPropertyErrorTests
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
                                       .Concat(typeof(ValidStruct).GetNestedTypes())
            select new[] { type, source };

        public static IEnumerable<object[]> WrongPropertyNames() =>
            from source in SourceObjects()
            from type in typeof(IWrongPropertyName).GetNestedTypes()
                                                   .Concat(typeof(WrongPropertyNameAbstractClass).GetNestedTypes())
                                                   .Concat(typeof(WrongPropertyNameVirtualClass).GetNestedTypes())
                                                   .Concat(typeof(WrongPropertyNameStruct).GetNestedTypes())
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
            from type in typeof(IWrongChainedReturnType).GetNestedTypes()
                                                        .Concat(typeof(WrongReturnTypeAbstractClass).GetNestedTypes())
                                                        .Concat(typeof(WrongReturnTypeVirtualClass).GetNestedTypes())
                                                        .Concat(typeof(WrongReturnTypeStruct).GetNestedTypes())
            select new[] { type, source };

        [Theory]
        [MemberData(nameof(Valid))]
        public void ValidCanCast(Type duckType, string obscureObjectName)
        {
            var obscureObject = ObscureObject.GetObject(obscureObjectName);
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeTrue();

            var valid = obscureObject.DuckCast(duckType);
            valid.Should().NotBeNull();
            valid.Should().BeAssignableTo(duckType);
        }

        [Theory]
        [MemberData(nameof(WrongPropertyNames))]
        public void WrongNamesThrow(Type duckType, string obscureObjectName)
        {
            var obscureObject = ObscureObject.GetObject(obscureObjectName);
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeFalse();
            Action cast = () => obscureObject.DuckCast(duckType);
            cast.Should().Throw<TargetInvocationException>();
        }

        [Theory(Skip = "We can't currently detect incorrect return types for properties")]
        [MemberData(nameof(WrongReturnTypes))]
        public void WrongReturnTypesThrow(Type duckType, string obscureObjectName)
        {
            var obscureObject = ObscureObject.GetObject(obscureObjectName);
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeFalse();
            Action cast = () => obscureObject.DuckCast(duckType);
            cast.Should().Throw<TargetInvocationException>();
        }

        [Theory(Skip = "We can't currently detect incorrect return types for properties")]
        [MemberData(nameof(WrongChainedReturnTypes))]
        public void WrongChainedReturnTypesThrow(Type duckType, string obscureObjectName)
        {
            var obscureObject = ObscureObject.GetObject(obscureObjectName);
            using var scope = new AssertionScope();
            obscureObject.DuckIs(duckType).Should().BeFalse();
            Action cast = () => obscureObject.DuckCast(duckType);
            cast.Should().Throw<TargetInvocationException>();
        }
    }
}
