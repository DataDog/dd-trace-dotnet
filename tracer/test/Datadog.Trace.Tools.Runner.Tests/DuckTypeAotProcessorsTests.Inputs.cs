// <copyright file="DuckTypeAotProcessorsTests.Inputs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201, SA1202 // Elements should appear in the correct order

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The inputs of the registry generator: map files, type names, generic instantiations.
/// </summary>
public partial class DuckTypeAotProcessorsTests
{
    private const string BibleMappingCatalogFileName = "ducktype-aot-bible-mapping-catalog.json";
    private const string BibleScenarioInventoryFileName = "ducktype-aot-bible-scenario-inventory.json";
    private const string BibleExpectedOutcomesFileName = "ducktype-aot-bible-expected-outcomes.json";
    private const string BibleKnownLimitationsFileName = "ducktype-aot-bible-known-limitations.json";
    private const string DuplicateAssignableBaseTargetTypeName = "Duplicate.Targets.SharedBaseTarget";
    private const string StrongNameKeyEnvironmentVariable = "DD_TRACE_DUCKTYPE_AOT_STRONG_NAME_KEY_FILE";
#if NETCOREAPP2_1
    private const string TemporaryTargetAssemblyFramework = "netcoreapp2.1";
#elif NETCOREAPP3_0
    private const string TemporaryTargetAssemblyFramework = "netcoreapp3.0";
#elif NETCOREAPP3_1
    private const string TemporaryTargetAssemblyFramework = "netcoreapp3.1";
#elif NET5_0
    private const string TemporaryTargetAssemblyFramework = "net5.0";
#elif NET6_0
    private const string TemporaryTargetAssemblyFramework = "net6.0";
#elif NET7_0
    private const string TemporaryTargetAssemblyFramework = "net7.0";
#elif NET8_0
    private const string TemporaryTargetAssemblyFramework = "net8.0";
#elif NET9_0
    private const string TemporaryTargetAssemblyFramework = "net9.0";
#elif NET10_0
    private const string TemporaryTargetAssemblyFramework = "net10.0";
#elif NET11_0
    private const string TemporaryTargetAssemblyFramework = "net11.0";
#else
#error Unsupported test target framework.
#endif

    private static readonly string[] FrameworkAssemblyNames = { "System.Runtime", "System.Private.CoreLib", "mscorlib", "netstandard" };

    public DuckTypeAotProcessorsTests()
    {
        DuckType.ResetRuntimeModeForTests();
        DuckTypeAotEngine.ResetForTests();
    }

    [Fact]
    public void TypeToStringNamesShouldBeTheNamesOfTypeToString()
    {
        // Failure messages replayed from metadata name the types like dynamic duck typing, with Type.ToString().
        var types = new[]
        {
            typeof(int[]),
            typeof(Dictionary<string, List<int[]>>),
            typeof(KeyValuePair<int, string>[]),
            typeof(Dictionary<int, string>.KeyCollection),
            typeof(TestDuckTarget),
        };

        foreach (var type in types)
        {
            DuckTypeAotNameHelpers.ToTypeToStringName(type.FullName!).Should().Be(type.ToString());
        }

        DuckTypeAotNameHelpers.ToTypeToStringName("Example.Outer/Inner").Should().Be("Example.Outer+Inner");
    }

    [Theory]
    [InlineData("System.Collections.Generic.List`1", true)]
    [InlineData("System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]]", false)]
    [InlineData("Example.Outer`1+Inner`1[[System.String, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]]", false)]
    [InlineData("Example.Outer`1+Inner`1[[System.String, System.Private.CoreLib]]", true)]
    [InlineData("Example.Container`1[!0]", true)]
    [InlineData("Example.Container`2[[System.String, System.Private.CoreLib],!!0]", true)]
    [InlineData("Example.Outer`1+Inner", true)]
    // A backtick that isn't an arity, or a '!' in a name, doesn't make a type generic.
    [InlineData("Example.A`B", false)]
    [InlineData("Example.A`1x", false)]
    [InlineData("Example.A!B", false)]
    [InlineData("Example.Holder`1[[Example.A!B, Example]]", false)]
    public void OpenGenericNameDetectionShouldHandleNestedClosedGenericTypeNames(string typeName, bool expectedIsOpen)
    {
        DuckTypeAotNameHelpers.IsOpenGenericTypeName(typeName).Should().Be(expectedIsOpen);
    }

    [Fact]
    public void MapFileParserShouldAcceptOpenGenericMapRulesForBuildTimeExpansion()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyName = typeof(DuckTypeAotProcessorsTests).Assembly.GetName().Name;
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-open-generic-parser.json");
            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IClosedGenericDuckProxy<>).FullName,
                        proxyAssembly = sharedAssemblyName,
                        targetType = typeof(ClosedGenericDuckTarget<>).FullName,
                        targetAssembly = sharedAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var parseResult = DuckTypeAotMapFileParser.Parse(mapFilePath);

            parseResult.Errors.Should().BeEmpty();
            parseResult.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.ProxyTypeName, typeof(IClosedGenericDuckProxy<>).FullName, StringComparison.Ordinal) &&
                string.Equals(mapping.TargetTypeName, typeof(ClosedGenericDuckTarget<>).FullName, StringComparison.Ordinal));
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void ParseTypeAndAssemblyShouldHandleAssemblyQualifiedGenericType()
    {
        const string typeName = "System.Collections.Generic.Dictionary`2[[System.String, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]]";
        const string qualifiedType = typeName + ", System.Private.CoreLib";

        var (parsedTypeName, parsedAssemblyName) = DuckTypeAotNameHelpers.ParseTypeAndAssembly(qualifiedType);
        parsedTypeName.Should().Be(typeName);
        parsedAssemblyName.Should().Be("System.Private.CoreLib");
    }

    private static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "dd-trace-ducktype-aot-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
        }
    }
}
