// <copyright file="PrivateGenericTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests;

public class PrivateGenericTests
{
    public interface IDuckType
    {
        IDuckTypeInner Method { get; }
    }

    public interface IDuckTypeInner
    {
        string Value { get; }
    }

    [Fact]
    public void CanDuckTypeInstanceReferencingPrivateTypesFromOtherAssembliesInDeepGeneric()
    {
        // Setting up a scenario similar to this:
        // PublicTypeFromAssembly1<PublicTypeFromAssembly2<PrivateTypeFromAssembly3>>>
        // Where each type is defined in a different assembly, and the inner most one is a type that should not be accessible

        var instance = new TargetObject<IEnumerable<Span>>();
        var duckType = instance.DuckCast<IDuckType>();
        duckType.Method.Should().NotBeNull();
        duckType.Method.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CanDuckTypeInstanceReferencingPrivateTypesFromOtherAssembliesInDeepGenericReallyDeep()
    {
        // Setting up a scenario similar to this:
        // PublicTypeFromAssembly1<PublicTypeFromAssembly2<PrivateTypeFromAssembly3>>>
        // Where each type is defined in a different assembly, and the inner most one is a type that should not be accessible

        var instance = new TargetObject<IEnumerable<System.Tuple<BoundedConcurrentQueue<MockHttpParser>, Span>>>();
        var duckType = instance.DuckCast<IDuckType>();
        duckType.Method.Should().NotBeNull();
        duckType.Method.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CanDuckTypeInstanceReferencingPrivateTypesFromOtherAssembliesInDeepGenericWithNestedTypes()
    {
        // Setting up a scenario similar to this:
        // PublicTypeFromAssembly1<PublicTypeFromAssembly2<PrivateTypeFromAssembly3>>>
        // Where each type is defined in a different assembly, and the inner most one is a type that should not be accessible

        var instance = new TargetObject<IEnumerable<System.Tuple<BoundedConcurrentQueue<MockHttpParser.StreamReaderHelper>, Span>>>();
        var duckType = instance.DuckCast<IDuckType>();
        duckType.Method.Should().NotBeNull();
        duckType.Method.Value.Should().NotBeNullOrWhiteSpace();
    }

    public class TargetObject<T>
    {
        private PrivateType<T> Method { get; } = new();

        private class PrivateType<TInner>
        {
            public string Value => typeof(TInner).ToString();
        }
    }
}
