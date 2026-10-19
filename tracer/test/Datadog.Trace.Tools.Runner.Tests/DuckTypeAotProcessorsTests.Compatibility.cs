// <copyright file="DuckTypeAotProcessorsTests.Compatibility.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
#if NETCOREAPP2_1
using AssemblyLoadContext = Datadog.Trace.Tools.Runner.Tests.NetCore21AssemblyLoadContext;
#else
using System.Runtime.Loader;
#endif
using System.Security.Cryptography;
using System.Text;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.DuckTyping.Aot;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Spectre.Console;
using Xunit;

#pragma warning disable SA1201, SA1202 // Elements should appear in the correct order

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The compatibility tooling: verify-compat, the Bible catalogs and contracts, and the merge of discovered mappings into a map file.
/// </summary>
public partial class DuckTypeAotProcessorsTests
{
    [Fact]
    public void BibleExpectedOutcomesFileShouldRemainStrictEmptyContract()
    {
        var expectedOutcomesPath = GetDuckTypingAotCompatibilityFilePath(BibleExpectedOutcomesFileName);
        File.Exists(expectedOutcomesPath).Should().BeTrue($"expected checked-in compatibility artifact '{expectedOutcomesPath}' to exist");

        var expectedOutcomes = JsonConvert.DeserializeObject<DuckTypeAotExpectedOutcomesContract>(File.ReadAllText(expectedOutcomesPath));
        expectedOutcomes.Should().NotBeNull();
        expectedOutcomes!.ExpectedOutcomes.Should().BeEmpty();
        expectedOutcomes.DefaultStatus.Should().Be(DuckTypeAotCompatibilityStatuses.Compatible);
    }

    [Fact]
    public void BibleKnownLimitationsFileShouldRemainStrictEmptyContract()
    {
        var knownLimitationsPath = GetDuckTypingAotCompatibilityFilePath(BibleKnownLimitationsFileName);
        File.Exists(knownLimitationsPath).Should().BeTrue($"expected checked-in compatibility artifact '{knownLimitationsPath}' to exist");

        var knownLimitations = JsonConvert.DeserializeObject<DuckTypeAotKnownLimitationsContract>(File.ReadAllText(knownLimitationsPath));
        knownLimitations.Should().NotBeNull();
        knownLimitations!.KnownLimitations.Should().BeEmpty();
    }

    [Fact]
    public void VerifyCompatOptionsFactoriesShouldSeparateCanonicalMapAndLegacyContracts()
    {
        var canonical = DuckTypeAotVerifyCompatOptions.CreateCanonicalMapContract(
            compatReportPath: "report.md",
            compatMatrixPath: "matrix.json",
            mapFilePath: "map.json",
            mappingCatalogPath: "catalog.json",
            manifestPath: "manifest.json",
            scenarioInventoryPath: "inventory.json",
            strictAssemblyFingerprintValidation: false,
            failureMode: DuckTypeAotFailureMode.Strict);

        canonical.CompatReportPath.Should().Be("report.md");
        canonical.CompatMatrixPath.Should().Be("matrix.json");
        canonical.MapFilePath.Should().Be("map.json");
        canonical.MappingCatalogPath.Should().Be("catalog.json");
        canonical.ManifestPath.Should().Be("manifest.json");
        canonical.ScenarioInventoryPath.Should().Be("inventory.json");
        canonical.ExpectedOutcomesPath.Should().BeNull();
        canonical.KnownLimitationsPath.Should().BeNull();
        canonical.StrictAssemblyFingerprintValidation.Should().BeTrue();

        var legacy = DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
            compatReportPath: "report.md",
            compatMatrixPath: "matrix.json",
            mappingCatalogPath: "catalog.json",
            manifestPath: "manifest.json",
            scenarioInventoryPath: "inventory.json",
            expectedOutcomesPath: "expected.json",
            knownLimitationsPath: "known.json",
            strictAssemblyFingerprintValidation: false);

        legacy.CompatReportPath.Should().Be("report.md");
        legacy.CompatMatrixPath.Should().Be("matrix.json");
        legacy.MapFilePath.Should().BeEmpty();
        legacy.MappingCatalogPath.Should().Be("catalog.json");
        legacy.ManifestPath.Should().Be("manifest.json");
        legacy.ScenarioInventoryPath.Should().Be("inventory.json");
        legacy.ExpectedOutcomesPath.Should().Be("expected.json");
        legacy.KnownLimitationsPath.Should().Be("known.json");
        legacy.StrictAssemblyFingerprintValidation.Should().BeFalse();
    }

    [Fact]
    public void BibleMappingCatalogFileShouldParseAndContainRequiredSeedScenarios()
    {
        var catalogPath = GetDuckTypingAotCompatibilityFilePath(BibleMappingCatalogFileName);
        File.Exists(catalogPath).Should().BeTrue($"expected checked-in compatibility artifact '{catalogPath}' to exist");

        var parseResult = DuckTypeAotMappingCatalogParser.Parse(catalogPath);
        parseResult.Errors.Should().BeEmpty();
        parseResult.RequiredMappings.Should().NotBeEmpty();
        parseResult.RequiredMappings.Should().HaveCountGreaterOrEqualTo(116);

        var scenarioIds = parseResult.RequiredMappings
                                     .Where(mapping => !string.IsNullOrWhiteSpace(mapping.ScenarioId))
                                     .Select(mapping => mapping.ScenarioId!)
                                     .ToHashSet(StringComparer.Ordinal);

        scenarioIds.Should().Contain("A-01");
        scenarioIds.Should().Contain("A-02");
        scenarioIds.Should().Contain("A-03");
        scenarioIds.Should().Contain("A-04");
        scenarioIds.Should().Contain("A-05");
        scenarioIds.Should().Contain("A-06");
        scenarioIds.Should().Contain("A-07");
        scenarioIds.Should().Contain("A-08");
        scenarioIds.Should().Contain("A-09");
        scenarioIds.Should().Contain("A-10");
        scenarioIds.Should().Contain("A-11");
        scenarioIds.Should().Contain("A-12");
        scenarioIds.Should().Contain("A-13");
        scenarioIds.Should().Contain("A-14");
        scenarioIds.Should().Contain("A-15");
        scenarioIds.Should().Contain("B-16");
        scenarioIds.Should().Contain("B-17");
        scenarioIds.Should().Contain("B-18");
        scenarioIds.Should().Contain("B-19");
        scenarioIds.Should().Contain("B-20");
        scenarioIds.Should().Contain("B-21");
        scenarioIds.Should().Contain("B-22");
        scenarioIds.Should().Contain("B-23");
        scenarioIds.Should().Contain("B-24");
        scenarioIds.Should().Contain("B-25");
        scenarioIds.Should().Contain("B-26");
        scenarioIds.Should().Contain("B-27");
        scenarioIds.Should().Contain("C-28");
        scenarioIds.Should().Contain("C-29");
        scenarioIds.Should().Contain("C-30");
        scenarioIds.Should().Contain("C-31");
        scenarioIds.Should().Contain("C-32");
        scenarioIds.Should().Contain("C-33");
        scenarioIds.Should().Contain("D-34");
        scenarioIds.Should().Contain("D-35");
        scenarioIds.Should().Contain("D-36");
        scenarioIds.Should().Contain("D-37");
        scenarioIds.Should().Contain("E-38");
        scenarioIds.Should().Contain("E-39");
        scenarioIds.Should().Contain("E-40");
        scenarioIds.Should().Contain("E-41");
        scenarioIds.Should().Contain("FG-1");
        scenarioIds.Should().Contain("FS-1");
        scenarioIds.Should().Contain("FF-1");
        scenarioIds.Should().Contain("FM-1");
        scenarioIds.Should().Contain("RT-1");
        scenarioIds.Should().Contain("FG-2");
        scenarioIds.Should().Contain("FS-5");
        scenarioIds.Should().Contain("FF-2");
        scenarioIds.Should().Contain("FM-8");
        scenarioIds.Should().Contain("RT-3");
        scenarioIds.Should().Contain("FG-3");
        scenarioIds.Should().Contain("FG-4");
        scenarioIds.Should().Contain("FG-5");
        scenarioIds.Should().Contain("FG-6");
        scenarioIds.Should().Contain("FG-9");
        scenarioIds.Should().Contain("FG-10");
        scenarioIds.Should().Contain("FS-2");
        scenarioIds.Should().Contain("FS-6");
        scenarioIds.Should().Contain("FS-7");
        scenarioIds.Should().Contain("FF-3");
        scenarioIds.Should().Contain("FF-4");
        scenarioIds.Should().Contain("FF-5");
        scenarioIds.Should().Contain("FF-6");
        scenarioIds.Should().Contain("RT-2");
        scenarioIds.Should().Contain("FG-7");
        scenarioIds.Should().Contain("FG-8");
        scenarioIds.Should().Contain("FS-3");
        scenarioIds.Should().Contain("FS-4");
        scenarioIds.Should().Contain("FM-2");
        scenarioIds.Should().Contain("FM-3");
        scenarioIds.Should().Contain("FM-4");
        scenarioIds.Should().Contain("FM-5");
        scenarioIds.Should().Contain("FM-6");
        scenarioIds.Should().Contain("FM-7");
        scenarioIds.Should().Contain("RT-4");
        scenarioIds.Should().Contain("RT-5");
        scenarioIds.Should().Contain("EX-01");
        scenarioIds.Should().Contain("EX-02");
        scenarioIds.Should().Contain("EX-03");
        scenarioIds.Should().Contain("EX-04");
        scenarioIds.Should().Contain("EX-05");
        scenarioIds.Should().Contain("EX-06");
        scenarioIds.Should().Contain("EX-07");
        scenarioIds.Should().Contain("EX-08");
        scenarioIds.Should().Contain("EX-09");
        scenarioIds.Should().Contain("EX-10");
        scenarioIds.Should().Contain("EX-11");
        scenarioIds.Should().Contain("EX-12");
        scenarioIds.Should().Contain("EX-13");
        scenarioIds.Should().Contain("EX-14");
        scenarioIds.Should().Contain("EX-15");
        scenarioIds.Should().Contain("EX-16");
        scenarioIds.Should().Contain("EX-17");
        scenarioIds.Should().Contain("EX-18");
        scenarioIds.Should().Contain("EX-19");
        scenarioIds.Should().Contain("EX-20");
        scenarioIds.Should().Contain("TX-A");
        scenarioIds.Should().Contain("TX-B");
        scenarioIds.Should().Contain("TX-C");
        scenarioIds.Should().Contain("TX-D");
        scenarioIds.Should().Contain("TX-E");
        scenarioIds.Should().Contain("TX-F");
        scenarioIds.Should().Contain("TX-G");
        scenarioIds.Should().Contain("TX-H");
        scenarioIds.Should().Contain("TX-I");
        scenarioIds.Should().Contain("TX-J");
        scenarioIds.Should().Contain("TX-K");
        scenarioIds.Should().Contain("TX-L");
        scenarioIds.Should().Contain("TX-M");
        scenarioIds.Should().Contain("TX-N");
        scenarioIds.Should().Contain("TX-O");
        scenarioIds.Should().Contain("TX-P");
        scenarioIds.Should().Contain("TX-Q");
        scenarioIds.Should().Contain("TX-R");
        scenarioIds.Should().Contain("TX-S");
        scenarioIds.Should().Contain("TX-T");
    }

    [Fact]
    public void BibleMappingCatalogShouldMatchScenarioInventoryContract()
    {
        var catalogPath = GetDuckTypingAotCompatibilityFilePath(BibleMappingCatalogFileName);
        var inventoryPath = GetDuckTypingAotCompatibilityFilePath(BibleScenarioInventoryFileName);

        var catalogResult = DuckTypeAotMappingCatalogParser.Parse(catalogPath);
        var inventoryResult = DuckTypeAotScenarioInventoryParser.Parse(inventoryPath);

        catalogResult.Errors.Should().BeEmpty();
        inventoryResult.Errors.Should().BeEmpty();
        catalogResult.RequiredMappings.Should().NotBeEmpty();
        inventoryResult.RequiredScenarios.Should().NotBeEmpty();

        var catalogScenarioIds = catalogResult.RequiredMappings
                                            .Select(mapping => mapping.ScenarioId)
                                            .Where(scenarioId => !string.IsNullOrWhiteSpace(scenarioId))
                                            .Select(scenarioId => scenarioId!)
                                            .ToHashSet(StringComparer.Ordinal);
        var inventoryScenarioIds = inventoryResult.RequiredScenarios.ToHashSet(StringComparer.Ordinal);

        var catalogEntriesMissingFromInventory = catalogScenarioIds
                                                .Where(scenarioId => !IsScenarioTrackedByInventoryForTest(scenarioId, inventoryScenarioIds))
                                                .OrderBy(scenarioId => scenarioId, StringComparer.Ordinal)
                                                .ToList();
        catalogEntriesMissingFromInventory.Should().BeEmpty(
            "every catalog scenario id must be explicitly tracked by the inventory or a reviewed wildcard family");

        var inventoryEntriesMissingFromCatalog = inventoryScenarioIds
                                                .Where(requiredEntry => !IsScenarioCoveredByCatalogForTest(requiredEntry, catalogScenarioIds))
                                                .OrderBy(requiredEntry => requiredEntry, StringComparer.Ordinal)
                                                .ToList();
        inventoryEntriesMissingFromCatalog.Should().BeEmpty(
            "every required inventory scenario or wildcard family must have at least one catalog mapping");
    }

    [Fact]
    public void BibleExpectedOutcomesFileShouldNotContainApprovedNonCompatibleScenarios()
    {
        var expectedOutcomesPath = GetDuckTypingAotCompatibilityFilePath(BibleExpectedOutcomesFileName);
        File.Exists(expectedOutcomesPath).Should().BeTrue($"expected checked-in compatibility artifact '{expectedOutcomesPath}' to exist");

        var document = JsonConvert.DeserializeObject<ExpectedOutcomesTestDocument>(File.ReadAllText(expectedOutcomesPath));
        document.Should().NotBeNull();
        document!.ExpectedOutcomes.Should().NotBeNull();
        document.ExpectedOutcomes.Should().BeEmpty("the parity contract requires all Bible scenarios to be fully compatible");
    }

    [Fact]
    public void GenerateProcessorShouldExpandOpenGenericMappingsFromClosedGenericInstantiations()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            DuckType.ResetRuntimeModeForTests();

            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.OpenGeneric.Expanded.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-open-generic-expanded.json");
            var genericInstantiationsPath = Path.Combine(tempDirectory, "ducktype-aot-open-generic-expanded-instantiations.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-open-generic-expanded.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-open-generic-expanded.props");

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

            var genericInstantiationsDocument = new
            {
                instantiations = new object[]
                {
                    new
                    {
                        type = typeof(ClosedGenericDuckTarget<int>).FullName,
                        assembly = sharedAssemblyName
                    },
                    new
                    {
                        type = typeof(ClosedGenericDuckTarget<string>).FullName,
                        assembly = sharedAssemblyName
                    }
                }
            };
            File.WriteAllText(genericInstantiationsPath, JsonConvert.SerializeObject(genericInstantiationsDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: genericInstantiationsPath,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.OpenGeneric.Expanded",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var manifestPath = $"{outputPath}.manifest.json";
            var manifest = JsonConvert.DeserializeObject<DuckTypeAotManifest>(File.ReadAllText(manifestPath));
            manifest.Should().NotBeNull();
            manifest!.Mappings.Should().HaveCount(2);
            // Mapping type names are canonical: generic arguments are qualified with simple assembly names.
            manifest.Mappings.Should().Contain(mapping =>
                string.Equals(mapping.ProxyType, DuckTypeAotNameHelpers.CanonicalizeTypeName(typeof(IClosedGenericDuckProxy<int>).FullName!), StringComparison.Ordinal) &&
                string.Equals(mapping.TargetType, DuckTypeAotNameHelpers.CanonicalizeTypeName(typeof(ClosedGenericDuckTarget<int>).FullName!), StringComparison.Ordinal));
            manifest.Mappings.Should().Contain(mapping =>
                string.Equals(mapping.ProxyType, DuckTypeAotNameHelpers.CanonicalizeTypeName(typeof(IClosedGenericDuckProxy<string>).FullName!), StringComparison.Ordinal) &&
                string.Equals(mapping.TargetType, DuckTypeAotNameHelpers.CanonicalizeTypeName(typeof(ClosedGenericDuckTarget<string>).FullName!), StringComparison.Ordinal));
            manifest.Mappings.Should().OnlyContain(mapping => !mapping.ProxyType!.Contains("Version=") && !mapping.TargetType!.Contains("Version="));

            var verifyOptions = DuckTypeAotVerifyCompatOptions.CreateCanonicalMapContract(
                compatReportPath: outputPath + ".compat.md",
                compatMatrixPath: outputPath + ".compat.json",
                mapFilePath: mapFilePath,
                manifestPath: manifestPath);
            DuckTypeAotVerifyCompatProcessor.Process(verifyOptions).Should().Be(0);

            var explicitRootsOptions = DuckTypeAotVerifyCompatOptions.CreateCanonicalMapContract(
                compatReportPath: outputPath + ".compat.md",
                compatMatrixPath: outputPath + ".compat.json",
                mapFilePath: mapFilePath,
                genericInstantiationsPath: genericInstantiationsPath);
            DuckTypeAotVerifyCompatProcessor.Process(explicitRootsOptions).Should().Be(0);
            DuckTypeAotVerifyCompatProcessor.Process(DuckTypeAotVerifyCompatOptions.CreateCanonicalMapContract(
                compatReportPath: outputPath + ".compat.md",
                compatMatrixPath: outputPath + ".compat.json",
                mapFilePath: mapFilePath)).Should().Be(1, "open map rules require independently supplied closed roots");

            var matrixJson = File.ReadAllText(outputPath + ".compat.json");
            var incompleteMatrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(matrixJson)!;
            incompleteMatrix.Mappings.RemoveAt(0);
            File.WriteAllText(outputPath + ".compat.json", JsonConvert.SerializeObject(incompleteMatrix));
            DuckTypeAotVerifyCompatProcessor.Process(explicitRootsOptions).Should().Be(1, "every closed instantiation must be covered");
            File.WriteAllText(outputPath + ".compat.json", matrixJson);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-OpenGeneric-Expanded", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var intProxy = DuckType.Create(typeof(IClosedGenericDuckProxy<int>), new ClosedGenericDuckTarget<int>(42));
                intProxy.Should().BeAssignableTo<IClosedGenericDuckProxy<int>>();
                ((IClosedGenericDuckProxy<int>)intProxy!).Value.Should().Be(42);

                var stringProxy = DuckType.Create(typeof(IClosedGenericDuckProxy<string>), new ClosedGenericDuckTarget<string>("closed"));
                stringProxy.Should().BeAssignableTo<IClosedGenericDuckProxy<string>>();
                ((IClosedGenericDuckProxy<string>)stringProxy!).Value.Should().Be("closed");
            }
            finally
            {
                DuckType.ResetRuntimeModeForTests();
                DuckTypeAotEngine.ResetForTests();
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldSucceedWhenAllMappingsAreCompatible()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "MAP-0001",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var result = DuckTypeAotVerifyCompatProcessor.Process(DuckTypeAotVerifyCompatOptions.CreateLegacyContract(reportPath, matrixPath, mappingCatalogPath: null, manifestPath: null, strictAssemblyFingerprintValidation: false));
            result.Should().Be(0);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldAllowMissingCompatReportWhenAllMappingsAreCompatible()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");

            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "MAP-0001",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var result = DuckTypeAotVerifyCompatProcessor.Process(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(string.Empty, matrixPath, mappingCatalogPath: null, manifestPath: null, strictAssemblyFingerprintValidation: false));
            result.Should().Be(0);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldAllowGroupedScenarioIdsAcrossMultipleMappings()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "C-28",
                        Mode = "forward",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    },
                    new()
                    {
                        Id = "C-28",
                        Mode = "forward",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckNamedMethodProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckNamedMethodTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var result = DuckTypeAotVerifyCompatProcessor.Process(DuckTypeAotVerifyCompatOptions.CreateLegacyContract(reportPath, matrixPath, mappingCatalogPath: null, manifestPath: null, strictAssemblyFingerprintValidation: false));
            result.Should().Be(0);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldFailWhenAnyMappingIsNotCompatible()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "MAP-0001",
                        Status = DuckTypeAotCompatibilityStatuses.PendingProxyEmission
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var result = DuckTypeAotVerifyCompatProcessor.Process(DuckTypeAotVerifyCompatOptions.CreateLegacyContract(reportPath, matrixPath, mappingCatalogPath: null, manifestPath: null, strictAssemblyFingerprintValidation: false));
            result.Should().Be(1);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldSucceedWhenExpectedOutcomesContractIsStrictEmpty()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var expectedOutcomesPath = Path.Combine(tempDirectory, "ducktyping-aot-expected-outcomes.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "RT-2",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    },
                    new()
                    {
                        Id = "E-39",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var expectedOutcomes = new
            {
                schemaVersion = "1",
                defaultStatus = DuckTypeAotCompatibilityStatuses.Compatible,
                expectedOutcomes = Array.Empty<object>()
            };
            File.WriteAllText(expectedOutcomesPath, JsonConvert.SerializeObject(expectedOutcomes, Formatting.Indented));

            var result = DuckTypeAotVerifyCompatProcessor.Process(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
                    reportPath,
                    matrixPath,
                    mappingCatalogPath: null,
                    manifestPath: null,
                    scenarioInventoryPath: null,
                    expectedOutcomesPath: expectedOutcomesPath,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(0);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldFailWhenExpectedOutcomesDoNotMatchStatus()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var expectedOutcomesPath = Path.Combine(tempDirectory, "ducktyping-aot-expected-outcomes.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "RT-2",
                        Status = DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var expectedOutcomes = new
            {
                defaultStatus = DuckTypeAotCompatibilityStatuses.Compatible,
                expectedOutcomes = new[]
                {
                    new
                    {
                        scenarioId = "RT-2",
                        status = DuckTypeAotCompatibilityStatuses.MissingTargetMethod
                    }
                }
            };
            File.WriteAllText(expectedOutcomesPath, JsonConvert.SerializeObject(expectedOutcomes, Formatting.Indented));

            var result = DuckTypeAotVerifyCompatProcessor.Process(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
                    reportPath,
                    matrixPath,
                    mappingCatalogPath: null,
                    manifestPath: null,
                    scenarioInventoryPath: null,
                    expectedOutcomesPath: expectedOutcomesPath,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(1);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldFailWhenExpectedOutcomesContainStaleEntries()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var expectedOutcomesPath = Path.Combine(tempDirectory, "ducktyping-aot-expected-outcomes.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "RT-2",
                        Status = DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var expectedOutcomes = new
            {
                defaultStatus = DuckTypeAotCompatibilityStatuses.Compatible,
                expectedOutcomes = new[]
                {
                    new
                    {
                        scenarioId = "RT-2",
                        status = DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature
                    },
                    new
                    {
                        scenarioId = "E-39",
                        status = DuckTypeAotCompatibilityStatuses.MissingTargetMethod
                    }
                }
            };
            File.WriteAllText(expectedOutcomesPath, JsonConvert.SerializeObject(expectedOutcomes, Formatting.Indented));

            var result = DuckTypeAotVerifyCompatProcessor.Process(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
                    reportPath,
                    matrixPath,
                    mappingCatalogPath: null,
                    manifestPath: null,
                    scenarioInventoryPath: null,
                    expectedOutcomesPath: expectedOutcomesPath,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(1);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldFailWhenMapFileContainsDuplicateMappings()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var mapFilePath = Path.Combine(tempDirectory, "ducktyping-aot-mappings.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "A-01",
                        Mode = "forward",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var mapFile = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        proxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        targetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        targetAssembly = "Datadog.Trace.Tools.Runner.Tests"
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        proxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        targetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        targetAssembly = "Datadog.Trace.Tools.Runner.Tests"
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapFile, Formatting.Indented));

            var result = DuckTypeAotVerifyCompatProcessor.Process(
                DuckTypeAotVerifyCompatOptions.CreateCanonicalMapContract(
                    compatReportPath: reportPath,
                    compatMatrixPath: matrixPath,
                    mapFilePath: mapFilePath,
                    manifestPath: null,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(1);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldSucceedWhenMapFileMatchesCompatibilityMatrix()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var mapFilePath = Path.Combine(tempDirectory, "ducktyping-aot-mappings.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "A-01",
                        Mode = "forward",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var mapFile = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        proxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        targetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        targetAssembly = "Datadog.Trace.Tools.Runner.Tests"
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapFile, Formatting.Indented));

            var result = DuckTypeAotVerifyCompatProcessor.Process(
                DuckTypeAotVerifyCompatOptions.CreateCanonicalMapContract(
                    compatReportPath: reportPath,
                    compatMatrixPath: matrixPath,
                    mapFilePath: mapFilePath,
                    manifestPath: null,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(0);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldValidateScenarioInventoryFromCatalogWhenMapFileIsProvided()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var mapFilePath = Path.Combine(tempDirectory, "ducktyping-aot-mappings.json");
            var mappingCatalogPath = Path.Combine(tempDirectory, "ducktyping-aot-mapping-catalog.json");
            var scenarioInventoryPath = Path.Combine(tempDirectory, "ducktyping-aot-scenario-inventory.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "MAP-0001",
                        Mode = "forward",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var mapFile = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        proxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        targetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        targetAssembly = "Datadog.Trace.Tools.Runner.Tests"
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapFile, Formatting.Indented));

            var catalog = new
            {
                requiredMappings = new[]
                {
                    new
                    {
                        scenarioId = "A-01",
                        mode = "forward",
                        proxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        proxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        targetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        targetAssembly = "Datadog.Trace.Tools.Runner.Tests"
                    }
                }
            };
            File.WriteAllText(mappingCatalogPath, JsonConvert.SerializeObject(catalog, Formatting.Indented));

            var scenarioInventory = new
            {
                requiredScenarios = new[] { "A-01" }
            };
            File.WriteAllText(scenarioInventoryPath, JsonConvert.SerializeObject(scenarioInventory, Formatting.Indented));

            var result = DuckTypeAotVerifyCompatProcessor.Process(
                DuckTypeAotVerifyCompatOptions.CreateCanonicalMapContract(
                    compatReportPath: reportPath,
                    compatMatrixPath: matrixPath,
                    mapFilePath: mapFilePath,
                    mappingCatalogPath: mappingCatalogPath,
                    manifestPath: null,
                    scenarioInventoryPath: scenarioInventoryPath,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(0);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldValidateCatalogWhenMapFileIsProvided()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var mapFilePath = Path.Combine(tempDirectory, "ducktyping-aot-mappings.json");
            var mappingCatalogPath = Path.Combine(tempDirectory, "ducktyping-aot-mapping-catalog.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "A-01",
                        Mode = "forward",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var mapFile = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        proxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        targetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        targetAssembly = "Datadog.Trace.Tools.Runner.Tests"
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapFile, Formatting.Indented));

            var catalog = new
            {
                requiredMappings = new[]
                {
                    new
                    {
                        scenarioId = "A-01",
                        mode = "forward",
                        proxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        proxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        targetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        targetAssembly = "Datadog.Trace.Tools.Runner.Tests"
                    },
                    new
                    {
                        scenarioId = "B-16",
                        mode = "forward",
                        proxyType = "Datadog.Trace.Tools.Runner.Tests.IMissingDuckProxy",
                        proxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        targetType = "Datadog.Trace.Tools.Runner.Tests.MissingDuckTarget",
                        targetAssembly = "Datadog.Trace.Tools.Runner.Tests"
                    }
                }
            };
            File.WriteAllText(mappingCatalogPath, JsonConvert.SerializeObject(catalog, Formatting.Indented));

            var (result, output) = ProcessAndCapture(
                DuckTypeAotVerifyCompatOptions.CreateCanonicalMapContract(
                    compatReportPath: reportPath,
                    compatMatrixPath: matrixPath,
                    mapFilePath: mapFilePath,
                    mappingCatalogPath: mappingCatalogPath,
                    manifestPath: null,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(1);
            output.Should().Contain("--compat-matrix is missing required mapping from --mapping-catalog");
            output.Should().Contain("IMissingDuckProxy");
            output.Should().Contain("MissingDuckTarget");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldFailWhenCatalogOmitsMapFileMapping()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var mapFilePath = Path.Combine(tempDirectory, "ducktyping-aot-mappings.json");
            var mappingCatalogPath = Path.Combine(tempDirectory, "ducktyping-aot-mapping-catalog.json");
            var scenarioInventoryPath = Path.Combine(tempDirectory, "ducktyping-aot-scenario-inventory.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "MAP-0001",
                        Mode = "forward",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    },
                    new()
                    {
                        Id = "MAP-0002",
                        Mode = "forward",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckNamedMethodProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckNamedMethodTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var mapFile = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        proxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        targetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        targetAssembly = "Datadog.Trace.Tools.Runner.Tests"
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckNamedMethodProxy",
                        proxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        targetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckNamedMethodTarget",
                        targetAssembly = "Datadog.Trace.Tools.Runner.Tests"
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapFile, Formatting.Indented));

            var catalog = new
            {
                requiredMappings = new[]
                {
                    new
                    {
                        scenarioId = "A-01",
                        mode = "forward",
                        proxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        proxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        targetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        targetAssembly = "Datadog.Trace.Tools.Runner.Tests"
                    }
                }
            };
            File.WriteAllText(mappingCatalogPath, JsonConvert.SerializeObject(catalog, Formatting.Indented));

            var scenarioInventory = new
            {
                requiredScenarios = new[] { "A-01" }
            };
            File.WriteAllText(scenarioInventoryPath, JsonConvert.SerializeObject(scenarioInventory, Formatting.Indented));

            var (result, output) = ProcessAndCapture(
                DuckTypeAotVerifyCompatOptions.CreateCanonicalMapContract(
                    compatReportPath: reportPath,
                    compatMatrixPath: matrixPath,
                    mapFilePath: mapFilePath,
                    mappingCatalogPath: mappingCatalogPath,
                    manifestPath: null,
                    scenarioInventoryPath: scenarioInventoryPath,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(1);
            output.Should().Contain("--mapping-catalog is missing mapping declared in --map-file/--compat-matrix");
            output.Should().Contain("ITestDuckNamedMethodProxy");
            output.Should().Contain("TestDuckNamedMethodTarget");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldFailWhenCatalogContainsNonCompatibleRequiredMapping()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var mappingCatalogPath = Path.Combine(tempDirectory, "ducktyping-aot-mapping-catalog.json");
            var expectedOutcomesPath = Path.Combine(tempDirectory, "ducktyping-aot-expected-outcomes.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "E-39",
                        Mode = "reverse",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.MissingTargetMethod
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var catalog = new
            {
                requiredMappings = new[]
                {
                    new
                    {
                        scenarioId = "E-39",
                        mode = "reverse",
                        proxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        proxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        targetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        targetAssembly = "Datadog.Trace.Tools.Runner.Tests"
                    }
                }
            };
            File.WriteAllText(mappingCatalogPath, JsonConvert.SerializeObject(catalog, Formatting.Indented));

            var expectedOutcomes = new
            {
                defaultStatus = DuckTypeAotCompatibilityStatuses.Compatible,
                expectedOutcomes = new[]
                {
                    new
                    {
                        scenarioId = "E-39",
                        status = DuckTypeAotCompatibilityStatuses.MissingTargetMethod
                    }
                }
            };
            File.WriteAllText(expectedOutcomesPath, JsonConvert.SerializeObject(expectedOutcomes, Formatting.Indented));

            var result = DuckTypeAotVerifyCompatProcessor.Process(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
                    reportPath,
                    matrixPath,
                    mappingCatalogPath,
                    manifestPath: null,
                    scenarioInventoryPath: null,
                    expectedOutcomesPath: expectedOutcomesPath,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(1);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldFailWhenCatalogScenarioIdMismatchesCompatibilityMatrix()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var mappingCatalogPath = Path.Combine(tempDirectory, "ducktyping-aot-mapping-catalog.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "A-01",
                        Mode = "forward",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var catalog = new
            {
                requiredMappings = new[]
                {
                    new
                    {
                        scenarioId = "A-02",
                        mode = "forward",
                        proxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        proxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        targetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        targetAssembly = "Datadog.Trace.Tools.Runner.Tests"
                    }
                }
            };
            File.WriteAllText(mappingCatalogPath, JsonConvert.SerializeObject(catalog, Formatting.Indented));

            var (result, output) = ProcessAndCapture(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
                    reportPath,
                    matrixPath,
                    mappingCatalogPath: mappingCatalogPath,
                    manifestPath: null,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(1);
            output.Should().Contain("Scenario id mismatch for required mapping");
            output.Should().Contain("Expected='A-02', actual='A-01'");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldFailWhenCatalogMappingHasNoScenarioId()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var mappingCatalogPath = Path.Combine(tempDirectory, "ducktyping-aot-mapping-catalog.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "MAP-0001",
                        Mode = "forward",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var catalog = new
            {
                requiredMappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        proxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        targetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        targetAssembly = "Datadog.Trace.Tools.Runner.Tests"
                    }
                }
            };
            File.WriteAllText(mappingCatalogPath, JsonConvert.SerializeObject(catalog, Formatting.Indented));

            var (result, output) = ProcessAndCapture(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
                    reportPath,
                    matrixPath,
                    mappingCatalogPath: mappingCatalogPath,
                    manifestPath: null,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(1);
            output.Should().Contain("--mapping-catalog required mapping is missing scenarioId");
            output.Should().Contain("ITestDuckProxy");
            output.Should().Contain("TestDuckTarget");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldSucceedWhenScenarioInventoryMatchesCompatibilityMatrix()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var scenarioInventoryPath = Path.Combine(tempDirectory, "ducktyping-aot-scenario-inventory.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "A-01",
                        Mode = "forward",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    },
                    new()
                    {
                        Id = "FG-001",
                        Mode = "forward",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var scenarioInventory = new
            {
                schemaVersion = "1",
                requiredScenarios = new[] { "A-01", "FG-*" }
            };
            File.WriteAllText(scenarioInventoryPath, JsonConvert.SerializeObject(scenarioInventory, Formatting.Indented));

            var result = DuckTypeAotVerifyCompatProcessor.Process(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
                    reportPath,
                    matrixPath,
                    mappingCatalogPath: null,
                    manifestPath: null,
                    scenarioInventoryPath: scenarioInventoryPath,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(0);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldFailWhenScenarioInventoryRequiresMissingScenario()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var scenarioInventoryPath = Path.Combine(tempDirectory, "ducktyping-aot-scenario-inventory.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "A-01",
                        Mode = "forward",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var scenarioInventory = new
            {
                schemaVersion = "1",
                requiredScenarios = new[] { "A-01", "B-16" }
            };
            File.WriteAllText(scenarioInventoryPath, JsonConvert.SerializeObject(scenarioInventory, Formatting.Indented));

            var (result, output) = ProcessAndCapture(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
                    reportPath,
                    matrixPath,
                    mappingCatalogPath: null,
                    manifestPath: null,
                    scenarioInventoryPath: scenarioInventoryPath,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(1);
            output.Should().Contain("--compat-matrix is missing required scenario from --scenario-inventory: 'B-16'");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldFailWhenCompatibilityMatrixContainsUntrackedScenario()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var scenarioInventoryPath = Path.Combine(tempDirectory, "ducktyping-aot-scenario-inventory.json");

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "A-01",
                        Mode = "forward",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    },
                    new()
                    {
                        Id = "X-99",
                        Mode = "forward",
                        ProxyType = "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                        ProxyAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        TargetType = "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                        TargetAssembly = "Datadog.Trace.Tools.Runner.Tests",
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var scenarioInventory = new
            {
                schemaVersion = "1",
                requiredScenarios = new[] { "A-01" }
            };
            File.WriteAllText(scenarioInventoryPath, JsonConvert.SerializeObject(scenarioInventory, Formatting.Indented));

            var (result, output) = ProcessAndCapture(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
                    reportPath,
                    matrixPath,
                    mappingCatalogPath: null,
                    manifestPath: null,
                    scenarioInventoryPath: scenarioInventoryPath,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(1);
            output.Should().Contain("--compat-matrix contains scenario id 'X-99' that is not tracked by --scenario-inventory");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldSucceedWhenManifestMatchesCompatibilityMatrix()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var manifestPath = Path.Combine(tempDirectory, "ducktyping-aot-manifest.json");
            var mapping = new DuckTypeAotMapping(
                proxyTypeName: "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                proxyAssemblyName: "Datadog.Trace.Tools.Runner.Tests",
                targetTypeName: "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                targetAssemblyName: "Datadog.Trace.Tools.Runner.Tests",
                mode: DuckTypeAotMappingMode.Forward,
                source: DuckTypeAotMappingSource.MapFile,
                scenarioId: "A-01");
            var mappingChecksum = ComputeMappingIdentityChecksumForTest(mapping.Key);

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                SchemaVersion = "1",
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "A-01",
                        MappingIdentityChecksum = mappingChecksum,
                        Mode = "forward",
                        ProxyType = mapping.ProxyTypeName,
                        ProxyAssembly = mapping.ProxyAssemblyName,
                        TargetType = mapping.TargetTypeName,
                        TargetAssembly = mapping.TargetAssemblyName,
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var manifest = new DuckTypeAotManifest
            {
                SchemaVersion = "1",
                Mappings = new List<DuckTypeAotManifestMapping>
                {
                    new()
                    {
                        Mode = "forward",
                        ScenarioId = "A-01",
                        MappingIdentityChecksum = mappingChecksum,
                        ProxyType = mapping.ProxyTypeName,
                        ProxyAssembly = mapping.ProxyAssemblyName,
                        TargetType = mapping.TargetTypeName,
                        TargetAssembly = mapping.TargetAssemblyName,
                        Source = "mapfile"
                    }
                }
            };
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            var result = DuckTypeAotVerifyCompatProcessor.Process(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
                    reportPath,
                    matrixPath,
                    mappingCatalogPath: null,
                    manifestPath: manifestPath,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(0);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldFailWhenManifestChecksumDiffersFromCompatibilityMatrix()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var manifestPath = Path.Combine(tempDirectory, "ducktyping-aot-manifest.json");
            var mapping = new DuckTypeAotMapping(
                proxyTypeName: "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                proxyAssemblyName: "Datadog.Trace.Tools.Runner.Tests",
                targetTypeName: "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                targetAssemblyName: "Datadog.Trace.Tools.Runner.Tests",
                mode: DuckTypeAotMappingMode.Forward,
                source: DuckTypeAotMappingSource.MapFile,
                scenarioId: "A-01");
            var mappingChecksum = ComputeMappingIdentityChecksumForTest(mapping.Key);
            var mismatchedChecksum = new string('f', 64);

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                SchemaVersion = "1",
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "A-01",
                        MappingIdentityChecksum = mappingChecksum,
                        Mode = "forward",
                        ProxyType = mapping.ProxyTypeName,
                        ProxyAssembly = mapping.ProxyAssemblyName,
                        TargetType = mapping.TargetTypeName,
                        TargetAssembly = mapping.TargetAssemblyName,
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var manifest = new DuckTypeAotManifest
            {
                SchemaVersion = "1",
                Mappings = new List<DuckTypeAotManifestMapping>
                {
                    new()
                    {
                        Mode = "forward",
                        ScenarioId = "A-01",
                        MappingIdentityChecksum = mismatchedChecksum,
                        ProxyType = mapping.ProxyTypeName,
                        ProxyAssembly = mapping.ProxyAssemblyName,
                        TargetType = mapping.TargetTypeName,
                        TargetAssembly = mapping.TargetAssemblyName,
                        Source = "mapfile"
                    }
                }
            };
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            var result = DuckTypeAotVerifyCompatProcessor.Process(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
                    reportPath,
                    matrixPath,
                    mappingCatalogPath: null,
                    manifestPath: manifestPath,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(1);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldWarnButSucceedWhenAssemblyFingerprintDriftsInNonStrictMode()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var manifestPath = Path.Combine(tempDirectory, "ducktyping-aot-manifest.json");
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;
            var mapping = new DuckTypeAotMapping(
                proxyTypeName: "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                proxyAssemblyName: "Datadog.Trace.Tools.Runner.Tests",
                targetTypeName: "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                targetAssemblyName: "Datadog.Trace.Tools.Runner.Tests",
                mode: DuckTypeAotMappingMode.Forward,
                source: DuckTypeAotMappingSource.MapFile,
                scenarioId: "A-01");
            var mappingChecksum = ComputeMappingIdentityChecksumForTest(mapping.Key);

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                SchemaVersion = "1",
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "A-01",
                        MappingIdentityChecksum = mappingChecksum,
                        Mode = "forward",
                        ProxyType = mapping.ProxyTypeName,
                        ProxyAssembly = mapping.ProxyAssemblyName,
                        TargetType = mapping.TargetTypeName,
                        TargetAssembly = mapping.TargetAssemblyName,
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var manifest = new DuckTypeAotManifest
            {
                SchemaVersion = "1",
                Mappings = new List<DuckTypeAotManifestMapping>
                {
                    new()
                    {
                        Mode = "forward",
                        ScenarioId = "A-01",
                        MappingIdentityChecksum = mappingChecksum,
                        ProxyType = mapping.ProxyTypeName,
                        ProxyAssembly = mapping.ProxyAssemblyName,
                        TargetType = mapping.TargetTypeName,
                        TargetAssembly = mapping.TargetAssemblyName,
                        Source = "mapfile"
                    }
                },
                TargetAssemblies = new List<DuckTypeAotAssemblyFingerprint>
                {
                    new()
                    {
                        Name = targetAssemblyName,
                        Path = targetAssemblyPath,
                        Mvid = Guid.Empty.ToString("D"),
                        Sha256 = new string('0', 64)
                    }
                }
            };
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            var result = DuckTypeAotVerifyCompatProcessor.Process(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
                    reportPath,
                    matrixPath,
                    mappingCatalogPath: null,
                    manifestPath: manifestPath,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(0);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldFailWhenAssemblyFingerprintDriftsInStrictMode()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var manifestPath = Path.Combine(tempDirectory, "ducktyping-aot-manifest.json");
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;
            var mapping = new DuckTypeAotMapping(
                proxyTypeName: "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                proxyAssemblyName: "Datadog.Trace.Tools.Runner.Tests",
                targetTypeName: "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                targetAssemblyName: "Datadog.Trace.Tools.Runner.Tests",
                mode: DuckTypeAotMappingMode.Forward,
                source: DuckTypeAotMappingSource.MapFile,
                scenarioId: "A-01");
            var mappingChecksum = ComputeMappingIdentityChecksumForTest(mapping.Key);

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                SchemaVersion = "1",
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "A-01",
                        MappingIdentityChecksum = mappingChecksum,
                        Mode = "forward",
                        ProxyType = mapping.ProxyTypeName,
                        ProxyAssembly = mapping.ProxyAssemblyName,
                        TargetType = mapping.TargetTypeName,
                        TargetAssembly = mapping.TargetAssemblyName,
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var manifest = new DuckTypeAotManifest
            {
                SchemaVersion = "1",
                Mappings = new List<DuckTypeAotManifestMapping>
                {
                    new()
                    {
                        Mode = "forward",
                        ScenarioId = "A-01",
                        MappingIdentityChecksum = mappingChecksum,
                        ProxyType = mapping.ProxyTypeName,
                        ProxyAssembly = mapping.ProxyAssemblyName,
                        TargetType = mapping.TargetTypeName,
                        TargetAssembly = mapping.TargetAssemblyName,
                        Source = "mapfile"
                    }
                },
                TargetAssemblies = new List<DuckTypeAotAssemblyFingerprint>
                {
                    new()
                    {
                        Name = targetAssemblyName,
                        Path = targetAssemblyPath,
                        Mvid = Guid.Empty.ToString("D"),
                        Sha256 = new string('0', 64)
                    }
                }
            };
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            var result = DuckTypeAotVerifyCompatProcessor.Process(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
                    reportPath,
                    matrixPath,
                    mappingCatalogPath: null,
                    manifestPath: manifestPath,
                    strictAssemblyFingerprintValidation: false,
                    failureMode: DuckTypeAotFailureMode.Strict));
            result.Should().Be(1);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldFailWhenDatadogTraceFingerprintDriftsInStrictMode()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var manifestPath = Path.Combine(tempDirectory, "ducktyping-aot-manifest.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktyping-aot.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktyping-aot.props");
            var registryAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var registryAssemblyName = AssemblyName.GetAssemblyName(registryAssemblyPath).Name;
            var registryAssemblyVersion = AssemblyName.GetAssemblyName(registryAssemblyPath).Version?.ToString() ?? "0.0.0.0";
            var datadogTraceAssemblyPath = typeof(Datadog.Trace.Tracer).Assembly.Location;
            var datadogTraceAssemblyName = AssemblyName.GetAssemblyName(datadogTraceAssemblyPath).Name;
            var mapping = new DuckTypeAotMapping(
                proxyTypeName: "Datadog.Trace.Tools.Runner.Tests.ITestDuckProxy",
                proxyAssemblyName: "Datadog.Trace.Tools.Runner.Tests",
                targetTypeName: "Datadog.Trace.Tools.Runner.Tests.TestDuckTarget",
                targetAssemblyName: "Datadog.Trace.Tools.Runner.Tests",
                mode: DuckTypeAotMappingMode.Forward,
                source: DuckTypeAotMappingSource.MapFile,
                scenarioId: "A-01");
            var mappingChecksum = ComputeMappingIdentityChecksumForTest(mapping.Key);

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                SchemaVersion = "1",
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "A-01",
                        MappingIdentityChecksum = mappingChecksum,
                        Mode = "forward",
                        ProxyType = mapping.ProxyTypeName,
                        ProxyAssembly = mapping.ProxyAssemblyName,
                        TargetType = mapping.TargetTypeName,
                        TargetAssembly = mapping.TargetAssemblyName,
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var trimmerDescriptor = "<linker>" + Environment.NewLine +
                                    $"  <assembly fullname=\"{registryAssemblyName}\">" + Environment.NewLine +
                                    "    <type fullname=\"Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap\" preserve=\"all\" />" + Environment.NewLine +
                                    "  </assembly>" + Environment.NewLine +
                                    $"  <assembly fullname=\"{mapping.ProxyAssemblyName}\">" + Environment.NewLine +
                                    $"    <type fullname=\"{mapping.ProxyTypeName}\" preserve=\"all\" />" + Environment.NewLine +
                                    "  </assembly>" + Environment.NewLine +
                                    $"  <assembly fullname=\"{mapping.TargetAssemblyName}\">" + Environment.NewLine +
                                    $"    <type fullname=\"{mapping.TargetTypeName}\" preserve=\"all\" />" + Environment.NewLine +
                                    "  </assembly>" + Environment.NewLine +
                                    "</linker>";
            File.WriteAllText(trimmerDescriptorPath, trimmerDescriptor);
            File.WriteAllText(propsPath, "<Project />");

            var manifest = new DuckTypeAotManifest
            {
                SchemaVersion = "1",
                RegistryAssembly = registryAssemblyPath,
                RegistryAssemblyName = registryAssemblyName,
                RegistryAssemblyVersion = registryAssemblyVersion,
                RegistryBootstrapType = "Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap",
                RegistryAssemblySha256 = ComputeSha256ForTest(registryAssemblyPath),
                TrimmerDescriptorPath = trimmerDescriptorPath,
                TrimmerDescriptorSha256 = ComputeSha256ForTest(trimmerDescriptorPath),
                PropsPath = propsPath,
                PropsSha256 = ComputeSha256ForTest(propsPath),
                Mappings = new List<DuckTypeAotManifestMapping>
                {
                    new()
                    {
                        Mode = "forward",
                        ScenarioId = "A-01",
                        MappingIdentityChecksum = mappingChecksum,
                        ProxyType = mapping.ProxyTypeName,
                        ProxyAssembly = mapping.ProxyAssemblyName,
                        TargetType = mapping.TargetTypeName,
                        TargetAssembly = mapping.TargetAssemblyName,
                        Source = "mapfile"
                    }
                },
                DatadogTraceAssembly = new DuckTypeAotAssemblyFingerprint
                {
                    Name = datadogTraceAssemblyName,
                    Path = datadogTraceAssemblyPath,
                    Mvid = Guid.Empty.ToString("D"),
                    Sha256 = new string('0', 64)
                }
            };
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            var (result, output) = ProcessAndCapture(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
                    reportPath,
                    matrixPath,
                    mappingCatalogPath: null,
                    manifestPath: manifestPath,
                    strictAssemblyFingerprintValidation: false,
                    failureMode: DuckTypeAotFailureMode.Strict));
            result.Should().Be(1);
            output.Should().Contain("Manifest Datadog.Trace assembly MVID mismatch");
            output.Should().Contain("Manifest Datadog.Trace assembly sha256 mismatch");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void VerifyCompatProcessorShouldFailWhenTrimmerDescriptorDoesNotContainCompatibleRoots()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var reportPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.md");
            var matrixPath = Path.Combine(tempDirectory, "ducktyping-aot-compat.json");
            var manifestPath = Path.Combine(tempDirectory, "ducktyping-aot-manifest.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktyping-aot.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktyping-aot.props");
            var registryAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var registryAssemblyName = AssemblyName.GetAssemblyName(registryAssemblyPath).Name;
            var registryAssemblyVersion = AssemblyName.GetAssemblyName(registryAssemblyPath).Version?.ToString() ?? "0.0.0.0";

            var mapping = new DuckTypeAotMapping(
                proxyTypeName: typeof(ITestDuckProxy).FullName!,
                proxyAssemblyName: typeof(DuckTypeAotProcessorsTests).Assembly.GetName().Name!,
                targetTypeName: typeof(TestDuckTarget).FullName!,
                targetAssemblyName: typeof(TestDuckTarget).Assembly.GetName().Name!,
                mode: DuckTypeAotMappingMode.Forward,
                source: DuckTypeAotMappingSource.MapFile,
                scenarioId: "A-01");
            var mappingChecksum = ComputeMappingIdentityChecksumForTest(mapping.Key);

            File.WriteAllText(reportPath, "# Compatibility Report");
            var matrix = new DuckTypeAotCompatibilityMatrix
            {
                SchemaVersion = "1",
                Mappings = new List<DuckTypeAotCompatibilityMapping>
                {
                    new()
                    {
                        Id = "A-01",
                        MappingIdentityChecksum = mappingChecksum,
                        Mode = "forward",
                        ProxyType = mapping.ProxyTypeName,
                        ProxyAssembly = mapping.ProxyAssemblyName,
                        TargetType = mapping.TargetTypeName,
                        TargetAssembly = mapping.TargetAssemblyName,
                        Status = DuckTypeAotCompatibilityStatuses.Compatible
                    }
                }
            };
            File.WriteAllText(matrixPath, JsonConvert.SerializeObject(matrix));

            var trimmerDescriptor = "<linker>" + Environment.NewLine +
                                    $"  <assembly fullname=\"{registryAssemblyName}\">" + Environment.NewLine +
                                    "    <type fullname=\"Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap\" preserve=\"all\" />" + Environment.NewLine +
                                    "  </assembly>" + Environment.NewLine +
                                    "</linker>";
            File.WriteAllText(trimmerDescriptorPath, trimmerDescriptor);
            File.WriteAllText(propsPath, "<Project />");

            var manifest = new DuckTypeAotManifest
            {
                SchemaVersion = "1",
                RegistryAssembly = registryAssemblyPath,
                RegistryAssemblyName = registryAssemblyName,
                RegistryAssemblyVersion = registryAssemblyVersion,
                RegistryBootstrapType = "Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap",
                RegistryAssemblySha256 = ComputeSha256ForTest(registryAssemblyPath),
                TrimmerDescriptorPath = trimmerDescriptorPath,
                TrimmerDescriptorSha256 = ComputeSha256ForTest(trimmerDescriptorPath),
                PropsPath = propsPath,
                PropsSha256 = ComputeSha256ForTest(propsPath),
                Mappings = new List<DuckTypeAotManifestMapping>
                {
                    new()
                    {
                        Mode = "forward",
                        ScenarioId = "A-01",
                        MappingIdentityChecksum = mappingChecksum,
                        ProxyType = mapping.ProxyTypeName,
                        ProxyAssembly = mapping.ProxyAssemblyName,
                        TargetType = mapping.TargetTypeName,
                        TargetAssembly = mapping.TargetAssemblyName,
                        Source = "mapfile"
                    }
                }
            };
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            var result = DuckTypeAotVerifyCompatProcessor.Process(
                DuckTypeAotVerifyCompatOptions.CreateLegacyContract(
                    reportPath,
                    matrixPath,
                    mappingCatalogPath: null,
                    manifestPath: manifestPath,
                    strictAssemblyFingerprintValidation: false));
            result.Should().Be(1);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static string ComputeMappingIdentityChecksumForTest(string key)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var hashByte in hash)
        {
            _ = builder.Append(hashByte.ToString("x2"));
        }

        return builder.ToString();
    }

    private static string ComputeSha256ForTest(string path)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = sha256.ComputeHash(stream);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var hashByte in hash)
        {
            _ = builder.Append(hashByte.ToString("x2"));
        }

        return builder.ToString();
    }

    private static string GetDuckTypingAotCompatibilityFilePath(string fileName, [CallerFilePath] string sourceFilePath = "")
    {
        var testsDirectory = Path.GetDirectoryName(sourceFilePath);
        testsDirectory.Should().NotBeNullOrWhiteSpace();

        return Path.GetFullPath(
            Path.Combine(
                testsDirectory!,
                "..",
                "Datadog.Trace.DuckTyping.Tests",
                "AotCompatibility",
                fileName));
    }

    private static bool IsScenarioTrackedByInventoryForTest(string scenarioId, IReadOnlyCollection<string> inventoryScenarioIds)
    {
        foreach (var inventoryScenarioId in inventoryScenarioIds)
        {
            if (IsScenarioWildcardForTest(inventoryScenarioId))
            {
                var wildcardPrefix = inventoryScenarioId.Substring(0, inventoryScenarioId.Length - 1);
                if (scenarioId.StartsWith(wildcardPrefix, StringComparison.Ordinal))
                {
                    return true;
                }

                continue;
            }

            if (string.Equals(scenarioId, inventoryScenarioId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsScenarioCoveredByCatalogForTest(string inventoryScenarioId, IReadOnlyCollection<string> catalogScenarioIds)
    {
        if (!IsScenarioWildcardForTest(inventoryScenarioId))
        {
            return catalogScenarioIds.Contains(inventoryScenarioId);
        }

        var wildcardPrefix = inventoryScenarioId.Substring(0, inventoryScenarioId.Length - 1);
        foreach (var scenarioId in catalogScenarioIds)
        {
            if (scenarioId.StartsWith(wildcardPrefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsScenarioWildcardForTest(string scenarioId)
        => scenarioId.EndsWith("*", StringComparison.Ordinal);

    private static (int Result, string Output) ProcessAndCapture(DuckTypeAotVerifyCompatOptions options)
    {
        using var capture = ConsoleOutputCapture.Redirect();
        var result = DuckTypeAotVerifyCompatProcessor.Process(options);
        return (result, capture.Output);
    }

    private sealed class DuckTypeAotExpectedOutcomesContract
    {
        [JsonProperty("defaultStatus")]
        public string? DefaultStatus { get; set; }

        [JsonProperty("expectedOutcomes")]
        public List<DuckTypeAotExpectedOutcomeContract> ExpectedOutcomes { get; set; } = [];
    }

    private sealed class DuckTypeAotKnownLimitationsContract
    {
        [JsonProperty("knownLimitations")]
        public List<DuckTypeAotExpectedOutcomeContract> KnownLimitations { get; set; } = [];
    }

    private sealed class DuckTypeAotExpectedOutcomeContract
    {
        [JsonProperty("scenarioId")]
        public string? ScenarioId { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; }
    }

    private sealed class ExpectedOutcomesTestDocument
    {
        public List<ExpectedOutcomeTestEntry>? ExpectedOutcomes { get; set; }
    }

    private sealed class ExpectedOutcomeTestEntry
    {
        public string? ScenarioId { get; set; }

        public string? Status { get; set; }
    }
}
