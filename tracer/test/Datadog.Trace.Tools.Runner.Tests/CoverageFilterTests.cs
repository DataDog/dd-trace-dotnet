// <copyright file="CoverageFilterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Coverage.Collector;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class CoverageFilterTests
{
    public static IEnumerable<object[]> ValidModuleFilterData =>
        new List<object[]>
        {
            new object[] { "[Module]*" },
            new object[] { "[Module*]*" },
            new object[] { "[Mod*ule]*" },
            new object[] { "[M*e]*" },
            new object[] { "[Mod*le*]*" },
            new object[] { "[Module?]*" },
            new object[] { "[ModuleX?]*" },
        };

    public static IEnumerable<object[]> ValidModuleAndNamespaceFilterData =>
        new List<object[]>
            {
                new object[] { "[Module]a.b.Dto" },
                new object[] { "[Module]a.b.Dtos?" },
                new object[] { "[Module]a.*" },
                new object[] { "[Module]a*" },
                new object[] { "[Module]*b.*" },
            }
           .Concat(ValidModuleFilterData);

    public static IEnumerable<object[]> FilterExpressionData
    {
        get
        {
            yield return new object[] { "[*]*", true };
            yield return new object[] { "[*]*core", true };
            yield return new object[] { "[assembly]*", true };
            yield return new object[] { "[*]type", true };
            yield return new object[] { "[assembly]type", true };
            yield return new object[] { "[*]", false };
            yield return new object[] { "[-]*", false };
            yield return new object[] { "*", false };
            yield return new object[] { "][", false };
            yield return new object[] { null, false };
        }
    }

    [Theory]
    [MemberData(nameof(FilterExpressionData))]
    public void TestIsValidFilterExpression(string filter, bool result)
    {
        FiltersHelper.IsValidFilterExpression(filter).Should().Be(result);
    }

    [Theory]
    [MemberData(nameof(ValidModuleFilterData))]
    public void TestIsModuleExcludedAndIncludedWithFilter(string filter)
    {
        FiltersHelper.FilteredByAssemblyAndType("Module.dll", null, new[] { filter }).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ValidModuleFilterData))]
    public void TestIsModuleExcludedAndIncludedWithMatchingAndMismatchingFilter(string filter)
    {
        var filters = new[] { "[Mismatch]*", filter, "[Mismatch]*" };
        FiltersHelper.FilteredByAssemblyAndType("Module.dll", null, filters).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ValidModuleAndNamespaceFilterData))]
    public void TestIsTypeExcludedAndIncludedWithFilter(string filter)
    {
        FiltersHelper.FilteredByAssemblyAndType("Module.dll", "a.b.Dto", new[] { filter }).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ValidModuleAndNamespaceFilterData))]
    public void TestIsTypeExcludedAndIncludedWithMatchingAndMismatchingFilter(string filter)
    {
        var filters = new[] { "[Mismatch]*", filter, "[Mismatch]*" };
        FiltersHelper.FilteredByAssemblyAndType("Module.dll", "a.b.Dto", filters).Should().BeTrue();
    }
}
