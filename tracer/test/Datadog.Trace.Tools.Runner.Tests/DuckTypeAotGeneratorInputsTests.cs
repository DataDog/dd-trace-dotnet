// <copyright file="DuckTypeAotGeneratorInputsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Linq;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The inputs of the registry generator: the map file, the generic instantiations file, the declarations of the proxy
/// assemblies, and the resolution of the assemblies of every mapping.
/// </summary>
public class DuckTypeAotGeneratorInputsTests : IDisposable
{
    private static readonly string TestAssemblyName = typeof(DuckTypeAotGeneratorInputsTests).Assembly.GetName().Name!;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dd-ducktype-aot-inputs-" + Guid.NewGuid().ToString("N"));

    public DuckTypeAotGeneratorInputsTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void MapFileParserReadsTheModeAndDerivedTypesOfEachMapping()
    {
        const string map = """
            {
              "schemaVersion": "1",
              "mappings": [
                { "proxyType": "Proxies.IForward", "proxyAssembly": "Proxies", "targetType": "Targets.Forward", "targetAssembly": "Targets" },
                { "mode": "reverse", "proxyType": "Proxies.IReverse", "proxyAssembly": "Proxies", "targetType": "Targets.Delegation", "targetAssembly": "Targets" },
                { "proxyType": "Proxies.IBase", "proxyAssembly": "Proxies", "targetType": "Targets.Base", "targetAssembly": "Targets", "includeDerivedTypes": true }
              ]
            }
            """;

        var result = DuckTypeAotMapFileParser.ParseText(map, "map.json");

        result.Errors.Should().BeEmpty();
        result.Mappings.Should().HaveCount(3);
        result.Mappings.Single(m => m.ProxyTypeName == "Proxies.IForward").Mode.Should().Be(DuckTypeAotMappingMode.Forward);
        result.Mappings.Single(m => m.ProxyTypeName == "Proxies.IReverse").Mode.Should().Be(DuckTypeAotMappingMode.Reverse);
        result.Mappings.Single(m => m.ProxyTypeName == "Proxies.IBase").IncludesDerivedTypes.Should().BeTrue();
    }

    [Fact]
    public void MapFileParserReportsInvalidMapFiles()
    {
        DuckTypeAotMapFileParser.ParseText("""{ "schemaVersion": "2", "mappings": [] }""", "map.json").Errors
                                .Should().ContainSingle(error => error.Contains("schemaVersion must be '1'"));
        DuckTypeAotMapFileParser.ParseText("""{ "schemaVersion": "1" }""", "map.json").Errors
                                .Should().ContainSingle(error => error.Contains("must contain a 'mappings' array"));
        const string duplicates = """
            { "mappings": [
              { "proxyType": "P.I", "proxyAssembly": "P", "targetType": "T.C", "targetAssembly": "T" },
              { "proxyType": "P.I", "proxyAssembly": "P", "targetType": "T.C", "targetAssembly": "T" } ] }
            """;
        DuckTypeAotMapFileParser.ParseText(duplicates, "map.json").Errors.Should().ContainSingle(error => error.Contains("duplicate mappings"));
    }

    [Fact]
    public void GenericInstantiationsParserAcceptsClosedGenericTypesOnly()
    {
        var valid = Path.Combine(_directory, "valid.json");
        const string instantiations = """
            [
              "System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]], System.Private.CoreLib",
              { "type": "System.Collections.Generic.Dictionary`2[[System.String, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]]", "assembly": "System.Private.CoreLib" }
            ]
            """;
        File.WriteAllText(valid, instantiations);
        var result = DuckTypeAotGenericInstantiationsParser.Parse(valid);
        result.Errors.Should().BeEmpty();
        result.TypeRoots.Select(root => root.TypeName).Should().BeEquivalentTo(
            "System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]]",
            "System.Collections.Generic.Dictionary`2[[System.String, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]]");

        var invalid = Path.Combine(_directory, "invalid.json");
        File.WriteAllText(invalid, """{ "instantiations": [ "System.Collections.Generic.List`1, System.Private.CoreLib", "System.String, System.Private.CoreLib" ] }""");
        DuckTypeAotGenericInstantiationsParser.Parse(invalid).Errors.Should().HaveCount(2).And.Contain(error => error.Contains("is open")).And.Contain(error => error.Contains("is not generic"));
    }

    [Fact]
    public void AttributeDiscoveryFindsTheDuckTypeDeclarationsOfProxies()
    {
        var result = DuckTypeAotAttributeDiscovery.Discover(new[] { typeof(DuckTypeAotGeneratorInputsTests).Assembly.Location });

        result.Errors.Should().BeEmpty();
        result.Mappings.Should().Contain(mapping =>
            mapping.ProxyTypeName == typeof(IInputsDeclaredProxy).FullName &&
            mapping.TargetTypeName == typeof(InputsDeclaredTarget).FullName &&
            mapping.TargetAssemblyName == TestAssemblyName &&
            mapping.Mode == DuckTypeAotMappingMode.Forward &&
            mapping.Source == DuckTypeAotMappingSource.Attribute);
    }

    [Fact]
    public void MappingResolverResolvesTheAssembliesOfTheMappings()
    {
        var testAssembly = typeof(DuckTypeAotGeneratorInputsTests).Assembly.Location;
        var mapFile = Path.Combine(_directory, "map.json");
        var map = $$"""
            { "mappings": [
              { "proxyType": "{{typeof(IInputsDeclaredProxy).FullName}}", "proxyAssembly": "{{TestAssemblyName}}", "targetType": "{{typeof(InputsDeclaredTarget).FullName}}", "targetAssembly": "{{TestAssemblyName}}" },
              { "proxyType": "{{typeof(IInputsDeclaredProxy).FullName}}", "proxyAssembly": "{{TestAssemblyName}}", "targetType": "Missing.Target", "targetAssembly": "Missing.Assembly" } ] }
            """;
        File.WriteAllText(mapFile, map);
        var options = new DuckTypeAotGenerateOptions(
            proxyAssemblies: new[] { testAssembly },
            targetAssemblies: new[] { testAssembly },
            targetFolders: Array.Empty<string>(),
            targetFilters: Array.Empty<string>(),
            mapFile: mapFile,
            genericInstantiationsFile: null,
            outputPath: Path.Combine(_directory, "registry.dll"),
            assemblyName: null,
            trimmerDescriptorPath: Path.Combine(_directory, "registry.linker.xml"),
            propsPath: Path.Combine(_directory, "registry.props"));

        var result = DuckTypeAotMappingResolver.Resolve(options);

        result.ProxyAssemblyPathsByName.Should().ContainKey(TestAssemblyName);
        result.TargetAssemblyPathsByName.Should().ContainKey(TestAssemblyName);
        result.Mappings.Should().Contain(mapping => mapping.TargetTypeName == typeof(InputsDeclaredTarget).FullName);
        result.Errors.Should().Contain(error => error.Contains("Missing.Assembly"));
    }
}
