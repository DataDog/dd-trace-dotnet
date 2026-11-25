// <copyright file="MemberInfoExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class MemberInfoExtensionsTests
{
    [Fact]
    public void HasCustomAttribute_AppliedAttribute_ReturnsTrue()
    {
        typeof(BaseType).HasCustomAttribute<MarkerAttribute>().Should().BeTrue();
        typeof(BaseType).GetProperty(nameof(BaseType.Value))!.HasCustomAttribute<MarkerAttribute>().Should().BeTrue();
    }

    [Fact]
    public void HasCustomAttribute_MissingAttribute_ReturnsFalse()
    {
        typeof(UnmarkedType).HasCustomAttribute<MarkerAttribute>().Should().BeFalse();
    }

    [Fact]
    public void HasCustomAttribute_InheritedAttribute_MatchesGetCustomAttribute()
    {
        var derivedType = typeof(DerivedType);
        var overriddenProperty = derivedType.GetProperty(nameof(DerivedType.Value))!;

        derivedType.HasCustomAttribute<MarkerAttribute>().Should().Be(derivedType.GetCustomAttribute<MarkerAttribute>() is not null).And.BeTrue();
        overriddenProperty.HasCustomAttribute<MarkerAttribute>().Should().Be(overriddenProperty.GetCustomAttribute<MarkerAttribute>() is not null).And.BeTrue();
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = true)]
    private sealed class MarkerAttribute : Attribute
    {
    }

    [Marker]
    private class BaseType
    {
        [Marker]
        public virtual int Value => 0;
    }

    private sealed class DerivedType : BaseType
    {
        public override int Value => 1;
    }

    private sealed class UnmarkedType
    {
    }
}
