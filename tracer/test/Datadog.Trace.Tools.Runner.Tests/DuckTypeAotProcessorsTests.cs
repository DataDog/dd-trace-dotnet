// <copyright file="DuckTypeAotProcessorsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
#if NETCOREAPP2_1
using AssemblyLoadContext = Datadog.Trace.Tools.Runner.Tests.NetCore21AssemblyLoadContext;
#else
using System.Runtime.Loader;
#endif
using System.Security.Cryptography;
using System.Text;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using FluentAssertions;
using Spectre.Console;
using Xunit;

#pragma warning disable SA1201, SA1202 // Elements should appear in the correct order

namespace Datadog.Trace.Tools.Runner.Tests;

[Collection(nameof(DuckTypeAotProcessorConsoleCollection))]
public class DuckTypeAotProcessorsTests
{
    private const string BibleMappingCatalogFileName = "ducktype-aot-bible-mapping-catalog.json";
    private const string BibleScenarioInventoryFileName = "ducktype-aot-bible-scenario-inventory.json";
    private const string BibleExpectedOutcomesFileName = "ducktype-aot-bible-expected-outcomes.json";
    private const string BibleKnownLimitationsFileName = "ducktype-aot-bible-known-limitations.json";
    private const string DuplicateAssignableBaseTargetTypeName = "Duplicate.Targets.SharedBaseTarget";
    private const string StrongNameKeyEnvironmentVariable = "DD_TRACE_DUCKTYPE_AOT_STRONG_NAME_KEY_FILE";

    public DuckTypeAotProcessorsTests()
    {
        DuckType.ResetRuntimeModeForTests();
        DuckTypeAotEngine.ResetForTests();
    }

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

    [Theory]
    [InlineData("System.Collections.Generic.List`1", true)]
    [InlineData("System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]]", false)]
    [InlineData("Example.Outer`1+Inner`1[[System.String, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]]", false)]
    [InlineData("Example.Outer`1+Inner`1[[System.String, System.Private.CoreLib]]", true)]
    [InlineData("Example.Container`1[!0]", true)]
    public void OpenGenericNameDetectionShouldHandleNestedClosedGenericTypeNames(string typeName, bool expectedIsOpen)
    {
        DuckTypeAotNameHelpers.IsOpenGenericTypeName(typeName).Should().Be(expectedIsOpen);
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
    public void GenerateProcessorShouldEmitExpectedArtifactsAndBootstrap()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };

            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var manifestPath = $"{outputPath}.manifest.json";
            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var compatibilityReportPath = $"{outputPath}.compat.md";

            File.Exists(outputPath).Should().BeTrue();
            File.Exists(manifestPath).Should().BeTrue();
            File.Exists(compatibilityMatrixPath).Should().BeTrue();
            File.Exists(compatibilityReportPath).Should().BeTrue();
            File.Exists(trimmerDescriptorPath).Should().BeTrue();
            File.Exists(propsPath).Should().BeTrue();

            var manifest = JsonConvert.DeserializeObject<DuckTypeAotManifest>(File.ReadAllText(manifestPath));
            manifest.Should().NotBeNull();
            manifest!.SchemaVersion.Should().Be("1");
            manifest.RegistryAssemblyName.Should().Be("Datadog.Trace.DuckType.AotRegistry");
            manifest.Mappings.Should().HaveCount(1);
            manifest.Mappings[0].MappingIdentityChecksum.Should().NotBeNullOrWhiteSpace();
            manifest.ProxyAssemblies.Should().ContainSingle(assembly => assembly.Name == proxyAssemblyName);
            manifest.TargetAssemblies.Should().ContainSingle(assembly => assembly.Name == targetAssemblyName);

            var compatibilityMatrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            compatibilityMatrix.Should().NotBeNull();
            compatibilityMatrix!.TotalMappings.Should().Be(1);
            compatibilityMatrix.Mappings.Should().ContainSingle(mapping => string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));
            var compatibilityMapping = compatibilityMatrix.Mappings.Single();
            compatibilityMapping.MappingIdentityChecksum.Should().NotBeNullOrWhiteSpace();
            compatibilityMapping.GeneratedProxyAssembly.Should().Be("Datadog.Trace.DuckType.AotRegistry");
            compatibilityMapping.GeneratedProxyType.Should().StartWith("Datadog.Trace.DuckTyping.Generated.Proxies.DuckTypeProxy_");

            var propsContent = File.ReadAllText(propsPath);
            propsContent.Should().Contain("<Reference Include=\"Datadog.Trace.DuckType.AotRegistry\">");
            propsContent.Should().Contain("ducktype-aot.linker.xml");

            var trimmerDescriptorContent = File.ReadAllText(trimmerDescriptorPath);
            trimmerDescriptorContent.Should().Contain("<assembly fullname=\"Datadog.Trace.DuckType.AotRegistry\">");
            trimmerDescriptorContent.Should().Contain($"<assembly fullname=\"{proxyAssemblyName}\">");
            trimmerDescriptorContent.Should().Contain($"<assembly fullname=\"{targetAssemblyName}\">");
            trimmerDescriptorContent.Should().Contain(typeof(ITestDuckProxy).FullName!.Replace('+', '/'));
            trimmerDescriptorContent.Should().Contain(typeof(TestDuckTarget).FullName!.Replace('+', '/'));
            trimmerDescriptorContent.Should().Contain("Datadog.Trace.DuckTyping.Generated.Proxies.DuckTypeProxy_");

            using var generatedModule = ModuleDefMD.Load(outputPath);
            var bootstrapType = generatedModule.Find("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap", isReflectionName: false);
            bootstrapType.Should().NotBeNull();
            var generatedProxyType = generatedModule.Types.SingleOrDefault(type =>
                string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
            generatedProxyType.Should().NotBeNull();
            generatedProxyType!.IsValueType.Should().BeTrue();
            generatedProxyType.BaseType!.FullName.Should().Be("System.ValueType");
            generatedProxyType!.FindMethod("Echo").Should().NotBeNull();
            generatedProxyType.Interfaces.Any(interfaceImpl =>
                string.Equals(interfaceImpl.Interface.FullName, "Datadog.Trace.DuckTyping.IDuckType", StringComparison.Ordinal)).Should().BeTrue();
            generatedProxyType.FindMethod("get_Instance").Should().NotBeNull();
            generatedProxyType.FindMethod("get_Type").Should().NotBeNull();
            generatedProxyType.FindMethod("GetInternalDuckTypedInstance").Should().NotBeNull();
            var generatedCtor = generatedProxyType.FindMethod(".ctor");
            generatedCtor.Should().NotBeNull();
            generatedCtor!.MethodSig.Params.Should().ContainSingle();
            generatedCtor.MethodSig.Params[0].FullName.Should().Be(typeof(TestDuckTarget).FullName);

            var initializeMethod = bootstrapType!.FindMethod("Initialize");
            initializeMethod.Should().NotBeNull();
            initializeMethod!.Body.Should().NotBeNull();
            var bootstrapInstructions = bootstrapType.Methods
                .Where(method => method.Body is not null)
                .SelectMany(method => method.Body!.Instructions)
                .ToList();
            var enableAotModeInstruction = bootstrapInstructions.SingleOrDefault(
                instruction =>
                    instruction.OpCode == OpCodes.Call &&
                    instruction.Operand is IMethod method &&
                    string.Equals(method.Name, "EnableAotMode", StringComparison.Ordinal));
            enableAotModeInstruction.Should().NotBeNull();

            var validateAotRegistryContractInstruction = bootstrapInstructions.SingleOrDefault(
                instruction =>
                    instruction.OpCode == OpCodes.Call &&
                    instruction.Operand is IMethod method &&
                    string.Equals(method.Name, "ValidateAotRegistryContract", StringComparison.Ordinal));
            validateAotRegistryContractInstruction.Should().NotBeNull();

            var registerAotProxyInstruction = bootstrapInstructions.SingleOrDefault(
                instruction =>
                    instruction.OpCode == OpCodes.Call &&
                    instruction.Operand is IMethod method &&
                    string.Equals(method.Name, "RegisterAotProxy", StringComparison.Ordinal));
            registerAotProxyInstruction.Should().NotBeNull();
            ((IMethod)registerAotProxyInstruction!.Operand).MethodSig.Params.Last().FullName.Should().StartWith("System.Func`2", "generated success registrations should pass direct delegates instead of RuntimeMethodHandle values");
            AssertRegistrationLoadsGeneratedProxyType(bootstrapType, "RegisterAotProxy", generatedProxyType);
            AssertBootstrapDoesNotUseReflectionForGeneratedRegistrations(bootstrapInstructions);
            AssertDirectDelegateRegistrationCalls(bootstrapType, "RegisterAotProxy", "ActivateProxy_", "System.Func`2");

            var initializesFuncDelegate = bootstrapInstructions.Any(
                instruction =>
                    instruction.OpCode == OpCodes.Newobj &&
                    instruction.Operand is IMethod method &&
                    method.DeclaringType.FullName.StartsWith("System.Func`2", StringComparison.Ordinal));
            initializesFuncDelegate.Should().BeTrue();

            var loadsActivatorMethodHandle = bootstrapInstructions.Any(
                instruction =>
                    instruction.OpCode == OpCodes.Ldtoken &&
                    instruction.Operand is IMethod method &&
                    method.Name.StartsWith("ActivateProxy_", StringComparison.Ordinal));
            loadsActivatorMethodHandle.Should().BeFalse("generated success registrations should not resolve activators from RuntimeMethodHandle");

            var loadsActivatorFunctionPointer = bootstrapInstructions.Any(
                instruction =>
                    instruction.OpCode == OpCodes.Ldftn &&
                    instruction.Operand is IMethod method &&
                    method.Name.StartsWith("ActivateProxy_", StringComparison.Ordinal));
            loadsActivatorFunctionPointer.Should().BeTrue();

            var activatorMethod = bootstrapType.Methods.Single(method =>
                method.Name.StartsWith("ActivateProxy_", StringComparison.Ordinal));
            activatorMethod.MethodSig.Params.Should().ContainSingle();
            activatorMethod.MethodSig.Params[0].FullName.Should().Be("System.Object");
            activatorMethod.MethodSig.RetType.FullName.Should().Be("System.Object");
            activatorMethod.Body.Should().NotBeNull();
            activatorMethod.Body!.Instructions.Should().Contain(instruction => instruction.OpCode == OpCodes.Castclass || instruction.OpCode == OpCodes.Unbox_Any);

            var moduleInitializer = generatedModule.GlobalType.FindMethod(".cctor");
            moduleInitializer.Should().NotBeNull();
            moduleInitializer!.Body.Should().NotBeNull();
            var initializeInstruction = moduleInitializer.Body!.Instructions.SingleOrDefault(
                instruction =>
                    instruction.OpCode == OpCodes.Call &&
                    instruction.Operand is IMethod method &&
                    string.Equals(method.Name, "Initialize", StringComparison.Ordinal));
            initializeInstruction.Should().NotBeNull();
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldEmitAssignableAliasRegistrationsForDerivedRuntimeTargets()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            DuckType.ResetRuntimeModeForTests();

            var assemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath).Name;
            assemblyName.Should().NotBeNullOrWhiteSpace();

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Alias.Runtime.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-alias-map.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-alias.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-alias.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IAliasForwardProxy).FullName,
                        proxyAssembly = assemblyName,
                        targetType = typeof(AliasForwardBaseTarget).FullName,
                        targetAssembly = assemblyName
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IAliasForwardProxy).FullName,
                        proxyAssembly = assemblyName,
                        targetType = typeof(AliasForwardDerivedTarget).FullName,
                        targetAssembly = assemblyName
                    }
                }
            };

            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { assemblyPath },
                targetAssemblies: new[] { assemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Alias.Runtime",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var manifestPath = $"{outputPath}.manifest.json";
            var manifest = JsonConvert.DeserializeObject<DuckTypeAotManifest>(File.ReadAllText(manifestPath));
            manifest.Should().NotBeNull();
            manifest!.AliasRegistrations.Should().BeGreaterThan(0);
            manifest.TotalRuntimeRegistrations.Should().BeGreaterThan(manifest.Mappings.Count);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-Alias-Runtime", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                DuckType.ResetRuntimeModeForTests();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);
                DuckTypeAotEngine.DirectObjectActivatorHandleCount.Should().Be(0);
                DuckTypeAotEngine.AdaptedTypedActivatorHandleCount.Should().Be(0);

                var proxy = DuckType.Create<IAliasForwardProxy>(new AliasForwardDerivedTarget("alias"));
                proxy.Should().NotBeNull();
                proxy!.Value.Should().Be("alias");

                var aliasProxy = DuckType.Create<IAliasForwardProxy>(new AliasForwardOtherDerivedTarget("alias-other"));
                aliasProxy.Should().NotBeNull();
                aliasProxy!.Value.Should().Be("alias-other");

                var nonGenericTarget = new AliasForwardDerivedTarget("non-generic");
                var nonGenericProxy = DuckType.Create(typeof(IAliasForwardProxy), nonGenericTarget);
                nonGenericProxy.Should().BeAssignableTo<IAliasForwardProxy>();

                var createTypeResult = DuckType.GetOrCreateProxyType(typeof(IAliasForwardProxy), typeof(AliasForwardDerivedTarget));
                createTypeResult.UsesDynamicInvokeFallback.Should().BeFalse();
                var activator = GetCreateTypeResultField<Delegate>(createTypeResult, "_activator");
                var untypedActivator = GetCreateTypeResultField<Func<object?, object?>>(createTypeResult, "_untypedActivator");
                activator.Should().NotBeNull();
                activator!.GetType().Should().Be(typeof(Func<object?, object?>));
                activator.Method.Name.Should().StartWith("ActivateProxy_", "generated registries should register object-input, object-output activators directly");
                untypedActivator.Should().NotBeNull();
                untypedActivator!.Method.Name.Should().Be(activator.Method.Name, "non-generic Create should use the generated activator directly instead of a DynamicInvoke wrapper");
                untypedActivator.Method.DeclaringType.Should().Be(activator.Method.DeclaringType);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            DuckType.ResetRuntimeModeForTests();
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldNotEmitAssignableAliasWhenDerivedBindingDiffersFromCanonicalBinding()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            DuckType.ResetRuntimeModeForTests();

            var assemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath).Name;
            assemblyName.Should().NotBeNullOrWhiteSpace();

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Alias.UnsafeDerived.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-alias-unsafe-derived-map.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-alias-unsafe-derived.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-alias-unsafe-derived.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IAliasShadowProxy).FullName,
                        proxyAssembly = assemblyName,
                        targetType = typeof(AliasShadowBaseTarget).FullName,
                        targetAssembly = assemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { assemblyPath },
                targetAssemblies: new[] { assemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Alias.UnsafeDerived",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var manifestPath = $"{outputPath}.manifest.json";
            var manifest = JsonConvert.DeserializeObject<DuckTypeAotManifest>(File.ReadAllText(manifestPath));
            manifest.Should().NotBeNull();
            manifest!.AliasRegistrations.Should().Be(0, "a derived type that shadows a bound member must require an explicit mapping");
            manifest.TotalRuntimeRegistrations.Should().Be(1);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-Alias-UnsafeDerived", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                DuckType.ResetRuntimeModeForTests();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var baseProxy = DuckType.Create<IAliasShadowProxy>(new AliasShadowBaseTarget("value"));
                baseProxy.Should().NotBeNull();
                baseProxy!.Value.Should().Be("base:value");

                DuckType.CanCreate<IAliasShadowProxy>(new AliasShadowDerivedTarget("value"))
                        .Should()
                        .BeFalse("unsafe assignable aliases should not silently bind derived targets through the base mapping");
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
            DuckType.ResetRuntimeModeForTests();
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldPreferExplicitCanonicalDerivedMappingOverAssignableAlias()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            DuckType.ResetRuntimeModeForTests();

            var assemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath).Name;
            assemblyName.Should().NotBeNullOrWhiteSpace();

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Alias.CanonicalDerived.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-alias-canonical-derived-map.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-alias-canonical-derived.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-alias-canonical-derived.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IAliasShadowProxy).FullName,
                        proxyAssembly = assemblyName,
                        targetType = typeof(AliasShadowBaseTarget).FullName,
                        targetAssembly = assemblyName
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IAliasShadowProxy).FullName,
                        proxyAssembly = assemblyName,
                        targetType = typeof(AliasShadowDerivedTarget).FullName,
                        targetAssembly = assemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { assemblyPath },
                targetAssemblies: new[] { assemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Alias.CanonicalDerived",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-Alias-CanonicalDerived", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                DuckType.ResetRuntimeModeForTests();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var baseProxy = DuckType.Create<IAliasShadowProxy>(new AliasShadowBaseTarget("value"));
                baseProxy.Should().NotBeNull();
                baseProxy!.Value.Should().Be("base:value");

                var derivedProxy = DuckType.Create<IAliasShadowProxy>(new AliasShadowDerivedTarget("value"));
                derivedProxy.Should().NotBeNull();
                derivedProxy!.Value.Should().Be("derived:value", "an explicit canonical derived mapping must not be shadowed by the base mapping assignable alias");
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
    public void GenerateProcessorShouldKeepAssignableAliasRegistrationsScopedToTargetAssembly()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var firstTargetAssemblyName = "DuckTypeAotDuplicateTargets.First";
            var secondTargetAssemblyName = "DuckTypeAotDuplicateTargets.Second";
            var firstTargetAssemblyPath = CreateDuplicateAssignableTargetAssembly(tempDirectory, firstTargetAssemblyName);
            var secondTargetAssemblyPath = CreateDuplicateAssignableTargetAssembly(tempDirectory, secondTargetAssemblyName);
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            proxyAssemblyName.Should().NotBeNullOrWhiteSpace();

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.DuplicateAliasScope.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-duplicate-alias-map.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-duplicate-alias.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-duplicate-alias.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IAliasForwardProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = DuplicateAssignableBaseTargetTypeName,
                        targetAssembly = firstTargetAssemblyName
                    }
                }
            };

            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { firstTargetAssemblyPath, secondTargetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.DuplicateAliasScope",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var manifestPath = $"{outputPath}.manifest.json";
            var manifest = JsonConvert.DeserializeObject<DuckTypeAotManifest>(File.ReadAllText(manifestPath));
            manifest.Should().NotBeNull();
            manifest!.TotalRuntimeRegistrations.Should().Be(2, "only the canonical mapping and the derived target from the mapped assembly should be registered");
            manifest.AliasRegistrations.Should().Be(1, "assignable aliases must not cross assemblies that happen to contain the same full type name");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldUseResolvedDatadogTraceAssemblyForContractMetadata()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var datadogTraceNetStandardPath = GetDatadogTraceNetStandardAssemblyPath();
            File.Exists(datadogTraceNetStandardPath).Should().BeTrue("expected Datadog.Trace netstandard build artifact to exist for contract metadata selection tests");

            var expectedDatadogTraceVersion = AssemblyName.GetAssemblyName(datadogTraceNetStandardPath).Version?.ToString() ?? "0.0.0.0";
            var expectedDatadogTraceMvid = ResolveAssemblyMvidForTest(datadogTraceNetStandardPath);
            var runtimeDatadogTraceMvid = ResolveAssemblyMvidForTest(typeof(Datadog.Trace.Tracer).Assembly.Location);
            expectedDatadogTraceMvid.Should().NotBe(
                runtimeDatadogTraceMvid,
                "this test must use a Datadog.Trace assembly artifact that differs from the runner runtime assembly");

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ContractMetadataSelection.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-contract-metadata-selection.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-contract-metadata-selection.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-contract-metadata-selection.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath, datadogTraceNetStandardPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ContractMetadataSelection",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            using var generatedModule = ModuleDefMD.Load(outputPath);
            var bootstrapType = generatedModule.Find("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap", isReflectionName: false);
            bootstrapType.Should().NotBeNull();
            var initializeMethod = bootstrapType!.FindMethod("Initialize");
            initializeMethod.Should().NotBeNull();
            initializeMethod!.Body.Should().NotBeNull();

            var instructions = initializeMethod.Body!.Instructions.ToList();
            var validateCallIndex = instructions.FindIndex(
                instruction =>
                    instruction.OpCode == OpCodes.Call &&
                    instruction.Operand is IMethod method &&
                    string.Equals(method.Name, "ValidateAotRegistryContract", StringComparison.Ordinal));
            validateCallIndex.Should().BeGreaterThan(0);

            var contractArguments = instructions
                                   .Take(validateCallIndex)
                                   .Reverse()
                                   .Where(instruction => instruction.OpCode == OpCodes.Ldstr && instruction.Operand is string)
                                   .Take(5)
                                   .Select(instruction => (string)instruction.Operand!)
                                   .Reverse()
                                   .ToList();

            contractArguments.Should().HaveCount(5);
            contractArguments[1].Should().Be(expectedDatadogTraceVersion);
            contractArguments[2].Should().Be(expectedDatadogTraceMvid);
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

    [Fact]
    public void GenerateProcessorShouldAssignDeterministicMappingIdWhenMapFileDoesNotProvideScenarioIds()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Scenario.MapFile.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-scenario.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-scenario.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-scenario.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Scenario.MapFile",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var compatibilityMatrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            compatibilityMatrix.Should().NotBeNull();
            compatibilityMatrix!.Mappings.Should().ContainSingle();
            compatibilityMatrix.Mappings[0].Id.Should().Be("MAP-0001");
            compatibilityMatrix.Mappings[0].MappingIdentityChecksum.Should().NotBeNullOrWhiteSpace();

            var manifestPath = $"{outputPath}.manifest.json";
            var manifest = JsonConvert.DeserializeObject<DuckTypeAotManifest>(File.ReadAllText(manifestPath));
            manifest.Should().NotBeNull();
            manifest!.Mappings.Should().ContainSingle();
            manifest.Mappings[0].ScenarioId.Should().BeNull();
            manifest.Mappings[0].MappingIdentityChecksum.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldFailWhenLegacyConstructorOmitsMapFile()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.RequireCatalog.Missing.dll");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-require-catalog-missing.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-require-catalog-missing.props");

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: null,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.RequireCatalog.Missing",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath,
                requireMappingCatalog: true);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(1);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldFailWhenLegacyConstructorProvidesCatalogButOmitsMapFile()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.RequireCatalog.Empty.dll");
            var mappingCatalogPath = Path.Combine(tempDirectory, "ducktype-aot-mapping-catalog-empty.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-require-catalog-empty.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-require-catalog-empty.props");

            var mappingCatalogDocument = new
            {
                requiredMappings = Array.Empty<object>()
            };
            File.WriteAllText(mappingCatalogPath, JsonConvert.SerializeObject(mappingCatalogDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: null,
                mappingCatalog: mappingCatalogPath,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.RequireCatalog.Empty",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath,
                requireMappingCatalog: true);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(1);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldIgnoreCatalogScenarioIdWhenMapFileIsCanonicalSourceOfTruth()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Scenario.Catalog.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-scenario-catalog.json");
            var mappingCatalogPath = Path.Combine(tempDirectory, "ducktype-aot-mapping-catalog-scenario.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-scenario-catalog.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-scenario-catalog.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var mappingCatalogDocument = new
            {
                requiredMappings = new[]
                {
                    new
                    {
                        scenarioId = "A-02",
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mappingCatalogPath, JsonConvert.SerializeObject(mappingCatalogDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: mappingCatalogPath,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Scenario.Catalog",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath,
                requireMappingCatalog: true);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var compatibilityMatrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            compatibilityMatrix.Should().NotBeNull();
            compatibilityMatrix!.Mappings.Should().ContainSingle();
            compatibilityMatrix.Mappings[0].Id.Should().Be("MAP-0001");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldIgnoreDeprecatedExpectCanCreateFieldInMappingCatalog()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(ParityVoidMismatchTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.DeprecatedExpectCanCreate.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-deprecated-expect-can-create.json");
            var mappingCatalogPath = Path.Combine(tempDirectory, "ducktype-aot-mapping-catalog-deprecated-expect-can-create.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-deprecated-expect-can-create.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-deprecated-expect-can-create.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IParityVoidMismatchProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(ParityVoidMismatchTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var mappingCatalogDocument = new
            {
                requiredMappings = new[]
                {
                    new
                    {
                        scenarioId = "PX-01",
                        mode = "forward",
                        proxyType = typeof(IParityVoidMismatchProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(ParityVoidMismatchTarget).FullName,
                        targetAssembly = targetAssemblyName,
                        expectCanCreate = false
                    }
                }
            };
            File.WriteAllText(mappingCatalogPath, JsonConvert.SerializeObject(mappingCatalogDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: mappingCatalogPath,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.DeprecatedExpectCanCreate",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath,
                requireMappingCatalog: true);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldFailWhenMapFileContainsDeprecatedExpectCanCreate()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.DeprecatedMapExpectCanCreate.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-deprecated-expect-can-create.json");
            var mappingCatalogPath = Path.Combine(tempDirectory, "ducktype-aot-mapping-catalog-valid.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-deprecated-map-expect-can-create.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-deprecated-map-expect-can-create.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName,
                        expectCanCreate = false
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var mappingCatalogDocument = new
            {
                requiredMappings = new[]
                {
                    new
                    {
                        scenarioId = "PX-02",
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mappingCatalogPath, JsonConvert.SerializeObject(mappingCatalogDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: mappingCatalogPath,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.DeprecatedMapExpectCanCreate",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath,
                requireMappingCatalog: true);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(1);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldAssignUniqueDeterministicMappingIdsForMultipleMappings()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Scenario.Grouped.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-scenario-duplicate.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-scenario-duplicate.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-scenario-duplicate.props");

            var mapDocument = new
            {
                mappings = new object[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckNamedMethodProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckNamedMethodTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Scenario.Grouped",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);
            File.Exists(outputPath).Should().BeTrue();

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var compatibilityMatrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            compatibilityMatrix.Should().NotBeNull();
            compatibilityMatrix!.Mappings.Should().HaveCount(2);
            compatibilityMatrix.Mappings.Select(mapping => mapping.Id).Should().BeEquivalentTo(new[] { "MAP-0001", "MAP-0002" });
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldIncludeGenericInstantiationsInManifestAndTrimmerDescriptor()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;
            var coreAssemblyName = typeof(List<string>).Assembly.GetName().Name!;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.GenericRoots.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-generic-roots.json");
            var genericInstantiationsPath = Path.Combine(tempDirectory, "ducktype-aot-generic-instantiations.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-generic-roots.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-generic-roots.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var listOfStringTypeName = typeof(List<string>).FullName!;
            var dictionaryTypeName = typeof(Dictionary<string, int>).FullName!;
            var genericInstantiationsDocument = new
            {
                instantiations = new object[]
                {
                    new
                    {
                        type = listOfStringTypeName,
                        assembly = coreAssemblyName
                    },
                    $"{dictionaryTypeName}, {coreAssemblyName}"
                }
            };
            File.WriteAllText(genericInstantiationsPath, JsonConvert.SerializeObject(genericInstantiationsDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: genericInstantiationsPath,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.GenericRoots",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var manifestPath = $"{outputPath}.manifest.json";
            var manifest = JsonConvert.DeserializeObject<DuckTypeAotManifest>(File.ReadAllText(manifestPath));
            manifest.Should().NotBeNull();
            manifest!.GenericInstantiations.Should().Contain(entry =>
                string.Equals(entry.Assembly, coreAssemblyName, StringComparison.Ordinal) &&
                string.Equals(entry.Type, listOfStringTypeName, StringComparison.Ordinal));
            manifest.GenericInstantiations.Should().Contain(entry =>
                string.Equals(entry.Assembly, coreAssemblyName, StringComparison.Ordinal) &&
                string.Equals(entry.Type, dictionaryTypeName, StringComparison.Ordinal));

            var trimmerDescriptorContent = File.ReadAllText(trimmerDescriptorPath);
            trimmerDescriptorContent.Should().Contain($"<assembly fullname=\"{coreAssemblyName}\">");
            trimmerDescriptorContent.Should().Contain(listOfStringTypeName);
            trimmerDescriptorContent.Should().Contain(dictionaryTypeName);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldFailForOpenGenericInstantiationRoots()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;
            var coreAssemblyName = typeof(List<string>).Assembly.GetName().Name!;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.GenericRoots.Invalid.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-generic-roots-invalid.json");
            var genericInstantiationsPath = Path.Combine(tempDirectory, "ducktype-aot-generic-instantiations-invalid.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-generic-roots-invalid.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-generic-roots-invalid.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var genericInstantiationsDocument = new
            {
                instantiations = new object[]
                {
                    $"{typeof(List<>).FullName}, {coreAssemblyName}"
                }
            };
            File.WriteAllText(genericInstantiationsPath, JsonConvert.SerializeObject(genericInstantiationsDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: genericInstantiationsPath,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.GenericRoots.Invalid",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(1);
            File.Exists(outputPath).Should().BeFalse();
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportClosedGenericMappingsWhenTargetIsDirectlyAssignable()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(List<int>).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ClosedGeneric.Unsupported.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-closed-generic-unsupported.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-closed-generic-unsupported.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-closed-generic-unsupported.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IReadOnlyCollection<int>).FullName,
                        proxyAssembly = sharedAssemblyName,
                        targetType = typeof(List<int>).FullName,
                        targetAssembly = sharedAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ClosedGeneric.Unsupported",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ClosedGeneric-Assignable", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var instance = new List<int> { 2, 4, 6 };
                var proxy = DuckType.Create(typeof(IReadOnlyCollection<int>), instance);
                proxy.Should().NotBeNull();
                proxy.Should().BeAssignableTo<IReadOnlyCollection<int>>();
                ((IReadOnlyCollection<int>)proxy!).Count.Should().Be(3);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportNestedPrivateClosedGenericMappingsWithCrossAssemblyArguments()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;
            var helperAssemblyPath = typeof(EnvironmentHelper).Assembly.Location;

            var closedArgumentType = typeof(IEnumerable<Tuple<EnvironmentHelper, string>>);
            var closedOuterTargetType = typeof(NestedPrivateClosedGenericTarget<>).MakeGenericType(closedArgumentType);
            var closedInnerTargetType = closedOuterTargetType
                                       .GetProperty("Method", BindingFlags.Instance | BindingFlags.NonPublic)
                                       ?.PropertyType;

            closedInnerTargetType.Should().NotBeNull("the nested private closed generic target type should be discoverable via reflection");

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ClosedGeneric.NestedPrivate.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-closed-generic-nested-private.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-closed-generic-nested-private.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-closed-generic-nested-private.props");

            var mapDocument = new
            {
                mappings = new object[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(INestedPrivateClosedGenericProxy).FullName,
                        proxyAssembly = sharedAssemblyName,
                        targetType = closedOuterTargetType.FullName,
                        targetAssembly = sharedAssemblyName
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(INestedPrivateClosedGenericInnerProxy).FullName,
                        proxyAssembly = sharedAssemblyName,
                        targetType = closedInnerTargetType!.FullName,
                        targetAssembly = sharedAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath, helperAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ClosedGeneric.NestedPrivate",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().OnlyContain(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ClosedGeneric-NestedPrivate", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var instance = Activator.CreateInstance(closedOuterTargetType);
                instance.Should().NotBeNull();

                var proxy = DuckType.Create(typeof(INestedPrivateClosedGenericProxy), instance!);
                proxy.Should().NotBeNull();
                proxy.Should().BeAssignableTo<INestedPrivateClosedGenericProxy>();
                ((INestedPrivateClosedGenericProxy)proxy!).Method.Value.Should().Contain(nameof(EnvironmentHelper));
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportNonGenericDuckTypeTaskContracts()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var traceAssemblyPath = typeof(IDuckType).Assembly.Location;
            var traceAssemblyName = AssemblyName.GetAssemblyName(traceAssemblyPath).Name;
            var runtimeAssemblyPath = typeof(Task).Assembly.Location;
            var runtimeAssemblyName = AssemblyName.GetAssemblyName(runtimeAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.DuckTypeTask.NonGeneric.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-duck-type-task-non-generic.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-duck-type-task-non-generic.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-duck-type-task-non-generic.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IDuckTypeTask).FullName,
                        proxyAssembly = traceAssemblyName,
                        targetType = typeof(Task).FullName,
                        targetAssembly = runtimeAssemblyName
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IDuckTypeAwaiter).FullName,
                        proxyAssembly = traceAssemblyName,
                        targetType = typeof(TaskAwaiter).FullName,
                        targetAssembly = runtimeAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { traceAssemblyPath },
                targetAssemblies: new[] { runtimeAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.DuckTypeTask.NonGeneric",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().OnlyContain(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-DuckTypeTask-NonGeneric", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var completedTask = new Task(static () => { });
                completedTask.RunSynchronously();

                var proxy = DuckType.Create(typeof(IDuckTypeTask), completedTask);
                proxy.Should().BeAssignableTo<IDuckTypeTask>();

                var duckTask = (IDuckTypeTask)proxy!;
                duckTask.IsCompletedSuccessfully.Should().BeTrue();
                duckTask.GetAwaiter().IsCompleted.Should().BeTrue();
#pragma warning disable xUnit1031 // The test intentionally exercises the duck-typed awaiter GetResult contract.
                duckTask.GetAwaiter().GetResult();
#pragma warning restore xUnit1031
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
    public void GenerateProcessorShouldSupportClosedGenericMappingsWhenDuckAdaptationIsRequired()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ClosedGeneric.DuckAdaptation.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-closed-generic-duck-adaptation.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-closed-generic-duck-adaptation.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-closed-generic-duck-adaptation.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IClosedGenericDuckProxy<int>).FullName,
                        proxyAssembly = sharedAssemblyName,
                        targetType = typeof(ClosedGenericDuckTarget<int>).FullName,
                        targetAssembly = sharedAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ClosedGeneric.DuckAdaptation",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ClosedGeneric-DuckAdaptation", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var proxy = DuckType.Create(typeof(IClosedGenericDuckProxy<int>), new ClosedGenericDuckTarget<int>(42));
                proxy.Should().BeAssignableTo<IClosedGenericDuckProxy<int>>();
                ((IClosedGenericDuckProxy<int>)proxy!).Value.Should().Be(42);
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
    public void GenerateProcessorShouldSupportClosedGenericClassMappingsWhenDuckAdaptationIsRequired()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ClosedGeneric.ClassDuckAdaptation.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-closed-generic-class-duck-adaptation.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-closed-generic-class-duck-adaptation.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-closed-generic-class-duck-adaptation.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ClosedGenericClassDuckProxy<int>).FullName,
                        proxyAssembly = sharedAssemblyName,
                        targetType = typeof(ClosedGenericClassDuckTarget<int>).FullName,
                        targetAssembly = sharedAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ClosedGeneric.ClassDuckAdaptation",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ClosedGeneric-ClassDuckAdaptation", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var proxy = DuckType.Create(typeof(ClosedGenericClassDuckProxy<int>), new ClosedGenericClassDuckTarget<int>(42));
                proxy.Should().BeAssignableTo<ClosedGenericClassDuckProxy<int>>();
                ((ClosedGenericClassDuckProxy<int>)proxy!).Value.Should().Be(42);
                ((ClosedGenericClassDuckProxy<int>)proxy).Echo(7).Should().Be(8);
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
    public void GenerateProcessorShouldEmitAssignableAliasRegistrationsForClosedGenericBaseMappings()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            DuckType.ResetRuntimeModeForTests();

            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ClosedGeneric.AssignableAlias.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-closed-generic-assignable-alias.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-closed-generic-assignable-alias.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-closed-generic-assignable-alias.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IClosedGenericAliasProxy<int>).FullName,
                        proxyAssembly = sharedAssemblyName,
                        targetType = typeof(ClosedGenericAliasBase<int>).FullName,
                        targetAssembly = sharedAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ClosedGeneric.AssignableAlias",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var manifestPath = $"{outputPath}.manifest.json";
            var manifest = JsonConvert.DeserializeObject<DuckTypeAotManifest>(File.ReadAllText(manifestPath));
            manifest.Should().NotBeNull();
            manifest!.AliasRegistrations.Should().BeGreaterThan(0);
            manifest.TotalRuntimeRegistrations.Should().BeGreaterThan(manifest.Mappings.Count);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ClosedGeneric-AssignableAlias", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var proxy = DuckType.Create(typeof(IClosedGenericAliasProxy<int>), new ClosedGenericAliasDerived(42));
                proxy.Should().BeAssignableTo<IClosedGenericAliasProxy<int>>();
                ((IClosedGenericAliasProxy<int>)proxy!).Value.Should().Be(42);
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
    public void GenerateProcessorShouldSupportClosedGenericDuckTypeTaskContracts()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var traceAssemblyPath = typeof(IDuckType).Assembly.Location;
            var traceAssemblyName = AssemblyName.GetAssemblyName(traceAssemblyPath).Name;
            var runtimeAssemblyPath = typeof(Task).Assembly.Location;
            var runtimeAssemblyName = AssemblyName.GetAssemblyName(runtimeAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.DuckTypeTask.Generic.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-duck-type-task-generic.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-duck-type-task-generic.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-duck-type-task-generic.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IDuckTypeTask<string>).FullName,
                        proxyAssembly = traceAssemblyName,
                        targetType = typeof(Task<string>).FullName,
                        targetAssembly = runtimeAssemblyName
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IDuckTypeAwaiter<string>).FullName,
                        proxyAssembly = traceAssemblyName,
                        targetType = typeof(TaskAwaiter<string>).FullName,
                        targetAssembly = runtimeAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { traceAssemblyPath },
                targetAssemblies: new[] { runtimeAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.DuckTypeTask.Generic",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().HaveCount(2);
            matrix.Mappings.Should().OnlyContain(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-DuckTypeTask-Generic", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var completedTask = Task.FromResult("completed");
                var proxy = DuckType.Create(typeof(IDuckTypeTask<string>), completedTask);
                proxy.Should().BeAssignableTo<IDuckTypeTask<string>>();

                var duckTask = (IDuckTypeTask<string>)proxy!;
                duckTask.IsCompletedSuccessfully.Should().BeTrue();
                duckTask.Result.Should().Be("completed");
                duckTask.GetAwaiter().IsCompleted.Should().BeTrue();
#pragma warning disable xUnit1031 // The test intentionally exercises the duck-typed awaiter GetResult contract.
                duckTask.GetAwaiter().GetResult().Should().Be("completed");
#pragma warning restore xUnit1031
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
    public void GenerateProcessorShouldSupportProxyContractsThatInheritExternalInterfaces()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ExternalInterfaceInheritance.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-external-interface-inheritance.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-external-interface-inheritance.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-external-interface-inheritance.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IExternalInheritedCompletionProxy).FullName,
                        proxyAssembly = sharedAssemblyName,
                        targetType = typeof(ExternalInheritedCompletionTarget).FullName,
                        targetAssembly = sharedAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ExternalInterfaceInheritance",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ExternalInterfaceInheritance", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var target = new ExternalInheritedCompletionTarget();
                var proxy = DuckType.Create(typeof(IExternalInheritedCompletionProxy), target);
                proxy.Should().BeAssignableTo<IExternalInheritedCompletionProxy>();

                var completionProxy = (IExternalInheritedCompletionProxy)proxy!;
                completionProxy.Value.Should().Be("completed");
                var continuationCalled = false;
                completionProxy.OnCompleted(() => continuationCalled = true);
                continuationCalled.Should().BeTrue();
                var unsafeContinuationCalled = false;
                completionProxy.UnsafeOnCompleted(() => unsafeContinuationCalled = true);
                unsafeContinuationCalled.Should().BeTrue();
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
    public void GenerateProcessorShouldHonorDuckAttributeBindingFlagsIgnoreCase()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.BindingFlags.IgnoreCase.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-binding-flags-ignore-case.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-binding-flags-ignore-case.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-binding-flags-ignore-case.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IBindingFlagsIgnoreCaseProxy).FullName,
                        proxyAssembly = sharedAssemblyName,
                        targetType = typeof(BindingFlagsIgnoreCaseTarget).FullName,
                        targetAssembly = sharedAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.BindingFlags.IgnoreCase",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-BindingFlags-IgnoreCase", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var proxy = DuckType.Create(typeof(IBindingFlagsIgnoreCaseProxy), new BindingFlagsIgnoreCaseTarget());
                proxy.Should().BeAssignableTo<IBindingFlagsIgnoreCaseProxy>();
                var ignoreCaseProxy = (IBindingFlagsIgnoreCaseProxy)proxy!;
                ignoreCaseProxy.Property.Should().Be("property");
                ignoreCaseProxy.Field.Should().Be("field");
                ignoreCaseProxy.Method("method").Should().Be("method:method");
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
    public void GenerateProcessorShouldKeepIgnoreCaseOutOfRelaxedMethodFallback()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.BindingFlags.IgnoreCaseRelaxedFallback.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-binding-flags-ignore-case-relaxed-fallback.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-binding-flags-ignore-case-relaxed-fallback.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-binding-flags-ignore-case-relaxed-fallback.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    CreateMappingDocumentEntry(typeof(IIgnoreCaseRelaxedFallbackProxy), typeof(IgnoreCaseRelaxedFallbackTarget))
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.BindingFlags.IgnoreCaseRelaxedFallback",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            var mapping = matrix!.Mappings.Should().ContainSingle().Subject;
            mapping.Status.Should().Be(DuckTypeAotCompatibilityStatuses.MissingTargetMethod);
            mapping.DiagnosticCode.Should().Be("DTAOT0207");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldHonorDuckAttributeBindingFlagsVisibilityStaticnessAndScope()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.BindingFlags.Semantics.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-binding-flags-semantics.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-binding-flags-semantics.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-binding-flags-semantics.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    CreateMappingDocumentEntry(typeof(IBindingFlagsPublicOnlyFieldProxy), typeof(BindingFlagsPrivateFieldTarget)),
                    CreateMappingDocumentEntry(typeof(IBindingFlagsInstanceOnlyStaticFieldProxy), typeof(BindingFlagsStaticFieldTarget)),
                    CreateMappingDocumentEntry(typeof(IBindingFlagsDeclaredOnlyPropertyProxy), typeof(BindingFlagsDerivedTarget)),
                    CreateMappingDocumentEntry(typeof(IBindingFlagsFlattenHierarchyStaticPropertyProxy), typeof(BindingFlagsStaticDerivedTarget)),
                    CreateMappingDocumentEntry(typeof(IBindingFlagsFallbackDeclaredOnlyPropertyProxy), typeof(BindingFlagsFallbackDerivedTarget)),
                    CreateMappingDocumentEntry(typeof(IBindingFlagsFallbackDeclaredOnlyFieldProxy), typeof(BindingFlagsFallbackDerivedTarget))
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.BindingFlags.Semantics",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            AssertMappingDiagnostic(matrix!, typeof(IBindingFlagsPublicOnlyFieldProxy), typeof(BindingFlagsPrivateFieldTarget), DuckTypeAotCompatibilityStatuses.MissingTargetMethod, "DTAOT0207");
            AssertMappingDiagnostic(matrix!, typeof(IBindingFlagsInstanceOnlyStaticFieldProxy), typeof(BindingFlagsStaticFieldTarget), DuckTypeAotCompatibilityStatuses.MissingTargetMethod, "DTAOT0207");
            AssertMappingDiagnostic(matrix!, typeof(IBindingFlagsDeclaredOnlyPropertyProxy), typeof(BindingFlagsDerivedTarget), DuckTypeAotCompatibilityStatuses.MissingTargetMethod, "DTAOT0207");
            AssertMappingDiagnostic(matrix!, typeof(IBindingFlagsFlattenHierarchyStaticPropertyProxy), typeof(BindingFlagsStaticDerivedTarget), DuckTypeAotCompatibilityStatuses.Compatible, null);
            AssertMappingDiagnostic(matrix!, typeof(IBindingFlagsFallbackDeclaredOnlyPropertyProxy), typeof(BindingFlagsFallbackDerivedTarget), DuckTypeAotCompatibilityStatuses.Compatible, null);
            AssertMappingDiagnostic(matrix!, typeof(IBindingFlagsFallbackDeclaredOnlyFieldProxy), typeof(BindingFlagsFallbackDerivedTarget), DuckTypeAotCompatibilityStatuses.Compatible, null);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-BindingFlags-Semantics", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var proxy = DuckType.Create(typeof(IBindingFlagsFlattenHierarchyStaticPropertyProxy), new BindingFlagsStaticDerivedTarget());
                proxy.Should().BeAssignableTo<IBindingFlagsFlattenHierarchyStaticPropertyProxy>();
                ((IBindingFlagsFlattenHierarchyStaticPropertyProxy)proxy!).Flattened.Should().Be("flattened");

                var fallbackPropertyProxy = DuckType.Create(typeof(IBindingFlagsFallbackDeclaredOnlyPropertyProxy), new BindingFlagsFallbackDerivedTarget());
                fallbackPropertyProxy.Should().BeAssignableTo<IBindingFlagsFallbackDeclaredOnlyPropertyProxy>();
                ((IBindingFlagsFallbackDeclaredOnlyPropertyProxy)fallbackPropertyProxy!).SecretProperty.Should().Be("base-property");

                var fallbackFieldProxy = DuckType.Create(typeof(IBindingFlagsFallbackDeclaredOnlyFieldProxy), new BindingFlagsFallbackDerivedTarget());
                fallbackFieldProxy.Should().BeAssignableTo<IBindingFlagsFallbackDeclaredOnlyFieldProxy>();
                ((IBindingFlagsFallbackDeclaredOnlyFieldProxy)fallbackFieldProxy!).SecretField.Should().Be("base-field");
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
    public void GenerateProcessorShouldSupportInheritedGenericInterfaceSubstitutions()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.InheritedGenericSubstitution.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-inherited-generic-substitution.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-inherited-generic-substitution.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-inherited-generic-substitution.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IInheritedGenericConstantProxy<int>).FullName,
                        proxyAssembly = sharedAssemblyName,
                        targetType = typeof(InheritedGenericStringTarget).FullName,
                        targetAssembly = sharedAssemblyName
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IInheritedGenericComposedProxy<int>).FullName,
                        proxyAssembly = sharedAssemblyName,
                        targetType = typeof(InheritedGenericListTarget).FullName,
                        targetAssembly = sharedAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.InheritedGenericSubstitution",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().OnlyContain(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-InheritedGenericSubstitution", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var constantProxy = DuckType.Create(typeof(IInheritedGenericConstantProxy<int>), new InheritedGenericStringTarget());
                constantProxy.Should().BeAssignableTo<IInheritedGenericConstantProxy<int>>();
                ((IInheritedGenericConstantProxy<int>)constantProxy!).Value.Should().Be("constant");
                ((IInheritedGenericConstantProxy<int>)constantProxy).Echo("value").Should().Be("echo:value");

                var composedProxy = DuckType.Create(typeof(IInheritedGenericComposedProxy<int>), new InheritedGenericListTarget());
                composedProxy.Should().BeAssignableTo<IInheritedGenericComposedProxy<int>>();
                ((IInheritedGenericComposedProxy<int>)composedProxy!).Value.Should().BeEquivalentTo([1, 2, 3]);
                ((IInheritedGenericComposedProxy<int>)composedProxy).Echo([4, 5]).Should().BeEquivalentTo([4, 5, 9]);
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
    public void GenerateProcessorShouldPreserveInheritedGenericByRefParameterMetadataAndArraySubstitution()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.InheritedGenericByRefSubstitution.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-inherited-generic-byref-substitution.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-inherited-generic-byref-substitution.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-inherited-generic-byref-substitution.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IInheritedGenericByRefComposedProxy<int>).FullName,
                        proxyAssembly = sharedAssemblyName,
                        targetType = typeof(InheritedGenericByRefTarget).FullName,
                        targetAssembly = sharedAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.InheritedGenericByRefSubstitution",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-InheritedGenericByRefSubstitution", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var proxy = DuckType.Create(typeof(IInheritedGenericByRefComposedProxy<int>), new InheritedGenericByRefTarget());
                proxy.Should().BeAssignableTo<IInheritedGenericByRefComposedProxy<int>>();
                var typedProxy = (IInheritedGenericByRefComposedProxy<int>)proxy!;

                typedProxy.TryGet(out var value).Should().BeTrue();
                value.Should().BeEquivalentTo([1, 2, 3]);

                var updateValue = new List<int> { 6 };
                typedProxy.TryUpdate(ref updateValue).Should().BeTrue();
                updateValue.Should().BeEquivalentTo([6, 8]);

                var grid = new[,] { { new List<int> { 4, 5 } } };
                typedProxy.EchoGrid(grid)[0, 0].Should().BeEquivalentTo([4, 5, 9]);
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
    public void GenerateProcessorShouldSupportTrailingOptionalTargetParameters()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.OptionalParameters.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-optional-parameters.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-optional-parameters.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-optional-parameters.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IOptionalParameterProxy).FullName,
                        proxyAssembly = sharedAssemblyName,
                        targetType = typeof(OptionalParameterTarget).FullName,
                        targetAssembly = sharedAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.OptionalParameters",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-OptionalParameters", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var proxy = DuckType.Create(typeof(IOptionalParameterProxy), new OptionalParameterTarget());
                proxy.Should().BeAssignableTo<IOptionalParameterProxy>();
                ((IOptionalParameterProxy)proxy!).Add(5).Should().Be(12);
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
    public void GenerateProcessorShouldNotTreatParameterConstantAsOptionalFlag()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyPath = CreateConstantOnlyOptionalTargetAssembly(tempDirectory);
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.OptionalParameters.ConstantOnly.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-optional-constant-only.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-optional-constant-only.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-optional-constant-only.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IConstantOnlyOptionalParameterProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = "ExternalOptional.ConstantOnlyOptionalParameterTarget",
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.OptionalParameters.ConstantOnly",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            var mapping = matrix!.Mappings.Should().ContainSingle().Subject;
            mapping.Status.Should().Be(DuckTypeAotCompatibilityStatuses.MissingTargetMethod);
            mapping.DiagnosticCode.Should().Be("DTAOT0207");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportReverseClosedGenericMappingsWhenDelegationTypeIsDirectlyAssignable()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(List<int>).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ClosedGeneric.Reverse.Assignable.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-closed-generic-reverse-assignable.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-closed-generic-reverse-assignable.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-closed-generic-reverse-assignable.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "reverse",
                        proxyType = typeof(IReadOnlyCollection<int>).FullName,
                        proxyAssembly = sharedAssemblyName,
                        targetType = typeof(List<int>).FullName,
                        targetAssembly = sharedAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ClosedGeneric.Reverse.Assignable",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ClosedGeneric-Reverse-Assignable", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var delegation = new List<int> { 1, 3, 5 };
                var reverseProxy = DuckType.CreateReverse(typeof(IReadOnlyCollection<int>), delegation);
                reverseProxy.Should().NotBeNull();
                reverseProxy.Should().BeAssignableTo<IReadOnlyCollection<int>>();
                ((IReadOnlyCollection<int>)reverseProxy!).Count.Should().Be(3);
            }
            finally
            {
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

    [Fact]
    public void GenerateProcessorShouldEmitReverseInterfaceProxyAsCompatible()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Reverse.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-reverse.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-reverse.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-reverse.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "reverse",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Reverse",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var bootstrapType = generatedModule.Find("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap", isReflectionName: false);
                bootstrapType.Should().NotBeNull();
                var generatedProxyType = generatedModule.Types.SingleOrDefault(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                generatedProxyType.Should().NotBeNull();
                var registerAotReverseInstruction = bootstrapType!.Methods
                    .Where(method => method.Body is not null)
                    .SelectMany(method => method.Body!.Instructions)
                    .SingleOrDefault(
                    instruction =>
                        instruction.OpCode == OpCodes.Call &&
                        instruction.Operand is IMethod method &&
                        string.Equals(method.Name, "RegisterAotReverseProxy", StringComparison.Ordinal));
                registerAotReverseInstruction.Should().NotBeNull();
                AssertRegistrationLoadsGeneratedProxyType(bootstrapType, "RegisterAotReverseProxy", generatedProxyType!);
                AssertBootstrapDoesNotUseReflectionForGeneratedRegistrations(
                    bootstrapType.Methods
                                 .Where(method => method.Body is not null)
                                 .SelectMany(method => method.Body!.Instructions)
                                 .ToList());
                AssertDirectDelegateRegistrationCalls(bootstrapType, "RegisterAotReverseProxy", "ActivateProxy_", "System.Func`2");
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ReverseInterface", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckProxy).FullName, StringComparison.Ordinal)));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckTarget();
                var generatedInstance = constructor!.Invoke([targetInstance]);
                var echoMethod = generatedProxyType.GetMethod(nameof(ITestDuckProxy.Echo), [typeof(string)]);
                echoMethod.Should().NotBeNull();
                var echoResult = echoMethod!.Invoke(generatedInstance, ["hello"]);
                echoResult.Should().Be("hello");

                var proxyDefinitionType = generatedProxyType.GetInterfaces().Single(@interface =>
                    string.Equals(@interface.FullName, typeof(ITestDuckProxy).FullName, StringComparison.Ordinal));
                var reverseProxy = DuckType.CreateReverse(proxyDefinitionType, targetInstance);
                var reverseEchoMethod = proxyDefinitionType.GetMethod(nameof(ITestDuckProxy.Echo), [typeof(string)]);
                reverseEchoMethod.Should().NotBeNull();
                var reverseEchoResult = reverseEchoMethod!.Invoke(reverseProxy, ["world"]);
                reverseEchoResult.Should().Be("world");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportReverseGenericMethodBindings()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckGenericMethodTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Reverse.GenericMethod.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-reverse-generic-method.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-reverse-generic-method.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-reverse-generic-method.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "reverse",
                        proxyType = typeof(ITestDuckGenericMethodProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckGenericMethodTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Reverse.GenericMethod",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-Reverse-GenericMethod", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckGenericMethodProxy).FullName, StringComparison.Ordinal)));

                var proxyDefinitionType = generatedProxyType.GetInterfaces().Single(@interface =>
                    string.Equals(@interface.FullName, typeof(ITestDuckGenericMethodProxy).FullName, StringComparison.Ordinal));
                var reverseProxy = DuckType.CreateReverse(proxyDefinitionType, new TestDuckGenericMethodTarget());
                reverseProxy.Should().NotBeNull();

                var echoMethod = proxyDefinitionType.GetMethods().Single(method =>
                    string.Equals(method.Name, nameof(ITestDuckGenericMethodProxy.Echo), StringComparison.Ordinal) &&
                    method.IsGenericMethodDefinition);

                var reverseEchoInt = echoMethod.MakeGenericMethod(typeof(int)).Invoke(reverseProxy, [123]);
                reverseEchoInt.Should().Be(123);

                var reverseEchoString = echoMethod.MakeGenericMethod(typeof(string)).Invoke(reverseProxy, ["reverse"]);
                reverseEchoString.Should().Be("reverse");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldMatchDynamicReverseImplementationMemberScope()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Reverse.MemberScope.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-reverse-member-scope.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-reverse-member-scope.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-reverse-member-scope.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    CreateMappingDocumentEntry(typeof(IReverseInheritedMethodProxy), typeof(ReverseInheritedMethodTarget), mode: "reverse"),
                    CreateMappingDocumentEntry(typeof(IReversePrivatePropertyProxy), typeof(ReversePrivatePropertyTarget), mode: "reverse"),
                    CreateMappingDocumentEntry(typeof(IReverseInheritedPublicPropertyProxy), typeof(ReverseInheritedPublicPropertyTarget), mode: "reverse")
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Reverse.MemberScope",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().HaveCount(3);
            AssertMappingDiagnostic(matrix, typeof(IReverseInheritedMethodProxy), typeof(ReverseInheritedMethodTarget), DuckTypeAotCompatibilityStatuses.MissingTargetMethod, "DTAOT0207");
            AssertMappingDiagnostic(matrix, typeof(IReversePrivatePropertyProxy), typeof(ReversePrivatePropertyTarget), DuckTypeAotCompatibilityStatuses.MissingTargetMethod, "DTAOT0207");
            AssertMappingDiagnostic(matrix, typeof(IReverseInheritedPublicPropertyProxy), typeof(ReverseInheritedPublicPropertyTarget), DuckTypeAotCompatibilityStatuses.Compatible, null);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-Reverse-MemberScope", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(IReverseInheritedPublicPropertyProxy).FullName, StringComparison.Ordinal)));
                var proxyDefinitionType = generatedProxyType.GetInterfaces().Single(@interface =>
                    string.Equals(@interface.FullName, typeof(IReverseInheritedPublicPropertyProxy).FullName, StringComparison.Ordinal));

                var target = new ReverseInheritedPublicPropertyTarget("before");
                var reverseProxy = DuckType.CreateReverse(proxyDefinitionType, target);
                reverseProxy.Should().NotBeNull();

                var property = proxyDefinitionType.GetProperty(nameof(IReverseInheritedPublicPropertyProxy.Value));
                property.Should().NotBeNull();
                property!.GetValue(reverseProxy).Should().Be("before");
                property.SetValue(reverseProxy, "after");
                target.ReadValue().Should().Be("after");
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
    public void GenerateProcessorShouldSignRegistryAssemblyWhenStrongNameKeyIsProvided()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;
            var strongNameKeyFile = GetVendoredStrongNameKeyPath();

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Signed.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-signed.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-signed.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-signed.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Signed",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath,
                requireMappingCatalog: false,
                strongNameKeyFile: strongNameKeyFile);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var assemblyName = AssemblyName.GetAssemblyName(outputPath);
            var publicKeyTokenBytes = assemblyName.GetPublicKeyToken();
            publicKeyTokenBytes.Should().NotBeNull();
            publicKeyTokenBytes!.Should().NotBeEmpty();
            var publicKeyToken = BitConverter.ToString(publicKeyTokenBytes!).Replace("-", string.Empty).ToLowerInvariant();

            var manifestPath = $"{outputPath}.manifest.json";
            var manifest = JsonConvert.DeserializeObject<DuckTypeAotManifest>(File.ReadAllText(manifestPath));
            manifest.Should().NotBeNull();
            manifest!.RegistryStrongNameSigned.Should().BeTrue();
            manifest.RegistryPublicKeyToken.Should().Be(publicKeyToken);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSignRegistryAssemblyWhenStrongNameKeyComesFromEnvironment()
    {
        var tempDirectory = CreateTempDirectory();
        var originalStrongNameKeyPath = Environment.GetEnvironmentVariable(StrongNameKeyEnvironmentVariable);
        var strongNameKeyFile = GetVendoredStrongNameKeyPath();
        Environment.SetEnvironmentVariable(StrongNameKeyEnvironmentVariable, strongNameKeyFile);

        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Signed.Env.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-signed-env.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-signed-env.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-signed-env.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Signed.Env",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath,
                requireMappingCatalog: false,
                strongNameKeyFile: null);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var assemblyName = AssemblyName.GetAssemblyName(outputPath);
            var publicKeyTokenBytes = assemblyName.GetPublicKeyToken();
            publicKeyTokenBytes.Should().NotBeNull();
            publicKeyTokenBytes!.Should().NotBeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable(StrongNameKeyEnvironmentVariable, originalStrongNameKeyPath);
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldFailWhenEnvironmentStrongNameKeyDoesNotExist()
    {
        var tempDirectory = CreateTempDirectory();
        var originalStrongNameKeyPath = Environment.GetEnvironmentVariable(StrongNameKeyEnvironmentVariable);
        Environment.SetEnvironmentVariable(StrongNameKeyEnvironmentVariable, Path.Combine(tempDirectory, "missing-signing-key.snk"));

        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Unsigned.EnvMissing.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-env-missing.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-env-missing.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-env-missing.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.EnvMissing",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath,
                requireMappingCatalog: false,
                strongNameKeyFile: null);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable(StrongNameKeyEnvironmentVariable, originalStrongNameKeyPath);
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldEmitClassProxyAsCompatible()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckClassTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Class.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-class.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-class.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-class.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(TestDuckClassProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckClassTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Class",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            using var generatedModule = ModuleDefMD.Load(outputPath);
            var generatedProxyType = generatedModule.Types.SingleOrDefault(type =>
                string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
            generatedProxyType.Should().NotBeNull();
            generatedProxyType!.BaseType.Should().NotBeNull();
            generatedProxyType.BaseType!.FullName.Should().Be(typeof(TestDuckClassProxy).FullName);
            generatedProxyType.FindMethod(nameof(TestDuckClassProxy.Echo)).Should().NotBeNull();
            generatedProxyType.Interfaces.Any(interfaceImpl =>
                string.Equals(interfaceImpl.Interface.FullName, "Datadog.Trace.DuckTyping.IDuckType", StringComparison.Ordinal)).Should().BeTrue();
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldStoreClassProxyTargetBeforeBaseConstructorRuns()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Class.ConstructorOrder.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-class-constructor-order.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-class-constructor-order.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-class-constructor-order.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    CreateMappingDocumentEntry(typeof(ConstructorOrderProxyBase), typeof(ConstructorOrderTarget))
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Class.ConstructorOrder",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-Class-ConstructorOrder", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var proxy = DuckType.Create(typeof(ConstructorOrderProxyBase), new ConstructorOrderTarget("constructor-value"));
                proxy.Should().BeAssignableTo<ConstructorOrderProxyBase>();
                ((ConstructorOrderProxyBase)proxy!).CapturedDuringConstruction.Should().Be("constructor-value");
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
    public void GenerateProcessorShouldSupportClassProxyWithPrivateParameterlessConstructor()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Class.PrivateCtor.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-class-private-ctor.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-class-private-ctor.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-class-private-ctor.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    CreateMappingDocumentEntry(typeof(PrivateConstructorClassProxy), typeof(PrivateConstructorClassTarget))
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Class.PrivateCtor",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-Class-PrivateCtor", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var proxy = DuckType.Create(typeof(PrivateConstructorClassProxy), new PrivateConstructorClassTarget());
                proxy.Should().BeAssignableTo<PrivateConstructorClassProxy>();
                ((PrivateConstructorClassProxy)proxy!).ReadValue().Should().Be("private-ctor");
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
    public void GenerateProcessorShouldEmitNonPublicClassProxyMethodsAndConstructor()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Class.NonPublicMembers.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-class-non-public-members.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-class-non-public-members.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-class-non-public-members.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(NonPublicClassProxy).FullName,
                        proxyAssembly = sharedAssemblyName,
                        targetType = typeof(NonPublicClassTarget).FullName,
                        targetAssembly = sharedAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Class.NonPublicMembers",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-Class-NonPublicMembers", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var proxy = DuckType.Create(typeof(NonPublicClassProxy), new NonPublicClassTarget());
                proxy.Should().BeAssignableTo<NonPublicClassProxy>();
                ((NonPublicClassProxy)proxy!).InvokeInternal("value").Should().Be("internal:value");
                ((NonPublicClassProxy)proxy).InvokePrivateProtected("value").Should().Be("private-protected:value");
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
    public void GenerateProcessorShouldEmitDuckAsClassInterfaceProxyAsClass()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.DuckAsClassInterface.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-duck-as-class-interface.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-duck-as-class-interface.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-duck-as-class-interface.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckAsClassProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.DuckAsClassInterface",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            using var generatedModule = ModuleDefMD.Load(outputPath);
            var generatedProxyType = generatedModule.Types.SingleOrDefault(type =>
                string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
            generatedProxyType.Should().NotBeNull();
            generatedProxyType!.IsClass.Should().BeTrue();
            generatedProxyType.IsValueType.Should().BeFalse();
            generatedProxyType.BaseType.Should().NotBeNull();
            generatedProxyType.BaseType!.FullName.Should().Be("System.Object");
            generatedProxyType.Interfaces.Any(interfaceImpl =>
                string.Equals(interfaceImpl.Interface.FullName, typeof(ITestDuckAsClassProxy).FullName, StringComparison.Ordinal)).Should().BeTrue();
            generatedProxyType.Interfaces.Any(interfaceImpl =>
                string.Equals(interfaceImpl.Interface.FullName, typeof(IDuckType).FullName, StringComparison.Ordinal)).Should().BeTrue();

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-DuckAsClassInterface", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                DuckType.ResetRuntimeModeForTests();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var proxy = DuckType.Create<ITestDuckAsClassProxy>(new TestDuckTarget());
                proxy.Should().NotBeNull();
                proxy!.Echo("duck-as-class").Should().Be("duck-as-class");
                proxy.Should().BeAssignableTo<IDuckType>();
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
    public void GenerateProcessorShouldExerciseDuckIncludeDuckIgnoreAndReverseAttributeRuntimePaths()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.RuntimeFeatureCoverage.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-runtime-feature-coverage.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-runtime-feature-coverage.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-runtime-feature-coverage.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    CreateMappingDocumentEntry(typeof(IObjectDuckIncludeProxy), typeof(ObjectDuckIncludeTarget)),
                    CreateMappingDocumentEntry(typeof(DuckIgnoreRuntimeProxyBase), typeof(DuckIgnoreRuntimeTarget)),
                    CreateMappingDocumentEntry(typeof(IReverseCopiedAttributeRuntimeProxy), typeof(ReverseCopiedAttributeRuntimeTarget), mode: "reverse")
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.RuntimeFeatureCoverage",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().HaveCount(3);
            matrix.Mappings.Should().OnlyContain(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-RuntimeFeatureCoverage", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                DuckType.ResetRuntimeModeForTests();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var includeProxy = DuckType.Create(typeof(IObjectDuckIncludeProxy), new ObjectDuckIncludeTarget());
                includeProxy.Should().BeAssignableTo<IObjectDuckIncludeProxy>();
                includeProxy!.GetHashCode().Should().Be(1234);

                var ignoreProxy = DuckType.Create(typeof(DuckIgnoreRuntimeProxyBase), new DuckIgnoreRuntimeTarget());
                ignoreProxy.Should().BeAssignableTo<DuckIgnoreRuntimeProxyBase>();
                var typedIgnoreProxy = (DuckIgnoreRuntimeProxyBase)ignoreProxy!;
                typedIgnoreProxy.Value.Should().Be("target");
                typedIgnoreProxy.IgnoredValue.Should().Be("ignored");

                var reverseProxy = DuckType.CreateReverse(typeof(IReverseCopiedAttributeRuntimeProxy), new ReverseCopiedAttributeRuntimeTarget());
                reverseProxy.Should().BeAssignableTo<IReverseCopiedAttributeRuntimeProxy>();
                ((IReverseCopiedAttributeRuntimeProxy)reverseProxy!).Read().Should().Be("reverse");
                var copiedAttribute = reverseProxy.GetType().GetCustomAttribute<ReverseCopiedRuntimeMarkerAttribute>();
                copiedAttribute.Should().NotBeNull();
                copiedAttribute!.Marker.Should().Be("copied");
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
    public void GenerateProcessorShouldReportClassProxyWithoutDefaultCtorAsUnsupported()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckClassTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Class.NoCtor.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-class-noctor.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-class-noctor.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-class-noctor.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(TestDuckClassProxyWithoutDefaultCtor).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckClassTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Class.NoCtor",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.UnsupportedProxyConstructor, StringComparison.Ordinal) &&
                string.Equals(mapping.DiagnosticCode, "DTAOT0210", StringComparison.Ordinal));
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldEmitReverseClassProxyAsCompatible()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckReverseDelegation).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Reverse.Class.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-reverse-class.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-reverse-class.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-reverse-class.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "reverse",
                        proxyType = typeof(TestDuckReverseBase).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckReverseDelegation).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Reverse.Class",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            using var generatedModule = ModuleDefMD.Load(outputPath);
            var bootstrapType = generatedModule.Find("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap", isReflectionName: false);
            bootstrapType.Should().NotBeNull();
            var registerAotReverseInstruction = bootstrapType!.Methods
                .Where(method => method.Body is not null)
                .SelectMany(method => method.Body!.Instructions)
                .SingleOrDefault(
                instruction =>
                    instruction.OpCode == OpCodes.Call &&
                    instruction.Operand is IMethod method &&
                    string.Equals(method.Name, "RegisterAotReverseProxy", StringComparison.Ordinal));
            registerAotReverseInstruction.Should().NotBeNull();
            AssertBootstrapDoesNotUseReflectionForGeneratedRegistrations(
                bootstrapType.Methods
                             .Where(method => method.Body is not null)
                             .SelectMany(method => method.Body!.Instructions)
                             .ToList());
            AssertDirectDelegateRegistrationCalls(bootstrapType, "RegisterAotReverseProxy", "ActivateProxy_", "System.Func`2");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportReverseDuckChainingConversions()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckReverseChainTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Reverse.DuckChain.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-reverse-duck-chain.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-reverse-duck-chain.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-reverse-duck-chain.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckReverseChainInnerProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckReverseChainInnerTarget).FullName,
                        targetAssembly = targetAssemblyName
                    },
                    new
                    {
                        mode = "reverse",
                        proxyType = typeof(ITestDuckReverseChainProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckReverseChainTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Reverse.DuckChain",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().OnlyContain(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-Reverse-DuckChain", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyTypes = generatedAssembly.GetTypes()
                                                           .Where(type => string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal))
                                                           .ToArray();

                var targetInstance = new TestDuckReverseChainTarget();
                var generatedOuterProxyType = generatedProxyTypes.Single(type =>
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckReverseChainProxy).FullName, StringComparison.Ordinal)));
                var generatedInnerProxyType = generatedProxyTypes.Single(type =>
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckReverseChainInnerProxy).FullName, StringComparison.Ordinal)));
                var outerConstructor = GetDuckProxyConstructor(generatedOuterProxyType);
                var innerConstructor = GetDuckProxyConstructor(generatedInnerProxyType);
                outerConstructor.Should().NotBeNull();
                innerConstructor.Should().NotBeNull();

                var reverseProxyObject = outerConstructor!.Invoke([targetInstance]);
                reverseProxyObject.Should().NotBeNull();

                var getValueMethod = generatedOuterProxyType.GetMethod("get_Value", Type.EmptyTypes);
                getValueMethod.Should().NotBeNull();
                var initialValue = getValueMethod!.Invoke(reverseProxyObject, Array.Empty<object>());
                initialValue.Should().BeNull();

                var innerTarget = new TestDuckReverseChainInnerTarget("alpha");
                var innerProxyObject = innerConstructor!.Invoke([innerTarget]);
                innerProxyObject.Should().NotBeNull();

                var setValueMethod = generatedOuterProxyType.GetMethods().Single(method =>
                    string.Equals(method.Name, "set_Value", StringComparison.Ordinal));
                _ = setValueMethod.Invoke(reverseProxyObject, [innerProxyObject]);
                targetInstance.Value.Should().BeSameAs(innerTarget);

                var roundtripMethod = generatedOuterProxyType.GetMethods().Single(method =>
                    string.Equals(method.Name, nameof(ITestDuckReverseChainProxy.Roundtrip), StringComparison.Ordinal));
                var roundtripResult = roundtripMethod.Invoke(reverseProxyObject, [innerProxyObject]);
                roundtripResult.Should().NotBeNull();
                targetInstance.Value.Should().BeSameAs(innerTarget);

                var roundtripInstanceGetter = roundtripResult!.GetType().GetMethod("get_Instance", Type.EmptyTypes);
                roundtripInstanceGetter.Should().NotBeNull();
                var roundtripTarget = roundtripInstanceGetter!.Invoke(roundtripResult, Array.Empty<object>());
                roundtripTarget.Should().BeSameAs(innerTarget);

                var afterSetValue = getValueMethod.Invoke(reverseProxyObject, Array.Empty<object>());
                afterSetValue.Should().NotBeNull();
                var afterSetInstanceGetter = afterSetValue!.GetType().GetMethod("get_Instance", Type.EmptyTypes);
                afterSetInstanceGetter.Should().NotBeNull();
                var afterSetTarget = afterSetInstanceGetter!.Invoke(afterSetValue, Array.Empty<object>());
                afterSetTarget.Should().BeSameAs(innerTarget);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldEmitInternalTargetMethodAsCompatibleAndAddIgnoresAccessChecks()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckInternalTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Internal.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-internal.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-internal.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-internal.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckInternalTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Internal",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            using var generatedModule = ModuleDefMD.Load(outputPath);
            var ignoresAccessChecksTargets = generatedModule.Assembly.CustomAttributes
                .Where(attribute => string.Equals(attribute.AttributeType.FullName, "System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute", StringComparison.Ordinal))
                .Select(attribute => attribute.ConstructorArguments[0].Value!.ToString())
                .ToList();
            ignoresAccessChecksTargets.Should().Contain(targetAssemblyName);
            ignoresAccessChecksTargets.Should().Contain("Datadog.Trace");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldEmitPrivateTargetMethodAsCompatible()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckPrivateTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Private.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-private.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-private.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-private.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckPrivateTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Private",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldInvokeInternalTargetMethodAtRuntime()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckInternalTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Internal.Runtime.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-internal-runtime.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-internal-runtime.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-internal-runtime.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckInternalTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Internal.Runtime",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-InternalRuntime", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckInternalTarget();
                var generatedInstance = constructor!.Invoke([targetInstance]);
                var echoMethod = generatedProxyType.GetMethod(nameof(ITestDuckProxy.Echo), [typeof(string)]);
                echoMethod.Should().NotBeNull();
                var echoResult = echoMethod!.Invoke(generatedInstance, ["hello"]);
                echoResult.Should().Be("hello");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldInvokePrivateTargetMethodAtRuntime()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckPrivateTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Private.Runtime.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-private-runtime.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-private-runtime.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-private-runtime.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckPrivateTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Private.Runtime",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-PrivateRuntime", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckPrivateTarget();
                var generatedInstance = constructor!.Invoke([targetInstance]);
                var echoMethod = generatedProxyType.GetMethod(nameof(ITestDuckProxy.Echo), [typeof(string)]);
                echoMethod.Should().NotBeNull();
                var echoResult = echoMethod!.Invoke(generatedInstance, ["hello"]);
                echoResult.Should().Be("hello");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldEmitRunnableProxyImplementingIDuckTypeContract()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Runtime.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-runtime.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-runtime.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-runtime.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Runtime",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-Generated", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckTarget();
                var generatedInstance = constructor!.Invoke([targetInstance]);

                var echoMethod = generatedProxyType.GetMethod(nameof(ITestDuckProxy.Echo), [typeof(string)]);
                echoMethod.Should().NotBeNull();
                var echoResult = echoMethod!.Invoke(generatedInstance, ["hello"]);
                echoResult.Should().Be("hello");

                generatedInstance.Should().BeAssignableTo<IDuckType>();
                var duckTypeInstance = (IDuckType)generatedInstance;
                duckTypeInstance.Instance.Should().BeSameAs(targetInstance);
                duckTypeInstance.Type.Should().Be(typeof(TestDuckTarget));
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckNameOverrideForMethod()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckNamedMethodTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.NamedMethod.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-named-method.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-named-method.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-named-method.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckNamedMethodProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckNamedMethodTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.NamedMethod",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var generatedProxyType = generatedModule.Types.Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var echoMethod = generatedProxyType.FindMethod(nameof(ITestDuckNamedMethodProxy.Echo));
                echoMethod.Should().NotBeNull();
                echoMethod!.Body.Should().NotBeNull();
                var invokesRenamedEcho = echoMethod.Body!.Instructions.Any(instruction =>
                {
                    var method = instruction.Operand as IMethod;
                    return method is not null && string.Equals(method.Name, nameof(TestDuckNamedMethodTarget.RenamedEcho), StringComparison.Ordinal);
                });
                invokesRenamedEcho.Should().BeTrue();
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-NamedMethod", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckNamedMethodTarget();
                var generatedInstance = constructor!.Invoke([targetInstance]);
                var echoMethod = generatedProxyType.GetMethod(nameof(ITestDuckNamedMethodProxy.Echo), [typeof(string)]);
                echoMethod.Should().NotBeNull();
                var echoResult = echoMethod!.Invoke(generatedInstance, ["hello"]);
                echoResult.Should().Be("named:hello");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckNameOverrideForProperty()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckNamedPropertyTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.NamedProperty.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-named-property.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-named-property.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-named-property.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckNamedPropertyProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckNamedPropertyTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.NamedProperty",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var generatedProxyType = generatedModule.Types.Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var getValueMethod = generatedProxyType.FindMethod("get_Value");
                getValueMethod.Should().NotBeNull();
                getValueMethod!.Body.Should().NotBeNull();
                var invokesGetActual = getValueMethod.Body!.Instructions.Any(instruction =>
                {
                    var method = instruction.Operand as IMethod;
                    return method is not null && string.Equals(method.Name, "get_Actual", StringComparison.Ordinal);
                });
                invokesGetActual.Should().BeTrue();

                var setValueMethod = generatedProxyType.FindMethod("set_Value");
                setValueMethod.Should().NotBeNull();
                setValueMethod!.Body.Should().NotBeNull();
                var invokesSetActual = setValueMethod.Body!.Instructions.Any(instruction =>
                {
                    var method = instruction.Operand as IMethod;
                    return method is not null && string.Equals(method.Name, "set_Actual", StringComparison.Ordinal);
                });
                invokesSetActual.Should().BeTrue();
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-NamedProperty", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckNamedPropertyTarget { Actual = "before" };
                var generatedInstance = constructor!.Invoke([targetInstance]);

                var getValueMethod = generatedProxyType.GetMethod("get_Value", Type.EmptyTypes);
                getValueMethod.Should().NotBeNull();
                var beforeValue = getValueMethod!.Invoke(generatedInstance, Array.Empty<object>());
                beforeValue.Should().Be("before");

                var setValueMethod = generatedProxyType.GetMethod("set_Value", new[] { typeof(string) });
                setValueMethod.Should().NotBeNull();
                _ = setValueMethod!.Invoke(generatedInstance, new object?[] { "after" });
                targetInstance.Actual.Should().Be("after");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckFieldAccessorBinding()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckFieldTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Field.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-field.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-field.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-field.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckFieldProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckFieldTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Field",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-Field", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckFieldTarget();
                var generatedInstance = constructor!.Invoke([targetInstance]);

                var getValueMethod = generatedProxyType.GetMethod("get_Value", Type.EmptyTypes);
                getValueMethod.Should().NotBeNull();
                var beforeValue = getValueMethod!.Invoke(generatedInstance, Array.Empty<object>());
                beforeValue.Should().Be("initial");

                var setValueMethod = generatedProxyType.GetMethod("set_Value", new[] { typeof(string) });
                setValueMethod.Should().NotBeNull();
                _ = setValueMethod!.Invoke(generatedInstance, new object?[] { "after" });
                var afterValue = getValueMethod.Invoke(generatedInstance, Array.Empty<object>());
                afterValue.Should().Be("after");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckFieldValueWithTypeBinding()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckFieldValueWithTypeTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Field.ValueWithType.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-field-value-with-type.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-field-value-with-type.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-field-value-with-type.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckFieldValueWithTypeProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckFieldValueWithTypeTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Field.ValueWithType",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-Field-ValueWithType", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckFieldValueWithTypeTarget();
                var generatedInstance = constructor!.Invoke([targetInstance]);

                var getValueMethod = generatedProxyType.GetMethod("get_Value", Type.EmptyTypes);
                getValueMethod.Should().NotBeNull();
                var beforeValueObject = getValueMethod!.Invoke(generatedInstance, Array.Empty<object>());
                beforeValueObject.Should().NotBeNull();
                beforeValueObject.Should().BeOfType<ValueWithType<string>>();
                var beforeValue = (ValueWithType<string>)beforeValueObject!;
                beforeValue.Value.Should().Be("initial");
                beforeValue.Type.Should().Be(typeof(string));

                var setValueMethod = generatedProxyType.GetMethod("set_Value", new[] { typeof(ValueWithType<string>) });
                setValueMethod.Should().NotBeNull();
                _ = setValueMethod!.Invoke(generatedInstance, [ValueWithType<string>.Create("after", typeof(string))]);

                targetInstance.ReadValue().Should().Be("after");

                var afterValueObject = getValueMethod.Invoke(generatedInstance, Array.Empty<object>());
                afterValueObject.Should().NotBeNull();
                afterValueObject.Should().BeOfType<ValueWithType<string>>();
                var afterValue = (ValueWithType<string>)afterValueObject!;
                afterValue.Value.Should().Be("after");
                afterValue.Type.Should().Be(typeof(string));
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckChainFieldBindings()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckChainFieldTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.DuckChain.Field.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-duck-chain-field.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-duck-chain-field.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-duck-chain-field.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckChainFieldInnerProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckChainFieldInnerTarget).FullName,
                        targetAssembly = targetAssemblyName
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckChainFieldProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckChainFieldTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.DuckChain.Field",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var generatedFieldProxyType = generatedModule.Types.Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.Interfaces.Any(interfaceImpl => string.Equals(interfaceImpl.Interface.FullName, typeof(ITestDuckChainFieldProxy).FullName, StringComparison.Ordinal)));

                var getValueMethod = generatedFieldProxyType.FindMethod("get_Value");
                getValueMethod.Should().NotBeNull();
                getValueMethod!.Body.Should().NotBeNull();
                var emitsCreateCacheCreateOnFieldGet = getValueMethod.Body!.Instructions.Any(instruction =>
                {
                    var method = instruction.Operand as IMethod;
                    return instruction.OpCode == OpCodes.Call
                        && method is not null
                        && string.Equals(method.Name, "Create", StringComparison.Ordinal)
                        && method.DeclaringType.FullName.IndexOf("Datadog.Trace.DuckTyping.DuckType/CreateCache`1", StringComparison.Ordinal) >= 0;
                });
                emitsCreateCacheCreateOnFieldGet.Should().BeTrue();

                var setValueMethod = generatedFieldProxyType.FindMethod("set_Value");
                setValueMethod.Should().NotBeNull();
                setValueMethod!.Body.Should().NotBeNull();
                var emitsDuckTypeInstanceExtractionOnFieldSet = setValueMethod.Body!.Instructions.Any(instruction =>
                {
                    var method = instruction.Operand as IMethod;
                    return instruction.OpCode == OpCodes.Callvirt
                        && method is not null
                        && string.Equals(method.Name, "get_Instance", StringComparison.Ordinal)
                        && string.Equals(method.DeclaringType.FullName, typeof(IDuckType).FullName, StringComparison.Ordinal);
                });
                emitsDuckTypeInstanceExtractionOnFieldSet.Should().BeTrue();
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-DuckChain-Field", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyTypes = generatedAssembly.GetTypes()
                                                           .Where(type => string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal))
                                                           .ToArray();

                var generatedFieldProxyType = generatedProxyTypes.Single(type =>
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckChainFieldProxy).FullName, StringComparison.Ordinal)));
                var generatedInnerProxyType = generatedProxyTypes.Single(type =>
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckChainFieldInnerProxy).FullName, StringComparison.Ordinal)));

                var fieldProxyConstructor = GetDuckProxyConstructor(generatedFieldProxyType);
                var innerProxyConstructor = GetDuckProxyConstructor(generatedInnerProxyType);
                fieldProxyConstructor.Should().NotBeNull();
                innerProxyConstructor.Should().NotBeNull();

                var targetInstance = new TestDuckChainFieldTarget();
                var fieldProxyInstance = fieldProxyConstructor!.Invoke([targetInstance]);

                var getValueMethod = generatedFieldProxyType.GetMethod("get_Value", Type.EmptyTypes);
                getValueMethod.Should().NotBeNull();
                var initialValue = getValueMethod!.Invoke(fieldProxyInstance, Array.Empty<object>());
                initialValue.Should().BeNull();

                var innerTarget = new TestDuckChainFieldInnerTarget("field");
                var innerProxyInstance = innerProxyConstructor!.Invoke([innerTarget]);

                var setValueMethod = generatedFieldProxyType.GetMethods().Single(method =>
                    string.Equals(method.Name, "set_Value", StringComparison.Ordinal));
                _ = setValueMethod.Invoke(fieldProxyInstance, [innerProxyInstance]);
                targetInstance.ReadValue().Should().BeSameAs(innerTarget);

                var afterSetValue = getValueMethod.Invoke(fieldProxyInstance, Array.Empty<object>());
                afterSetValue.Should().NotBeNull();
                afterSetValue.Should().BeAssignableTo<IDuckType>();
                ((IDuckType)afterSetValue!).Instance.Should().BeSameAs(innerTarget);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckPropertyOrFieldFallbackToField()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckPropertyOrFieldTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.PropertyOrField.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-property-or-field.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-property-or-field.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-property-or-field.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckPropertyOrFieldProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckPropertyOrFieldTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.PropertyOrField",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-PropertyOrField", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckPropertyOrFieldTarget();
                var generatedInstance = constructor!.Invoke([targetInstance]);

                var getValueMethod = generatedProxyType.GetMethod("get_Value", Type.EmptyTypes);
                getValueMethod.Should().NotBeNull();
                var beforeValue = getValueMethod!.Invoke(generatedInstance, Array.Empty<object>());
                beforeValue.Should().Be("initial");

                var setValueMethod = generatedProxyType.GetMethod("set_Value", new[] { typeof(string) });
                setValueMethod.Should().NotBeNull();
                _ = setValueMethod!.Invoke(generatedInstance, new object?[] { "after" });
                var afterValue = getValueMethod.Invoke(generatedInstance, Array.Empty<object>());
                afterValue.Should().Be("after");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckChainingPropertyBindings()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckChainPropertyTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.DuckChain.Property.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-duck-chain-property.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-duck-chain-property.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-duck-chain-property.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckChainPropertyInnerProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckChainPropertyInnerTarget).FullName,
                        targetAssembly = targetAssemblyName
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckChainPropertyProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckChainPropertyTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.DuckChain.Property",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var generatedPropertyProxyType = generatedModule.Types.Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.Interfaces.Any(interfaceImpl => string.Equals(interfaceImpl.Interface.FullName, typeof(ITestDuckChainPropertyProxy).FullName, StringComparison.Ordinal)));

                var getValueMethod = generatedPropertyProxyType.FindMethod("get_Value");
                getValueMethod.Should().NotBeNull();
                getValueMethod!.Body.Should().NotBeNull();
                var emitsCreateCacheCreateOnPropertyGet = getValueMethod.Body!.Instructions.Any(instruction =>
                {
                    var method = instruction.Operand as IMethod;
                    return instruction.OpCode == OpCodes.Call
                        && method is not null
                        && string.Equals(method.Name, "Create", StringComparison.Ordinal)
                        && method.DeclaringType.FullName.IndexOf("Datadog.Trace.DuckTyping.DuckType/CreateCache`1", StringComparison.Ordinal) >= 0;
                });
                emitsCreateCacheCreateOnPropertyGet.Should().BeTrue();

                var setValueMethod = generatedPropertyProxyType.FindMethod("set_Value");
                setValueMethod.Should().NotBeNull();
                setValueMethod!.Body.Should().NotBeNull();
                var emitsDuckTypeInstanceExtractionOnPropertySet = setValueMethod.Body!.Instructions.Any(instruction =>
                {
                    var method = instruction.Operand as IMethod;
                    return instruction.OpCode == OpCodes.Callvirt
                        && method is not null
                        && string.Equals(method.Name, "get_Instance", StringComparison.Ordinal)
                        && string.Equals(method.DeclaringType.FullName, typeof(IDuckType).FullName, StringComparison.Ordinal);
                });
                emitsDuckTypeInstanceExtractionOnPropertySet.Should().BeTrue();
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-DuckChain-Property", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyTypes = generatedAssembly.GetTypes()
                                                           .Where(type => string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal))
                                                           .ToArray();

                var generatedPropertyProxyType = generatedProxyTypes.Single(type =>
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckChainPropertyProxy).FullName, StringComparison.Ordinal)));
                var generatedInnerProxyType = generatedProxyTypes.Single(type =>
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckChainPropertyInnerProxy).FullName, StringComparison.Ordinal)));

                var propertyProxyConstructor = GetDuckProxyConstructor(generatedPropertyProxyType);
                var innerProxyConstructor = GetDuckProxyConstructor(generatedInnerProxyType);
                propertyProxyConstructor.Should().NotBeNull();
                innerProxyConstructor.Should().NotBeNull();

                var targetInstance = new TestDuckChainPropertyTarget();
                var propertyProxyInstance = propertyProxyConstructor!.Invoke([targetInstance]);

                var getValueMethod = generatedPropertyProxyType.GetMethod("get_Value", Type.EmptyTypes);
                getValueMethod.Should().NotBeNull();
                var initialValue = getValueMethod!.Invoke(propertyProxyInstance, Array.Empty<object>());
                initialValue.Should().BeNull();

                var innerTarget = new TestDuckChainPropertyInnerTarget("property");
                var innerProxyInstance = innerProxyConstructor!.Invoke([innerTarget]);

                var setValueMethod = generatedPropertyProxyType.GetMethods().Single(method =>
                    string.Equals(method.Name, "set_Value", StringComparison.Ordinal));
                _ = setValueMethod.Invoke(propertyProxyInstance, [innerProxyInstance]);
                targetInstance.Value.Should().BeSameAs(innerTarget);

                var afterSetValue = getValueMethod.Invoke(propertyProxyInstance, Array.Empty<object>());
                afterSetValue.Should().NotBeNull();
                afterSetValue.Should().BeAssignableTo<IDuckType>();
                ((IDuckType)afterSetValue!).Instance.Should().BeSameAs(innerTarget);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckPropertyOrFieldValueWithTypeFallbackToField()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckPropertyOrFieldTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.PropertyOrField.ValueWithType.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-property-or-field-value-with-type.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-property-or-field-value-with-type.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-property-or-field-value-with-type.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckPropertyOrFieldValueWithTypeProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckPropertyOrFieldTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.PropertyOrField.ValueWithType",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-PropertyOrField-ValueWithType", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckPropertyOrFieldTarget();
                var generatedInstance = constructor!.Invoke([targetInstance]);

                var getValueMethod = generatedProxyType.GetMethod("get_Value", Type.EmptyTypes);
                getValueMethod.Should().NotBeNull();
                var beforeValueObject = getValueMethod!.Invoke(generatedInstance, Array.Empty<object>());
                beforeValueObject.Should().NotBeNull();
                beforeValueObject.Should().BeOfType<ValueWithType<string>>();
                var beforeValue = (ValueWithType<string>)beforeValueObject!;
                beforeValue.Value.Should().Be("initial");
                beforeValue.Type.Should().Be(typeof(string));

                var setValueMethod = generatedProxyType.GetMethod("set_Value", new[] { typeof(ValueWithType<string>) });
                setValueMethod.Should().NotBeNull();
                _ = setValueMethod!.Invoke(generatedInstance, [ValueWithType<string>.Create("after", typeof(string))]);

                targetInstance.ReadValue().Should().Be("after");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckFieldBindingForStaticFields()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            TestDuckStaticFieldTarget.ResetValue("initial");
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckStaticFieldTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.StaticField.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-static-field.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-static-field.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-static-field.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckStaticFieldProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckStaticFieldTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.StaticField",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-StaticField", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckStaticFieldTarget();
                var generatedInstance = constructor!.Invoke([targetInstance]);
                var getValueMethod = generatedProxyType.GetMethod("get_Value", Type.EmptyTypes);
                getValueMethod.Should().NotBeNull();
                var beforeValue = getValueMethod!.Invoke(generatedInstance, Array.Empty<object>());
                beforeValue.Should().Be("initial");

                var setValueMethod = generatedProxyType.GetMethod("set_Value", new[] { typeof(string) });
                setValueMethod.Should().NotBeNull();
                _ = setValueMethod!.Invoke(generatedInstance, new object?[] { "after" });
                var afterValue = getValueMethod.Invoke(generatedInstance, Array.Empty<object>());
                afterValue.Should().Be("after");
                TestDuckStaticFieldTarget.ReadValue().Should().Be("after");
            }
            finally
            {
                loadContext.Unload();
                TestDuckStaticFieldTarget.ResetValue("initial");
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckStaticFieldValueWithTypeBinding()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            TestDuckStaticFieldTarget.ResetValue("initial");
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckStaticFieldTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.StaticField.ValueWithType.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-static-field-value-with-type.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-static-field-value-with-type.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-static-field-value-with-type.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckStaticFieldValueWithTypeProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckStaticFieldTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.StaticField.ValueWithType",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-StaticField-ValueWithType", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckStaticFieldTarget();
                var generatedInstance = constructor!.Invoke([targetInstance]);

                var getValueMethod = generatedProxyType.GetMethod("get_Value", Type.EmptyTypes);
                getValueMethod.Should().NotBeNull();
                var beforeValueObject = getValueMethod!.Invoke(generatedInstance, Array.Empty<object>());
                beforeValueObject.Should().NotBeNull();
                beforeValueObject.Should().BeOfType<ValueWithType<string>>();
                var beforeValue = (ValueWithType<string>)beforeValueObject!;
                beforeValue.Value.Should().Be("initial");
                beforeValue.Type.Should().Be(typeof(string));

                var setValueMethod = generatedProxyType.GetMethod("set_Value", new[] { typeof(ValueWithType<string>) });
                setValueMethod.Should().NotBeNull();
                _ = setValueMethod!.Invoke(generatedInstance, [ValueWithType<string>.Create("after", typeof(string))]);

                var afterValueObject = getValueMethod.Invoke(generatedInstance, Array.Empty<object>());
                afterValueObject.Should().NotBeNull();
                afterValueObject.Should().BeOfType<ValueWithType<string>>();
                var afterValue = (ValueWithType<string>)afterValueObject!;
                afterValue.Value.Should().Be("after");
                afterValue.Type.Should().Be(typeof(string));
                TestDuckStaticFieldTarget.ReadValue().Should().Be("after");
            }
            finally
            {
                loadContext.Unload();
                TestDuckStaticFieldTarget.ResetValue("initial");
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldReportReadonlyFieldSetterAsIncompatible()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckReadonlyFieldTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ReadonlyField.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-readonly-field.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-readonly-field.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-readonly-field.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckReadonlyFieldProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckReadonlyFieldTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ReadonlyField",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle();
            var mapping = matrix.Mappings[0];
            mapping.Status.Should().Be(DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature);
            mapping.DiagnosticCode.Should().Be("DTAOT0209");
            mapping.Details.Should().NotBeNull();
            mapping.Details!.Should().Contain("readonly");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldReportPrivateBasePropertyWithoutFallbackAsMissingTargetMember()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckBasePrivatePropertyTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.BasePrivateProperty.NoFallback.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-base-private-property-no-fallback.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-base-private-property-no-fallback.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-base-private-property-no-fallback.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckBasePrivatePropertyNoFallbackProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckBasePrivatePropertyTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.BasePrivateProperty.NoFallback",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle();

            var mapping = matrix.Mappings[0];
            mapping.Status.Should().BeOneOf(
                DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature);
            mapping.Details.Should().NotBeNull();
            mapping.Details!.Should().Contain("not found");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportPrivateBasePropertyWhenFallbackToBaseTypesIsEnabled()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckBasePrivatePropertyTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.BasePrivateProperty.Fallback.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-base-private-property-fallback.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-base-private-property-fallback.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-base-private-property-fallback.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckBasePrivatePropertyFallbackProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckBasePrivatePropertyTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.BasePrivateProperty.Fallback",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-BasePrivateProperty-Fallback", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var contextTestAssembly = loadContext.LoadFromAssemblyPath(proxyAssemblyPath);

                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckBasePrivatePropertyFallbackProxy).FullName, StringComparison.Ordinal)));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetType = contextTestAssembly.GetType(typeof(TestDuckBasePrivatePropertyTarget).FullName!, throwOnError: true)!;
                var targetCtor = targetType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    types: [typeof(int), typeof(int)],
                    modifiers: null);
                targetCtor.Should().NotBeNull();
                var targetInstance = targetCtor!.Invoke([41, 5]);

                var proxyInstance = constructor!.Invoke([targetInstance]);
                var getHiddenMethod = generatedProxyType.GetMethod("get_Hidden", Type.EmptyTypes);
                var setHiddenMethod = generatedProxyType.GetMethod("set_Hidden", [typeof(int)]);
                getHiddenMethod.Should().NotBeNull();
                setHiddenMethod.Should().NotBeNull();

                var before = getHiddenMethod!.Invoke(proxyInstance, Array.Empty<object>());
                before.Should().Be(41);

                _ = setHiddenMethod!.Invoke(proxyInstance, [73]);

                var after = getHiddenMethod.Invoke(proxyInstance, Array.Empty<object>());
                after.Should().Be(73);

                var readHiddenMethod = targetType.GetMethod("ReadHidden", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                readHiddenMethod.Should().NotBeNull();
                var targetHidden = readHiddenMethod!.Invoke(targetInstance, Array.Empty<object>());
                targetHidden.Should().Be(73);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldReportPrivateBaseFieldWithoutFallbackAsMissingTargetMember()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckBasePrivateFieldTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.BasePrivateField.NoFallback.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-base-private-field-no-fallback.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-base-private-field-no-fallback.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-base-private-field-no-fallback.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckBasePrivateFieldNoFallbackProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckBasePrivateFieldTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.BasePrivateField.NoFallback",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle();

            var mapping = matrix.Mappings[0];
            mapping.Status.Should().BeOneOf(
                DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature);
            mapping.Details.Should().NotBeNull();
            mapping.Details!.Should().Contain("not found");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportPrivateBaseFieldWhenFallbackToBaseTypesIsEnabled()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckBasePrivateFieldTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.BasePrivateField.Fallback.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-base-private-field-fallback.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-base-private-field-fallback.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-base-private-field-fallback.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckBasePrivateFieldFallbackProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckBasePrivateFieldTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.BasePrivateField.Fallback",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-BasePrivateField-Fallback", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var contextTestAssembly = loadContext.LoadFromAssemblyPath(proxyAssemblyPath);

                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckBasePrivateFieldFallbackProxy).FullName, StringComparison.Ordinal)));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetType = contextTestAssembly.GetType(typeof(TestDuckBasePrivateFieldTarget).FullName!, throwOnError: true)!;
                var targetCtor = targetType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    types: [typeof(int)],
                    modifiers: null);
                targetCtor.Should().NotBeNull();
                var targetInstance = targetCtor!.Invoke([59]);

                var proxyInstance = constructor!.Invoke([targetInstance]);
                var getHiddenMethod = generatedProxyType.GetMethod("get_Hidden", Type.EmptyTypes);
                var setHiddenMethod = generatedProxyType.GetMethod("set_Hidden", [typeof(int)]);
                getHiddenMethod.Should().NotBeNull();
                setHiddenMethod.Should().NotBeNull();

                var before = getHiddenMethod!.Invoke(proxyInstance, Array.Empty<object>());
                before.Should().Be(59);

                _ = setHiddenMethod!.Invoke(proxyInstance, [101]);

                var after = getHiddenMethod.Invoke(proxyInstance, Array.Empty<object>());
                after.Should().Be(101);

                var readHiddenMethod = targetType.GetMethod("ReadHidden", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                readHiddenMethod.Should().NotBeNull();
                var targetHidden = readHiddenMethod!.Invoke(targetInstance, Array.Empty<object>());
                targetHidden.Should().Be(101);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldResolveInheritedNonPrivateBasePropertyWithoutFallback()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckBasePrivatePropertyTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.BaseInheritedProperty.NoFallback.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-base-inherited-property-no-fallback.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-base-inherited-property-no-fallback.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-base-inherited-property-no-fallback.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckInheritedNonPrivatePropertyProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckBasePrivatePropertyTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.BaseInheritedProperty.NoFallback",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-BaseInheritedProperty-NoFallback", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var contextTestAssembly = loadContext.LoadFromAssemblyPath(proxyAssemblyPath);

                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckInheritedNonPrivatePropertyProxy).FullName, StringComparison.Ordinal)));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetType = contextTestAssembly.GetType(typeof(TestDuckBasePrivatePropertyTarget).FullName!, throwOnError: true)!;
                var targetCtor = targetType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    types: [typeof(int), typeof(int)],
                    modifiers: null);
                targetCtor.Should().NotBeNull();
                var targetInstance = targetCtor!.Invoke([13, 37]);

                var proxyInstance = constructor!.Invoke([targetInstance]);
                var getInheritedMethod = generatedProxyType.GetMethod("get_InheritedVisible", Type.EmptyTypes);
                var setInheritedMethod = generatedProxyType.GetMethod("set_InheritedVisible", [typeof(int)]);
                getInheritedMethod.Should().NotBeNull();
                setInheritedMethod.Should().NotBeNull();

                var before = getInheritedMethod!.Invoke(proxyInstance, Array.Empty<object>());
                before.Should().Be(37);

                _ = setInheritedMethod!.Invoke(proxyInstance, [91]);

                var after = getInheritedMethod.Invoke(proxyInstance, Array.Empty<object>());
                after.Should().Be(91);

                var readInheritedMethod = targetType.GetMethod("ReadInheritedVisible", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                readInheritedMethod.Should().NotBeNull();
                var targetValue = readInheritedMethod!.Invoke(targetInstance, Array.Empty<object>());
                targetValue.Should().Be(91);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldNotSupportPrivateBaseMethodEvenWhenFallbackToBaseTypesIsEnabled()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckBasePrivateMethodTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.BasePrivateMethod.Fallback.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-base-private-method-fallback.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-base-private-method-fallback.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-base-private-method-fallback.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckBasePrivateMethodFallbackProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckBasePrivateMethodTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.BasePrivateMethod.Fallback",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle();

            var mapping = matrix.Mappings[0];
            mapping.Status.Should().BeOneOf(
                DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature);
            mapping.Details.Should().NotBeNull();
            mapping.Details!.Should().Contain("not found");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldReportDuckCopyPrivateBasePropertyWithoutFallbackAsMissingTargetMember()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckBasePrivatePropertyTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.StructCopy.BasePrivateProperty.NoFallback.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-struct-copy-base-private-property-no-fallback.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy-base-private-property-no-fallback.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy-base-private-property-no-fallback.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(TestDuckStructCopyBasePrivatePropertyNoFallbackProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckBasePrivatePropertyTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.StructCopy.BasePrivateProperty.NoFallback",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle();

            var mapping = matrix.Mappings[0];
            mapping.Status.Should().BeOneOf(
                DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature);
            mapping.Details.Should().NotBeNull();
            mapping.Details!.Should().Contain("not found");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckCopyPrivateBasePropertyWhenFallbackToBaseTypesIsEnabled()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckBasePrivatePropertyTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.StructCopy.BasePrivateProperty.Fallback.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-struct-copy-base-private-property-fallback.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy-base-private-property-fallback.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy-base-private-property-fallback.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(TestDuckStructCopyBasePrivatePropertyFallbackProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckBasePrivatePropertyTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.StructCopy.BasePrivateProperty.Fallback",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-StructCopy-BasePrivateProperty-Fallback", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var copyObject = DuckType.Create(
                    typeof(TestDuckStructCopyBasePrivatePropertyFallbackProxy),
                    new TestDuckBasePrivatePropertyTarget(41, 5));
                copyObject.Should().NotBeNull();

                var hiddenField = typeof(TestDuckStructCopyBasePrivatePropertyFallbackProxy)
                                 .GetField(nameof(TestDuckStructCopyBasePrivatePropertyFallbackProxy.Hidden));
                hiddenField.Should().NotBeNull();
                hiddenField!.GetValue(copyObject).Should().Be(41);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldReportDuckCopyPrivateBaseFieldWithoutFallbackAsMissingTargetMember()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckBasePrivateFieldTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.StructCopy.BasePrivateField.NoFallback.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-struct-copy-base-private-field-no-fallback.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy-base-private-field-no-fallback.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy-base-private-field-no-fallback.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(TestDuckStructCopyBasePrivateFieldNoFallbackProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckBasePrivateFieldTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.StructCopy.BasePrivateField.NoFallback",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle();

            var mapping = matrix.Mappings[0];
            mapping.Status.Should().BeOneOf(
                DuckTypeAotCompatibilityStatuses.MissingTargetMethod,
                DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature);
            mapping.Details.Should().NotBeNull();
            mapping.Details!.Should().Contain("not found");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckCopyPrivateBaseFieldWhenFallbackToBaseTypesIsEnabled()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckBasePrivateFieldTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.StructCopy.BasePrivateField.Fallback.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-struct-copy-base-private-field-fallback.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy-base-private-field-fallback.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy-base-private-field-fallback.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(TestDuckStructCopyBasePrivateFieldFallbackProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckBasePrivateFieldTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.StructCopy.BasePrivateField.Fallback",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-StructCopy-BasePrivateField-Fallback", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var copyObject = DuckType.Create(
                    typeof(TestDuckStructCopyBasePrivateFieldFallbackProxy),
                    new TestDuckBasePrivateFieldTarget(59));
                copyObject.Should().NotBeNull();

                var hiddenField = typeof(TestDuckStructCopyBasePrivateFieldFallbackProxy)
                                 .GetField(nameof(TestDuckStructCopyBasePrivateFieldFallbackProxy.Hidden));
                hiddenField.Should().NotBeNull();
                hiddenField!.GetValue(copyObject).Should().Be(59);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckCopyPrivateBaseMembersWithDeclaredOnlyFallback()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.StructCopy.BasePrivateDeclaredOnly.Fallback.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-struct-copy-base-private-declared-only-fallback.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy-base-private-declared-only-fallback.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy-base-private-declared-only-fallback.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    CreateMappingDocumentEntry(typeof(TestDuckStructCopyBasePrivatePropertyDeclaredOnlyFallbackProxy), typeof(TestDuckBasePrivatePropertyTarget)),
                    CreateMappingDocumentEntry(typeof(TestDuckStructCopyBasePrivateFieldDeclaredOnlyFallbackProxy), typeof(TestDuckBasePrivateFieldTarget))
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.StructCopy.BasePrivateDeclaredOnly.Fallback",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            AssertMappingDiagnostic(matrix!, typeof(TestDuckStructCopyBasePrivatePropertyDeclaredOnlyFallbackProxy), typeof(TestDuckBasePrivatePropertyTarget), DuckTypeAotCompatibilityStatuses.Compatible, null);
            AssertMappingDiagnostic(matrix!, typeof(TestDuckStructCopyBasePrivateFieldDeclaredOnlyFallbackProxy), typeof(TestDuckBasePrivateFieldTarget), DuckTypeAotCompatibilityStatuses.Compatible, null);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-StructCopy-BasePrivateDeclaredOnly-Fallback", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var propertyCopyObject = DuckType.Create(
                    typeof(TestDuckStructCopyBasePrivatePropertyDeclaredOnlyFallbackProxy),
                    new TestDuckBasePrivatePropertyTarget(31, 5));
                propertyCopyObject.Should().NotBeNull();

                var propertyHiddenField = typeof(TestDuckStructCopyBasePrivatePropertyDeclaredOnlyFallbackProxy)
                                         .GetField(nameof(TestDuckStructCopyBasePrivatePropertyDeclaredOnlyFallbackProxy.Hidden));
                propertyHiddenField.Should().NotBeNull();
                propertyHiddenField!.GetValue(propertyCopyObject).Should().Be(31);

                var fieldCopyObject = DuckType.Create(
                    typeof(TestDuckStructCopyBasePrivateFieldDeclaredOnlyFallbackProxy),
                    new TestDuckBasePrivateFieldTarget(47));
                fieldCopyObject.Should().NotBeNull();

                var fieldHiddenField = typeof(TestDuckStructCopyBasePrivateFieldDeclaredOnlyFallbackProxy)
                                      .GetField(nameof(TestDuckStructCopyBasePrivateFieldDeclaredOnlyFallbackProxy.Hidden));
                fieldHiddenField.Should().NotBeNull();
                fieldHiddenField!.GetValue(fieldCopyObject).Should().Be(47);
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
    public void GenerateProcessorShouldPreferPropertyForDuckPropertyOrFieldWhenPropertyExists()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckPropertyPreferredTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.PropertyPreferred.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-property-preferred.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-property-preferred.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-property-preferred.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckPropertyPreferredProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckPropertyPreferredTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.PropertyPreferred",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-PropertyPreferred", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckPropertyPreferredTarget();
                targetInstance.ReadFieldValue().Should().Be("field");
                targetInstance.ReadPropertyValue().Should().Be("property");

                var generatedInstance = constructor!.Invoke([targetInstance]);
                var getValueMethod = generatedProxyType.GetMethod("get_Value", Type.EmptyTypes);
                getValueMethod.Should().NotBeNull();
                var beforeValue = getValueMethod!.Invoke(generatedInstance, Array.Empty<object>());
                beforeValue.Should().Be("property");

                var setValueMethod = generatedProxyType.GetMethod("set_Value", new[] { typeof(string) });
                setValueMethod.Should().NotBeNull();
                _ = setValueMethod!.Invoke(generatedInstance, new object?[] { "after" });

                targetInstance.ReadPropertyValue().Should().Be("after");
                targetInstance.ReadFieldValue().Should().Be("field");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportStaticMethodBindings()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckStaticMethodTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.StaticMethod.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-static-method.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-static-method.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-static-method.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckStaticMethodProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckStaticMethodTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.StaticMethod",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-StaticMethod", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckStaticMethodTarget();
                var generatedInstance = constructor!.Invoke([targetInstance]);
                var echoMethod = generatedProxyType.GetMethod(nameof(ITestDuckStaticMethodProxy.Echo), [typeof(string)]);
                echoMethod.Should().NotBeNull();
                var echoResult = echoMethod!.Invoke(generatedInstance, ["hello"]);
                echoResult.Should().Be("static:hello");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportStaticClassTargetWithNullCreateInstance()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            TestDuckStaticClassTarget.ResetValue("initial");
            var sharedAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var sharedAssemblyName = AssemblyName.GetAssemblyName(sharedAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.StaticClass.NullTarget.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-static-class-null-target.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-static-class-null-target.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-static-class-null-target.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    CreateMappingDocumentEntry(typeof(ITestDuckStaticClassProxy), typeof(TestDuckStaticClassTarget))
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { sharedAssemblyPath },
                targetAssemblies: new[] { sharedAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.StaticClass.NullTarget",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-StaticClass-NullTarget", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var createTypeResult = DuckType.GetOrCreateProxyType(typeof(ITestDuckStaticClassProxy), typeof(TestDuckStaticClassTarget));
                createTypeResult.Success.Should().BeTrue();

                var proxy = createTypeResult.CreateInstance<ITestDuckStaticClassProxy>(null);
                proxy.Echo("hello").Should().Be("static-class:hello");
                proxy.Value.Should().Be("initial");

                proxy.Value = "after";
                proxy.Value.Should().Be("after");
                TestDuckStaticClassTarget.ReadValue().Should().Be("after");
            }
            finally
            {
                DuckType.ResetRuntimeModeForTests();
                DuckTypeAotEngine.ResetForTests();
                loadContext.Unload();
                TestDuckStaticClassTarget.ResetValue("initial");
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportStaticPropertyBindings()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            TestDuckStaticPropertyTarget.ResetValue("initial");
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckStaticPropertyTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.StaticProperty.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-static-property.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-static-property.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-static-property.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckStaticPropertyProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckStaticPropertyTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.StaticProperty",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-StaticProperty", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckStaticPropertyTarget();
                var generatedInstance = constructor!.Invoke([targetInstance]);
                var getValueMethod = generatedProxyType.GetMethod("get_Value", Type.EmptyTypes);
                getValueMethod.Should().NotBeNull();
                var beforeValue = getValueMethod!.Invoke(generatedInstance, Array.Empty<object>());
                beforeValue.Should().Be("initial");

                var setValueMethod = generatedProxyType.GetMethod("set_Value", new[] { typeof(string) });
                setValueMethod.Should().NotBeNull();
                _ = setValueMethod!.Invoke(generatedInstance, new object?[] { "after" });
                var afterValue = getValueMethod.Invoke(generatedInstance, Array.Empty<object>());
                afterValue.Should().Be("after");
                TestDuckStaticPropertyTarget.ReadValue().Should().Be("after");
            }
            finally
            {
                loadContext.Unload();
                TestDuckStaticPropertyTarget.ResetValue("initial");
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportStaticPropertyValueWithTypeBindings()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            TestDuckStaticPropertyTarget.ResetValue("initial");
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckStaticPropertyTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.StaticProperty.ValueWithType.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-static-property-value-with-type.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-static-property-value-with-type.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-static-property-value-with-type.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckStaticPropertyValueWithTypeProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckStaticPropertyTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.StaticProperty.ValueWithType",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-StaticProperty-ValueWithType", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckStaticPropertyTarget();
                var generatedInstance = constructor!.Invoke([targetInstance]);

                var getValueMethod = generatedProxyType.GetMethod("get_Value", Type.EmptyTypes);
                getValueMethod.Should().NotBeNull();
                var beforeValueObject = getValueMethod!.Invoke(generatedInstance, Array.Empty<object>());
                beforeValueObject.Should().NotBeNull();
                beforeValueObject.Should().BeOfType<ValueWithType<string>>();
                var beforeValue = (ValueWithType<string>)beforeValueObject!;
                beforeValue.Value.Should().Be("initial");
                beforeValue.Type.Should().Be(typeof(string));

                var setValueMethod = generatedProxyType.GetMethod("set_Value", new[] { typeof(ValueWithType<string>) });
                setValueMethod.Should().NotBeNull();
                _ = setValueMethod!.Invoke(generatedInstance, [ValueWithType<string>.Create("after", typeof(string))]);

                var afterValueObject = getValueMethod.Invoke(generatedInstance, Array.Empty<object>());
                afterValueObject.Should().NotBeNull();
                afterValueObject.Should().BeOfType<ValueWithType<string>>();
                var afterValue = (ValueWithType<string>)afterValueObject!;
                afterValue.Value.Should().Be("after");
                afterValue.Type.Should().Be(typeof(string));
                TestDuckStaticPropertyTarget.ReadValue().Should().Be("after");
            }
            finally
            {
                loadContext.Unload();
                TestDuckStaticPropertyTarget.ResetValue("initial");
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportRefAndOutMethodBindings()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckByRefTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ByRef.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-byref.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-byref.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-byref.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckByRefProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckByRefTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ByRef",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ByRef", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var generatedInstance = constructor!.Invoke([new TestDuckByRefTarget()]);
                var mutateMethod = generatedProxyType.GetMethod(nameof(ITestDuckByRefProxy.Mutate), new[] { typeof(int).MakeByRefType(), typeof(int).MakeByRefType() });
                mutateMethod.Should().NotBeNull();

                object?[] arguments = [3, null];
                _ = mutateMethod!.Invoke(generatedInstance, arguments);
                arguments[0].Should().Be(4);
                arguments[1].Should().Be(8);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldIgnoreInRefDirectionForMethodSelectionParity()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(InRefDirectionTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ByRef.InDirection.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-byref-in-direction.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-byref-in-direction.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-byref-in-direction.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    CreateMappingDocumentEntry(typeof(IInRefDirectionProxy), typeof(InRefDirectionTarget))
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ByRef.InDirection",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ByRef-InDirection", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var proxy = DuckType.Create<IInRefDirectionProxy>(new InRefDirectionTarget());
                var value = 4;
                proxy!.Mutate(ref value);
                value.Should().Be(5);
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
    public void GenerateProcessorShouldSupportRefAndOutMethodConversions()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckByRefReverseConversionTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ByRef.Conversion.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-byref-conversion.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-byref-conversion.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-byref-conversion.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckByRefConversionInnerProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckByRefConversionInnerTarget).FullName,
                        targetAssembly = targetAssemblyName
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckByRefConversionProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckByRefConversionTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ByRef.Conversion",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().HaveCount(2);
            matrix.Mappings.Should().OnlyContain(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var generatedOuterProxyType = generatedModule.Types.Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.Interfaces.Any(interfaceImpl => string.Equals(interfaceImpl.Interface.FullName, typeof(ITestDuckByRefConversionProxy).FullName, StringComparison.Ordinal)));

                var roundtripMethod = generatedOuterProxyType.FindMethod(nameof(ITestDuckByRefConversionProxy.RoundtripInner));
                roundtripMethod.Should().NotBeNull();
                roundtripMethod!.Body.Should().NotBeNull();
                roundtripMethod.Body!.Instructions.Should().Contain(instruction => instruction.OpCode == OpCodes.Ldobj);
                roundtripMethod.Body!.Instructions.Should().Contain(instruction => instruction.OpCode == OpCodes.Stobj);

                var incrementMethod = generatedOuterProxyType.FindMethod(nameof(ITestDuckByRefConversionProxy.Increment));
                incrementMethod.Should().NotBeNull();
                incrementMethod!.Body.Should().NotBeNull();
                incrementMethod.Body!.Instructions.Should().Contain(instruction => instruction.OpCode == OpCodes.Unbox_Any);
                incrementMethod.Body!.Instructions.Should().Contain(instruction => instruction.OpCode == OpCodes.Box);

                var getNumberMethod = generatedOuterProxyType.FindMethod(nameof(ITestDuckByRefConversionProxy.GetNumber));
                getNumberMethod.Should().NotBeNull();
                getNumberMethod!.Body.Should().NotBeNull();
                getNumberMethod.Body!.Instructions.Should().Contain(instruction => instruction.OpCode == OpCodes.Box);
                getNumberMethod.Body!.Instructions.Should().Contain(instruction => instruction.OpCode == OpCodes.Stobj);
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ByRef-Conversion", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedOuterProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckByRefConversionProxy).FullName, StringComparison.Ordinal)));

                var constructor = GetDuckProxyConstructor(generatedOuterProxyType);
                constructor.Should().NotBeNull();

                var generatedInstance = constructor!.Invoke([new TestDuckByRefConversionTarget()]);

                var tryGetInnerMethod = generatedOuterProxyType.GetMethod(
                    nameof(ITestDuckByRefConversionProxy.TryGetInner),
                    new[] { typeof(ITestDuckByRefConversionInnerProxy).MakeByRefType() });
                tryGetInnerMethod.Should().NotBeNull();
                object?[] tryGetInnerArguments = [null];
                var tryGetInnerResult = tryGetInnerMethod!.Invoke(generatedInstance, tryGetInnerArguments);
                tryGetInnerResult.Should().Be(true);
                tryGetInnerArguments[0].Should().NotBeNull();
                var outInnerProxy = tryGetInnerArguments[0];
                outInnerProxy.Should().BeAssignableTo<ITestDuckByRefConversionInnerProxy>();
                ((ITestDuckByRefConversionInnerProxy)outInnerProxy!).Name.Should().Be("from-out");
                ((IDuckType)outInnerProxy).Instance.Should().BeOfType<TestDuckByRefConversionInnerTarget>();

                var roundtripInnerMethod = generatedOuterProxyType.GetMethod(
                    nameof(ITestDuckByRefConversionProxy.RoundtripInner),
                    new[] { typeof(ITestDuckByRefConversionInnerProxy).MakeByRefType() });
                roundtripInnerMethod.Should().NotBeNull();
                object?[] roundtripArguments = [outInnerProxy];
                var roundtripResult = roundtripInnerMethod!.Invoke(generatedInstance, roundtripArguments);
                roundtripResult.Should().Be(true);
                roundtripArguments[0].Should().NotBeNull();
                var roundtripInnerProxy = roundtripArguments[0];
                roundtripInnerProxy.Should().BeAssignableTo<ITestDuckByRefConversionInnerProxy>();
                ((ITestDuckByRefConversionInnerProxy)roundtripInnerProxy!).Name.Should().Be("from-out-roundtrip");
                ((IDuckType)roundtripInnerProxy).Instance.Should().BeOfType<TestDuckByRefConversionInnerTarget>();

                var incrementMethod = generatedOuterProxyType.GetMethod(
                    nameof(ITestDuckByRefConversionProxy.Increment),
                    new[] { typeof(object).MakeByRefType() });
                incrementMethod.Should().NotBeNull();
                object?[] incrementArguments = [5];
                _ = incrementMethod!.Invoke(generatedInstance, incrementArguments);
                incrementArguments[0].Should().Be(6);

                var getNumberMethod = generatedOuterProxyType.GetMethod(
                    nameof(ITestDuckByRefConversionProxy.GetNumber),
                    new[] { typeof(object).MakeByRefType() });
                getNumberMethod.Should().NotBeNull();
                object?[] getNumberArguments = [null];
                _ = getNumberMethod!.Invoke(generatedInstance, getNumberArguments);
                getNumberArguments[0].Should().Be(42);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportNonByRefMethodTypeConversions()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckTypeConversionTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.TypeConversion.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-type-conversion.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-type-conversion.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-type-conversion.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckTypeConversionProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckTypeConversionTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.TypeConversion",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var generatedProxyType = generatedModule.Types.Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.Interfaces.Any(interfaceImpl => string.Equals(interfaceImpl.Interface.FullName, typeof(ITestDuckTypeConversionProxy).FullName, StringComparison.Ordinal)));

                var addOneMethod = generatedProxyType.FindMethod(nameof(ITestDuckTypeConversionProxy.AddOne));
                addOneMethod.Should().NotBeNull();
                addOneMethod!.Body.Should().NotBeNull();
                addOneMethod.Body!.Instructions.Should().Contain(instruction => instruction.OpCode == OpCodes.Unbox_Any);

                var readNumberMethod = generatedProxyType.FindMethod(nameof(ITestDuckTypeConversionProxy.ReadNumber));
                readNumberMethod.Should().NotBeNull();
                readNumberMethod!.Body.Should().NotBeNull();
                readNumberMethod.Body!.Instructions.Should().Contain(instruction => instruction.OpCode == OpCodes.Box);

                var echoStringMethod = generatedProxyType.FindMethod(nameof(ITestDuckTypeConversionProxy.EchoString));
                echoStringMethod.Should().NotBeNull();
                echoStringMethod!.Body.Should().NotBeNull();
                echoStringMethod.Body!.Instructions.Should().Contain(instruction => instruction.OpCode == OpCodes.Castclass);

                var parseEnumMethod = generatedProxyType.FindMethod(nameof(ITestDuckTypeConversionProxy.ParseEnum));
                parseEnumMethod.Should().NotBeNull();
                parseEnumMethod!.Body.Should().NotBeNull();
                parseEnumMethod.Body!.Instructions.Should().Contain(instruction => instruction.OpCode == OpCodes.Unbox_Any);

                var echoEnumObjectMethod = generatedProxyType.FindMethod(nameof(ITestDuckTypeConversionProxy.EchoEnumObject));
                echoEnumObjectMethod.Should().NotBeNull();
                echoEnumObjectMethod!.Body.Should().NotBeNull();
                echoEnumObjectMethod.Body!.Instructions.Should().Contain(instruction => instruction.OpCode == OpCodes.Box);

                var readEnumComparableMethod = generatedProxyType.FindMethod(nameof(ITestDuckTypeConversionProxy.ReadEnumComparable));
                readEnumComparableMethod.Should().NotBeNull();
                readEnumComparableMethod!.Body.Should().NotBeNull();
                readEnumComparableMethod.Body!.Instructions.Should().Contain(instruction => instruction.OpCode == OpCodes.Box);
                readEnumComparableMethod.Body!.Instructions.Should().Contain(instruction => instruction.OpCode == OpCodes.Castclass);
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-TypeConversion", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckTypeConversionProxy).FullName, StringComparison.Ordinal)));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var generatedInstance = constructor!.Invoke([new TestDuckTypeConversionTarget()]);

                var addOneMethod = generatedProxyType.GetMethod(nameof(ITestDuckTypeConversionProxy.AddOne), [typeof(object)]);
                addOneMethod.Should().NotBeNull();
                var addOneResult = addOneMethod!.Invoke(generatedInstance, [4]);
                addOneResult.Should().Be(5);

                var readNumberMethod = generatedProxyType.GetMethod(nameof(ITestDuckTypeConversionProxy.ReadNumber), Type.EmptyTypes);
                readNumberMethod.Should().NotBeNull();
                var readNumberResult = readNumberMethod!.Invoke(generatedInstance, Array.Empty<object>());
                readNumberResult.Should().Be(42);
                readNumberResult.Should().BeOfType<int>();

                var echoStringMethod = generatedProxyType.GetMethod(nameof(ITestDuckTypeConversionProxy.EchoString), [typeof(object)]);
                echoStringMethod.Should().NotBeNull();
                var echoStringResult = echoStringMethod!.Invoke(generatedInstance, ["alpha"]);
                echoStringResult.Should().Be("alpha");

                var echoObjectMethod = generatedProxyType.GetMethod(nameof(ITestDuckTypeConversionProxy.EchoObject), Type.EmptyTypes);
                echoObjectMethod.Should().NotBeNull();
                var echoObjectResult = echoObjectMethod!.Invoke(generatedInstance, Array.Empty<object>());
                echoObjectResult.Should().Be("text");

                var parseEnumMethod = generatedProxyType.GetMethod(nameof(ITestDuckTypeConversionProxy.ParseEnum), [typeof(object)]);
                parseEnumMethod.Should().NotBeNull();
                var parseEnumResult = parseEnumMethod!.Invoke(generatedInstance, [DayOfWeek.Wednesday]);
                parseEnumResult.Should().Be(DayOfWeek.Wednesday);

                Action parseEnumInvalid = () => _ = parseEnumMethod.Invoke(generatedInstance, ["not-an-enum"]);
                parseEnumInvalid.Should().Throw<TargetInvocationException>()
                                .WithInnerException<InvalidCastException>();

                var echoEnumObjectMethod = generatedProxyType.GetMethod(nameof(ITestDuckTypeConversionProxy.EchoEnumObject), [typeof(DayOfWeek)]);
                echoEnumObjectMethod.Should().NotBeNull();
                var echoEnumObjectResult = echoEnumObjectMethod!.Invoke(generatedInstance, [DayOfWeek.Monday]);
                echoEnumObjectResult.Should().Be(DayOfWeek.Monday);
                echoEnumObjectResult.Should().BeOfType<DayOfWeek>();

                var readEnumComparableMethod = generatedProxyType.GetMethod(nameof(ITestDuckTypeConversionProxy.ReadEnumComparable), Type.EmptyTypes);
                readEnumComparableMethod.Should().NotBeNull();
                var readEnumComparableResult = readEnumComparableMethod!.Invoke(generatedInstance, Array.Empty<object>());
                readEnumComparableResult.Should().Be(DayOfWeek.Friday);
                readEnumComparableResult.Should().BeAssignableTo<IComparable>();
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportReverseRefAndOutMethodConversions()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckByRefConversionTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.Reverse.ByRef.Conversion.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-reverse-byref-conversion.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-reverse-byref-conversion.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-reverse-byref-conversion.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckByRefReverseConversionInnerProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckByRefReverseConversionInnerTarget).FullName,
                        targetAssembly = targetAssemblyName
                    },
                    new
                    {
                        mode = "reverse",
                        proxyType = typeof(ITestDuckByRefReverseConversionProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckByRefReverseConversionTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.Reverse.ByRef.Conversion",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().HaveCount(2);
            matrix.Mappings.Should().OnlyContain(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-Reverse-ByRef-Conversion", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var reverseProxy = DuckType.CreateReverse(typeof(ITestDuckByRefReverseConversionProxy), new TestDuckByRefReverseConversionTarget());
                reverseProxy.Should().NotBeNull();

                var proxyType = typeof(ITestDuckByRefReverseConversionProxy);
                var tryGetInnerMethod = proxyType.GetMethod(
                    nameof(ITestDuckByRefReverseConversionProxy.TryGetInner),
                    new[] { typeof(ITestDuckByRefReverseConversionInnerProxy).MakeByRefType() });
                tryGetInnerMethod.Should().NotBeNull();
                object?[] tryGetInnerArguments = [null];
                var tryGetInnerResult = tryGetInnerMethod!.Invoke(reverseProxy, tryGetInnerArguments);
                tryGetInnerResult.Should().Be(true);
                tryGetInnerArguments[0].Should().NotBeNull();
                var outInnerProxy = tryGetInnerArguments[0];
                outInnerProxy.Should().BeAssignableTo<ITestDuckByRefReverseConversionInnerProxy>();
                ((ITestDuckByRefReverseConversionInnerProxy)outInnerProxy!).Name.Should().Be("from-out");

                var roundtripInnerMethod = proxyType.GetMethod(
                    nameof(ITestDuckByRefReverseConversionProxy.RoundtripInner),
                    new[] { typeof(ITestDuckByRefReverseConversionInnerProxy).MakeByRefType() });
                roundtripInnerMethod.Should().NotBeNull();
                object?[] roundtripArguments = [outInnerProxy];
                var roundtripResult = roundtripInnerMethod!.Invoke(reverseProxy, roundtripArguments);
                roundtripResult.Should().Be(true);
                roundtripArguments[0].Should().NotBeNull();
                var roundtripInnerProxy = roundtripArguments[0];
                roundtripInnerProxy.Should().BeAssignableTo<ITestDuckByRefReverseConversionInnerProxy>();
                ((ITestDuckByRefReverseConversionInnerProxy)roundtripInnerProxy!).Name.Should().Be("from-out-roundtrip");

                var incrementMethod = proxyType.GetMethod(
                    nameof(ITestDuckByRefReverseConversionProxy.Increment),
                    new[] { typeof(object).MakeByRefType() });
                incrementMethod.Should().NotBeNull();
                object?[] incrementArguments = [5];
                _ = incrementMethod!.Invoke(reverseProxy, incrementArguments);
                incrementArguments[0].Should().Be(6);

                var getNumberMethod = proxyType.GetMethod(
                    nameof(ITestDuckByRefReverseConversionProxy.GetNumber),
                    new[] { typeof(object).MakeByRefType() });
                getNumberMethod.Should().NotBeNull();
                object?[] getNumberArguments = [null];
                _ = getNumberMethod!.Invoke(reverseProxy, getNumberArguments);
                getNumberArguments[0].Should().Be(42);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportValueWithTypeMethodConversions()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckValueWithTypeTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ValueWithType.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-value-with-type.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-value-with-type.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-value-with-type.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckValueWithTypeProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckValueWithTypeTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ValueWithType",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ValueWithType", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetInstance = new TestDuckValueWithTypeTarget();
                var generatedInstance = constructor!.Invoke([targetInstance]);

                var consumeMethod = generatedProxyType.GetMethod(nameof(ITestDuckValueWithTypeProxy.Consume), [typeof(ValueWithType<string>)]);
                consumeMethod.Should().NotBeNull();
                var consumeResult = consumeMethod!.Invoke(generatedInstance, [ValueWithType<string>.Create("input", typeof(string))]);
                consumeResult.Should().Be("consume:input");
                targetInstance.LastConsumed.Should().Be("input");

                var produceMethod = generatedProxyType.GetMethod(nameof(ITestDuckValueWithTypeProxy.Produce), [typeof(string)]);
                produceMethod.Should().NotBeNull();
                var wrappedResultObject = produceMethod!.Invoke(generatedInstance, ["abc"]);
                wrappedResultObject.Should().NotBeNull();
                wrappedResultObject.Should().BeOfType<ValueWithType<string>>();
                var wrappedResult = (ValueWithType<string>)wrappedResultObject!;
                wrappedResult.Value.Should().Be("produce:abc");
                wrappedResult.Type.Should().Be(typeof(string));
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckChainingMethodConversions()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckChainTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.DuckChain.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-duck-chain.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-duck-chain.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-duck-chain.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckChainInnerProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckChainInnerTarget).FullName,
                        targetAssembly = targetAssemblyName
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckChainProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckChainTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.DuckChain",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var generatedOuterProxyType = generatedModule.Types.Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.Interfaces.Any(interfaceImpl => string.Equals(interfaceImpl.Interface.FullName, typeof(ITestDuckChainProxy).FullName, StringComparison.Ordinal)));

                var roundtripMethod = generatedOuterProxyType.FindMethod(nameof(ITestDuckChainProxy.Roundtrip));
                roundtripMethod.Should().NotBeNull();
                roundtripMethod!.Body.Should().NotBeNull();

                var emitsDuckTypeInstanceExtraction = roundtripMethod.Body!.Instructions.Any(instruction =>
                {
                    var method = instruction.Operand as IMethod;
                    return instruction.OpCode == OpCodes.Callvirt
                        && method is not null
                        && string.Equals(method.Name, "get_Instance", StringComparison.Ordinal)
                        && string.Equals(method.DeclaringType.FullName, typeof(IDuckType).FullName, StringComparison.Ordinal);
                });
                emitsDuckTypeInstanceExtraction.Should().BeTrue();

                var emitsCreateCacheCreate = roundtripMethod.Body.Instructions.Any(instruction =>
                {
                    var method = instruction.Operand as IMethod;
                    return instruction.OpCode == OpCodes.Call
                        && method is not null
                        && string.Equals(method.Name, "Create", StringComparison.Ordinal)
                        && method.DeclaringType.FullName.IndexOf("Datadog.Trace.DuckTyping.DuckType/CreateCache`1", StringComparison.Ordinal) >= 0;
                });
                emitsCreateCacheCreate.Should().BeTrue();

                var emitsLegacyDuckTypeCreate = roundtripMethod.Body.Instructions.Any(instruction =>
                {
                    var method = instruction.Operand as IMethod;
                    return instruction.OpCode == OpCodes.Call
                        && method is not null
                        && string.Equals(method.Name, nameof(DuckType.Create), StringComparison.Ordinal)
                        && string.Equals(method.DeclaringType.FullName, typeof(DuckType).FullName, StringComparison.Ordinal);
                });
                emitsLegacyDuckTypeCreate.Should().BeFalse();
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-DuckChain", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyTypes = generatedAssembly.GetTypes()
                                                           .Where(type => string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal))
                                                           .ToArray();

                var generatedOuterProxyType = generatedProxyTypes.Single(type =>
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckChainProxy).FullName, StringComparison.Ordinal)));
                var generatedInnerProxyType = generatedProxyTypes.Single(type =>
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckChainInnerProxy).FullName, StringComparison.Ordinal)));

                var outerConstructor = GetDuckProxyConstructor(generatedOuterProxyType);
                var innerConstructor = GetDuckProxyConstructor(generatedInnerProxyType);
                outerConstructor.Should().NotBeNull();
                innerConstructor.Should().NotBeNull();

                var outerTarget = new TestDuckChainTarget();
                var innerTarget = new TestDuckChainInnerTarget("first");
                var outerProxyInstance = outerConstructor!.Invoke([outerTarget]);
                var innerProxyInstance = innerConstructor!.Invoke([innerTarget]);

                var roundtripMethod = generatedOuterProxyType.GetMethods().Single(method =>
                    string.Equals(method.Name, nameof(ITestDuckChainProxy.Roundtrip), StringComparison.Ordinal));
                var roundtripResult = roundtripMethod.Invoke(outerProxyInstance, [innerProxyInstance]);
                roundtripResult.Should().NotBeNull();
                outerTarget.LastReceived.Should().BeSameAs(innerTarget);
                roundtripResult.Should().BeAssignableTo<IDuckType>();
                ((IDuckType)roundtripResult!).Instance.Should().BeSameAs(innerTarget);

                var nameMethod = roundtripResult.GetType().GetMethod("get_Name", Type.EmptyTypes);
                nameMethod.Should().NotBeNull();
                var roundtripName = nameMethod!.Invoke(roundtripResult, Array.Empty<object>());
                roundtripName.Should().Be("first");

                var createMethod = generatedOuterProxyType.GetMethods().Single(method =>
                    string.Equals(method.Name, nameof(ITestDuckChainProxy.Create), StringComparison.Ordinal));
                var createResult = createMethod.Invoke(outerProxyInstance, ["second"]);
                createResult.Should().NotBeNull();
                createResult.Should().BeAssignableTo<IDuckType>();
                var createResultDuckType = (IDuckType)createResult!;
                createResultDuckType.Instance.Should().BeOfType<TestDuckChainInnerTarget>();
                var createdInnerTarget = createResultDuckType.Instance as TestDuckChainInnerTarget;
                createdInnerTarget.Should().NotBeNull();
                createdInnerTarget!.Name.Should().Be("second");

                var createNullMethod = generatedOuterProxyType.GetMethods().Single(method =>
                    string.Equals(method.Name, nameof(ITestDuckChainProxy.CreateNull), StringComparison.Ordinal));
                var createNullResult = createNullMethod.Invoke(outerProxyInstance, Array.Empty<object>());
                createNullResult.Should().BeNull();
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldNotPreserveNullForStandardDuckChainMethodArguments()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(NonByRefDuckChainNullTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.DuckChain.NullStandardArgument.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-duck-chain-null-standard-argument.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-duck-chain-null-standard-argument.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-duck-chain-null-standard-argument.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    CreateMappingDocumentEntry(typeof(INonByRefDuckChainNullProxy), typeof(NonByRefDuckChainNullTarget))
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.DuckChain.NullStandardArgument",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-DuckChain-NullStandardArgument", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var proxy = DuckType.Create<INonByRefDuckChainNullProxy>(new NonByRefDuckChainNullTarget());
                Action callWithNull = () => proxy!.Read(null!);
                callWithNull.Should().Throw<NullReferenceException>();
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
    public void GenerateProcessorShouldPreserveNullForByRefDuckChainMethodArguments()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(ByRefDuckChainNullTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.DuckChain.NullByRefArgument.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-duck-chain-null-byref-argument.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-duck-chain-null-byref-argument.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-duck-chain-null-byref-argument.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    CreateMappingDocumentEntry(typeof(IByRefDuckChainNullProxy), typeof(ByRefDuckChainNullTarget))
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.DuckChain.NullByRefArgument",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-DuckChain-NullByRefArgument", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var proxy = DuckType.Create<IByRefDuckChainNullProxy>(new ByRefDuckChainNullTarget());
                IByRefDuckChainNullInnerProxy? value = null;
                proxy!.IsNullPreserved(ref value).Should().BeTrue();
                value.Should().BeNull();
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
    public void GenerateProcessorShouldSupportNullableDuckCopyMethodReturnConversions()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckChainTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.DuckChain.Nullable.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-duck-chain-nullable.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-duck-chain-nullable.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-duck-chain-nullable.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(TestDuckNullableInnerProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckChainInnerTarget).FullName,
                        targetAssembly = targetAssemblyName
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckNullableChainProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckChainTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.DuckChain.Nullable",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().OnlyContain(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var generatedOuterProxyType = generatedModule.Types.Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.Interfaces.Any(interfaceImpl => string.Equals(interfaceImpl.Interface.FullName, typeof(ITestDuckNullableChainProxy).FullName, StringComparison.Ordinal)));

                var createMethod = generatedOuterProxyType.FindMethod(nameof(ITestDuckNullableChainProxy.Create));
                createMethod.Should().NotBeNull();
                createMethod!.Body.Should().NotBeNull();
                var usesNullableCtor = createMethod.Body!.Instructions.Any(instruction =>
                    instruction.OpCode == OpCodes.Newobj &&
                    instruction.Operand is IMethod method &&
                    string.Equals(method.Name, ".ctor", StringComparison.Ordinal) &&
                    string.Equals(method.DeclaringType.FullName, $"System.Nullable`1<{typeof(TestDuckNullableInnerProxy).FullName}>", StringComparison.Ordinal));
                usesNullableCtor.Should().BeTrue();
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-DuckChain-Nullable", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var contextTestAssembly = loadContext.LoadFromAssemblyPath(proxyAssemblyPath);

                var generatedOuterProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckNullableChainProxy).FullName, StringComparison.Ordinal)));

                var targetType = contextTestAssembly.GetType(typeof(TestDuckChainTarget).FullName!, throwOnError: true)!;
                var targetInstance = Activator.CreateInstance(targetType);
                targetInstance.Should().NotBeNull();

                var constructor = GetDuckProxyConstructor(generatedOuterProxyType);
                constructor.Should().NotBeNull();
                var outerProxyInstance = constructor!.Invoke([targetInstance]);

                var createMethod = generatedOuterProxyType.GetMethod(nameof(ITestDuckNullableChainProxy.Create), [typeof(string)]);
                createMethod.Should().NotBeNull();
                var createResult = createMethod!.Invoke(outerProxyInstance, ["nullable"]);
                createResult.Should().NotBeNull();
                createResult!.GetType().FullName.Should().Be(typeof(TestDuckNullableInnerProxy).FullName);
                var nameField = createResult.GetType().GetField(nameof(TestDuckNullableInnerProxy.Name));
                nameField.Should().NotBeNull();
                nameField!.GetValue(createResult).Should().Be("nullable");

                var createNullMethod = generatedOuterProxyType.GetMethod(nameof(ITestDuckNullableChainProxy.CreateNull), Type.EmptyTypes);
                createNullMethod.Should().NotBeNull();
                var createNullResult = createNullMethod!.Invoke(outerProxyInstance, Array.Empty<object>());
                createNullResult.Should().BeNull();
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportGenericMethodBindings()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckGenericMethodTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.GenericMethod.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-generic-method.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-generic-method.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-generic-method.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckGenericMethodProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckGenericMethodTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.GenericMethod",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var generatedProxyType = generatedModule.Types.Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.Interfaces.Any(interfaceImpl => string.Equals(interfaceImpl.Interface.FullName, typeof(ITestDuckGenericMethodProxy).FullName, StringComparison.Ordinal)));
                var echoMethod = generatedProxyType.FindMethod(nameof(ITestDuckGenericMethodProxy.Echo));
                echoMethod.Should().NotBeNull();
                echoMethod!.MethodSig.GenParamCount.Should().Be(1);
                echoMethod.GenericParameters.Should().HaveCount(1);

                echoMethod.Body.Should().NotBeNull();
                var genericCallInstruction = echoMethod.Body!.Instructions.FirstOrDefault(instruction =>
                    (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                    && instruction.Operand is MethodSpec methodSpec
                    && string.Equals(methodSpec.Method.Name, nameof(ITestDuckGenericMethodProxy.Echo), StringComparison.Ordinal));
                genericCallInstruction.Should().NotBeNull();

                var methodSpec = (MethodSpec)genericCallInstruction!.Operand!;
                methodSpec.Instantiation.Should().NotBeNull();
                methodSpec.Instantiation.Should().BeOfType<GenericInstMethodSig>();
                var methodInstantiation = (GenericInstMethodSig)methodSpec.Instantiation!;
                methodInstantiation.GenericArguments.Should().HaveCount(1);
                methodInstantiation.GenericArguments[0].Should().BeOfType<GenericMVar>();
                ((GenericMVar)methodInstantiation.GenericArguments[0]).Number.Should().Be(0);
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-GenericMethod", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckGenericMethodProxy).FullName, StringComparison.Ordinal)));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var proxyInstance = constructor!.Invoke([new TestDuckGenericMethodTarget()]);
                var echoMethod = generatedProxyType.GetMethods().Single(method =>
                    string.Equals(method.Name, nameof(ITestDuckGenericMethodProxy.Echo), StringComparison.Ordinal) &&
                    method.IsGenericMethodDefinition);

                var echoIntResult = echoMethod.MakeGenericMethod(typeof(int)).Invoke(proxyInstance, [42]);
                echoIntResult.Should().Be(42);

                var echoStringResult = echoMethod.MakeGenericMethod(typeof(string)).Invoke(proxyInstance, ["hello"]);
                echoStringResult.Should().Be("hello");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldResolveDuckGenericParameterTypeNamesFromTargetAssemblyPaths()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var (targetAssemblyPath, dependencyAssemblyPath) = CreateGenericParameterTypeNameAssemblies(tempDirectory);
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.GenericParameterTypeNames.External.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-generic-parameter-type-names-external.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-generic-parameter-type-names-external.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-generic-parameter-type-names-external.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(IExternalGenericParameterTypeNameProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = "ExternalGenericArgs.GenericParameterTypeNameTarget",
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: new[] { Path.GetDirectoryName(targetAssemblyPath)!, Path.GetDirectoryName(dependencyAssemblyPath)! },
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.GenericParameterTypeNames.External",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-GenericParameterTypeNames-External", isCollectible: true);
            try
            {
                _ = loadContext.LoadFromAssemblyPath(dependencyAssemblyPath);
                var targetAssembly = loadContext.LoadFromAssemblyPath(targetAssemblyPath);
                var targetType = targetAssembly.GetType("ExternalGenericArgs.GenericParameterTypeNameTarget");
                targetType.Should().NotBeNull();
                var targetInstance = Activator.CreateInstance(targetType!);
                targetInstance.Should().NotBeNull();

                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var proxy = DuckType.Create(typeof(IExternalGenericParameterTypeNameProxy), targetInstance!);
                proxy.Should().BeAssignableTo<IExternalGenericParameterTypeNameProxy>();
                ((IExternalGenericParameterTypeNameProxy)proxy!).Resolve().Should().Be("ExternalGenericArgs.GenericArgument");
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
    public void GenerateProcessorShouldPreserveGenericMethodConstraintFlags()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckGenericConstraintMethodTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.GenericMethod.Constraint.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-generic-method-constraint.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-generic-method-constraint.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-generic-method-constraint.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckGenericConstraintMethodProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckGenericConstraintMethodTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.GenericMethod.Constraint",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var generatedProxyType = generatedModule.Types.Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.Interfaces.Any(interfaceImpl => string.Equals(interfaceImpl.Interface.FullName, typeof(ITestDuckGenericConstraintMethodProxy).FullName, StringComparison.Ordinal)));

                var echoMethod = generatedProxyType.FindMethod(nameof(ITestDuckGenericConstraintMethodProxy.EchoClass));
                echoMethod.Should().NotBeNull();
                echoMethod!.GenericParameters.Should().HaveCount(1);
                echoMethod.GenericParameters[0].Flags.HasFlag(GenericParamAttributes.ReferenceTypeConstraint).Should().BeTrue();
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-GenericMethod-Constraint", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckGenericConstraintMethodProxy).FullName, StringComparison.Ordinal)));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var proxyInstance = constructor!.Invoke([new TestDuckGenericConstraintMethodTarget()]);
                var echoMethod = generatedProxyType.GetMethods().Single(method =>
                    string.Equals(method.Name, nameof(ITestDuckGenericConstraintMethodProxy.EchoClass), StringComparison.Ordinal) &&
                    method.IsGenericMethodDefinition);
                var result = echoMethod.MakeGenericMethod(typeof(string)).Invoke(proxyInstance, ["constraint"]);
                result.Should().Be("constraint");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportValueTypeTargets()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckStructTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ValueTypeTarget.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-value-type-target.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-value-type-target.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-value-type-target.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckStructTargetProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckStructTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ValueTypeTarget",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ValueTypeTarget", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var contextTestAssembly = loadContext.LoadFromAssemblyPath(proxyAssemblyPath);

                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckStructTargetProxy).FullName, StringComparison.Ordinal)));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetType = contextTestAssembly.GetType(typeof(TestDuckStructTarget).FullName!, throwOnError: true)!;
                var targetCtor = targetType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: [typeof(int)], modifiers: null);
                targetCtor.Should().NotBeNull();
                var targetInstance = targetCtor!.Invoke([7]);

                var proxyInstance = constructor!.Invoke([targetInstance]);
                var addMethod = generatedProxyType.GetMethod(nameof(ITestDuckStructTargetProxy.Add), [typeof(int)]);
                addMethod.Should().NotBeNull();
                var value = addMethod!.Invoke(proxyInstance, [5]);
                value.Should().Be(12);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldReportFieldAccessorsOnValueTypeTargetsAsIncompatible()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckStructFieldTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ValueTypeTarget.Field.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-value-type-target-field.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-value-type-target-field.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-value-type-target-field.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckFieldProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckStructFieldTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ValueTypeTarget.Field",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            var mapping = matrix!.Mappings.Should().ContainSingle().Subject;
            mapping.Status.Should().Be(DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature);
            mapping.Details.Should().Contain("belongs to value type");
            mapping.Details.Should().Contain(typeof(TestDuckStructFieldTarget).FullName!);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldEmitConstrainedCallvirtForInterfaceLikeValueTypeMethods()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckStructInterfaceMethodTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ValueTypeTarget.InterfaceMethod.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-value-type-target-interface-method.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-value-type-target-interface-method.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-value-type-target-interface-method.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckStructInterfaceMethodProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckStructInterfaceMethodTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ValueTypeTarget.InterfaceMethod",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var generatedProxyType = generatedModule.Types.Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.Interfaces.Any(interfaceImpl => string.Equals(interfaceImpl.Interface.FullName, typeof(ITestDuckStructInterfaceMethodProxy).FullName, StringComparison.Ordinal)));
                var addMethod = generatedProxyType.FindMethod(nameof(ITestDuckStructInterfaceMethodProxy.Add));
                addMethod.Should().NotBeNull();
                addMethod!.Body.Should().NotBeNull();

                var hasConstrained = addMethod.Body!.Instructions.Any(instruction =>
                    instruction.OpCode == OpCodes.Constrained &&
                    instruction.Operand is ITypeDefOrRef type &&
                    string.Equals(type.FullName, typeof(TestDuckStructInterfaceMethodTarget).FullName, StringComparison.Ordinal));
                hasConstrained.Should().BeTrue();

                var hasCallvirt = addMethod.Body.Instructions.Any(instruction =>
                    instruction.OpCode == OpCodes.Callvirt &&
                    instruction.Operand is IMethod method &&
                    string.Equals(method.Name, nameof(ITestDuckStructInterfaceMethodProxy.Add), StringComparison.Ordinal));
                hasCallvirt.Should().BeTrue();
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ValueTypeTarget-InterfaceMethod", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var contextTestAssembly = loadContext.LoadFromAssemblyPath(proxyAssemblyPath);

                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckStructInterfaceMethodProxy).FullName, StringComparison.Ordinal)));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetType = contextTestAssembly.GetType(typeof(TestDuckStructInterfaceMethodTarget).FullName!, throwOnError: true)!;
                var targetCtor = targetType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: [typeof(int)], modifiers: null);
                targetCtor.Should().NotBeNull();
                var targetInstance = targetCtor!.Invoke([10]);

                var proxyInstance = constructor!.Invoke([targetInstance]);
                var addMethod = generatedProxyType.GetMethod(nameof(ITestDuckStructInterfaceMethodProxy.Add), [typeof(int)]);
                addMethod.Should().NotBeNull();
                var value = addMethod!.Invoke(proxyInstance, [5]);
                value.Should().Be(15);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldBoxValueTypeForIDuckTypeInstanceAndUseValueTypeToString()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckStructToStringTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.ValueTypeTarget.ToString.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-value-type-target-tostring.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-value-type-target-tostring.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-value-type-target-tostring.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(ITestDuckStructToStringProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckStructToStringTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.ValueTypeTarget.ToString",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var generatedProxyType = generatedModule.Types.Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.Interfaces.Any(interfaceImpl => string.Equals(interfaceImpl.Interface.FullName, typeof(ITestDuckStructToStringProxy).FullName, StringComparison.Ordinal)));

                var getInstanceMethod = generatedProxyType.FindMethod("get_Instance");
                getInstanceMethod.Should().NotBeNull();
                getInstanceMethod!.Body.Should().NotBeNull();
                var hasBox = getInstanceMethod.Body!.Instructions.Any(instruction =>
                    instruction.OpCode == OpCodes.Box &&
                    instruction.Operand is ITypeDefOrRef type &&
                    string.Equals(type.FullName, typeof(TestDuckStructToStringTarget).FullName, StringComparison.Ordinal));
                hasBox.Should().BeTrue();
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ValueTypeTarget-ToString", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var contextTestAssembly = loadContext.LoadFromAssemblyPath(proxyAssemblyPath);

                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckStructToStringProxy).FullName, StringComparison.Ordinal)));
                var constructor = GetDuckProxyConstructor(generatedProxyType);
                constructor.Should().NotBeNull();

                var targetType = contextTestAssembly.GetType(typeof(TestDuckStructToStringTarget).FullName!, throwOnError: true)!;
                var targetCtor = targetType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: [typeof(string)], modifiers: null);
                targetCtor.Should().NotBeNull();
                var targetInstance = targetCtor!.Invoke(["seed"]);

                var proxyInstance = constructor!.Invoke([targetInstance]);
                proxyInstance.Should().NotBeNull();
                proxyInstance!.ToString().Should().Be("struct:seed");

                var echoMethod = generatedProxyType.GetMethod(nameof(ITestDuckStructToStringProxy.EchoLength), [typeof(string)]);
                echoMethod.Should().NotBeNull();
                var lengthValue = echoMethod!.Invoke(proxyInstance, ["abc"]);
                lengthValue.Should().Be(7);

                proxyInstance.Should().BeAssignableTo<IDuckType>();
                var duckType = (IDuckType)proxyInstance;
                duckType.Instance.Should().NotBeNull();
                duckType.Instance!.GetType().FullName.Should().Be(typeof(TestDuckStructToStringTarget).FullName);
                duckType.Instance.ToString().Should().Be("struct:seed");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckCopyStructProxies()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckStructCopyTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.StructCopy.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-struct-copy.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(TestDuckStructCopyProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckStructCopyTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.StructCopy",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var bootstrapType = generatedModule.Find("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap", isReflectionName: false);
                bootstrapType.Should().NotBeNull();
                var activatorMethod = bootstrapType!.Methods.Single(method =>
                    method.Name.StartsWith("CreateProxy_", StringComparison.Ordinal));
                activatorMethod.Body.Should().NotBeNull();
                var boxesStructCopyProxy = activatorMethod.Body!.Instructions.Any(instruction =>
                    instruction.OpCode == OpCodes.Box &&
                    instruction.Operand is ITypeDefOrRef type &&
                    string.Equals(type.FullName, typeof(TestDuckStructCopyProxy).FullName, StringComparison.Ordinal));
                boxesStructCopyProxy.Should().BeFalse();
                activatorMethod.MethodSig.Params.Should().ContainSingle();
                activatorMethod.MethodSig.Params[0].FullName.Should().Be(typeof(TestDuckStructCopyTarget).FullName);
                activatorMethod.Body!.Instructions.Should().NotContain(instruction => instruction.OpCode == OpCodes.Castclass || instruction.OpCode == OpCodes.Unbox_Any);
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-StructCopy", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var copyObject = DuckType.Create(typeof(TestDuckStructCopyProxy), new TestDuckStructCopyTarget());
                copyObject.Should().NotBeNull();

                var nameField = typeof(TestDuckStructCopyProxy).GetField(nameof(TestDuckStructCopyProxy.Name));
                var countField = typeof(TestDuckStructCopyProxy).GetField(nameof(TestDuckStructCopyProxy.Count));
                nameField.Should().NotBeNull();
                countField.Should().NotBeNull();

                nameField!.GetValue(copyObject).Should().Be("alpha");
                countField!.GetValue(copyObject).Should().Be(42);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldSupportDuckCopyStructWithNullableNestedDuckCopy()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckNullableStructCopyTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.StructCopy.NullableNested.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-struct-copy-nullable-nested.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy-nullable-nested.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy-nullable-nested.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(TestDuckNullableInnerProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckChainInnerTarget).FullName,
                        targetAssembly = targetAssemblyName
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(TestDuckNullableStructCopyProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckNullableStructCopyTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.StructCopy.NullableNested",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().OnlyContain(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-StructCopy-NullableNested", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var nullCopyObject = DuckType.Create(typeof(TestDuckNullableStructCopyProxy), new TestDuckNullableStructCopyTarget(value: null));
                nullCopyObject.Should().NotBeNull();
                var nullCopy = (TestDuckNullableStructCopyProxy)nullCopyObject!;
                nullCopy.Value.Should().BeNull();

                var valueCopyObject = DuckType.Create(typeof(TestDuckNullableStructCopyProxy), new TestDuckNullableStructCopyTarget(new TestDuckChainInnerTarget("beta")));
                valueCopyObject.Should().NotBeNull();
                var valueCopy = (TestDuckNullableStructCopyProxy)valueCopyObject!;
                valueCopy.Value.Should().NotBeNull();
                valueCopy.Value!.Value.Name.Should().Be("beta");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldBoxNullableValueTypeBeforeDuckChainCreateCallInStructCopy()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetAssemblyPath = typeof(TestDuckNullableValueTypeStructCopyTarget).Assembly.Location;
            var proxyAssemblyName = AssemblyName.GetAssemblyName(proxyAssemblyPath).Name;
            var targetAssemblyName = AssemblyName.GetAssemblyName(targetAssemblyPath).Name;

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.StructCopy.NullableValueType.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-struct-copy-nullable-value-type.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy-nullable-value-type.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-struct-copy-nullable-value-type.props");

            var mapDocument = new
            {
                mappings = new[]
                {
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(TestDuckNullableInnerProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckNullableValueTypeInnerTarget).FullName,
                        targetAssembly = targetAssemblyName
                    },
                    new
                    {
                        mode = "forward",
                        proxyType = typeof(TestDuckNullableValueTypeStructCopyProxy).FullName,
                        proxyAssembly = proxyAssemblyName,
                        targetType = typeof(TestDuckNullableValueTypeStructCopyTarget).FullName,
                        targetAssembly = targetAssemblyName
                    }
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { targetAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.StructCopy.NullableValueType",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().OnlyContain(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var bootstrapType = generatedModule.Find("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap", isReflectionName: false);
                bootstrapType.Should().NotBeNull();

                var activatorMethod = bootstrapType!.Methods.Single(method =>
                    method.Name.StartsWith("CreateProxy_", StringComparison.Ordinal) &&
                    method.MethodSig.Params.Count == 1 &&
                    string.Equals(method.MethodSig.Params[0].FullName, typeof(TestDuckNullableValueTypeStructCopyTarget).FullName, StringComparison.Ordinal));

                activatorMethod.Body.Should().NotBeNull();
                var instructions = activatorMethod.Body!.Instructions.ToList();

                var callIndex = instructions.FindIndex(instruction =>
                    instruction.OpCode == OpCodes.Call &&
                    instruction.Operand is IMethod method &&
                    method.MethodSig.Params.Count == 1 &&
                    method.MethodSig.Params[0].ElementType == ElementType.Object);

                callIndex.Should().BeGreaterThanOrEqualTo(0);

                var previousInstruction = instructions.Take(callIndex).Last(instruction => instruction.OpCode != OpCodes.Nop);
                previousInstruction.OpCode.Should().Be(OpCodes.Box);
                previousInstruction.Operand.Should().BeAssignableTo<ITypeDefOrRef>();
                var boxedType = ((ITypeDefOrRef)previousInstruction.Operand!).FullName;
                boxedType.Should().Contain("System.Nullable`1");
                boxedType.Should().Contain(typeof(TestDuckNullableValueTypeInnerTarget).FullName);
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-StructCopy-NullableValueType", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                var nullCopyObject = DuckType.Create(typeof(TestDuckNullableValueTypeStructCopyProxy), new TestDuckNullableValueTypeStructCopyTarget(value: null));
                nullCopyObject.Should().NotBeNull();
                var nullCopy = (TestDuckNullableValueTypeStructCopyProxy)nullCopyObject!;
                nullCopy.Value.Name.Should().BeNull();

                var valueCopyObject = DuckType.Create(
                    typeof(TestDuckNullableValueTypeStructCopyProxy),
                    new TestDuckNullableValueTypeStructCopyTarget(new TestDuckNullableValueTypeInnerTarget("gamma")));
                valueCopyObject.Should().NotBeNull();
                var valueCopy = (TestDuckNullableValueTypeStructCopyProxy)valueCopyObject!;
                valueCopy.Value.Name.Should().Be("gamma");
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldDiscoverMappingsWhenEnabled()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var targetFolderPath = Path.GetDirectoryName(typeof(TestDuckTarget).Assembly.Location);
            targetFolderPath.Should().NotBeNullOrWhiteSpace();

            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.DiscoverInGenerate.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-discover-in-generate.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-discover-in-generate.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-discover-in-generate.props");

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: Array.Empty<string>(),
                targetFolders: new[] { targetFolderPath! },
                targetFilters: new[] { Path.GetFileName(proxyAssemblyPath) },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.DiscoverInGenerate",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath,
                discoverMappings: true);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            File.Exists(outputPath).Should().BeTrue();
            File.Exists(mapFilePath).Should().BeTrue();

            var parseResult = DuckTypeAotMapFileParser.Parse(mapFilePath);
            parseResult.Errors.Should().BeEmpty();
            parseResult.Mappings.Should().NotBeEmpty();
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void GenerateProcessorShouldKeepCompatibilityDiagnosticsWhileReplayingExactRuntimeFailures()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var proxyAssemblyPath = typeof(DuckTypeAotProcessorsTests).Assembly.Location;
            var outputPath = Path.Combine(tempDirectory, "Datadog.Trace.DuckType.AotRegistry.FailureReplayParity.dll");
            var mapFilePath = Path.Combine(tempDirectory, "ducktype-aot-map-failure-replay-parity.json");
            var trimmerDescriptorPath = Path.Combine(tempDirectory, "ducktype-aot-failure-replay-parity.linker.xml");
            var propsPath = Path.Combine(tempDirectory, "ducktype-aot-failure-replay-parity.props");

            var mapDocument = new
            {
                mappings = new object[]
                {
                    CreateMappingDocumentEntry(typeof(IFailureReplayPropertyCantBeReadProxy), typeof(FailureReplayPropertyCantBeReadTarget)),
                    CreateMappingDocumentEntry(typeof(IFailureReplayPropertyArgumentsLengthProxy), typeof(FailureReplayPropertyArgumentsLengthTarget)),
                    CreateMappingDocumentEntry(typeof(IFailureReplayPropertyOrFieldNotFoundProxy), typeof(FailureReplayPropertyOrFieldNotFoundTarget)),
                    CreateMappingDocumentEntry(typeof(IFailureReplayProxyMethodParameterMissingProxy), typeof(FailureReplayProxyMethodParameterMissingTarget)),
                    CreateMappingDocumentEntry(typeof(IFailureReplayInvalidTypeConversionProxy), typeof(FailureReplayInvalidTypeConversionTarget)),
                    CreateMappingDocumentEntry(typeof(IFailureReplayReverseGenericProxy), typeof(FailureReplayReverseGenericTarget), mode: "reverse"),
                    CreateMappingDocumentEntry(typeof(IFailureReplayReverseAttributeMismatchProxy), typeof(FailureReplayReverseAttributeMismatchTarget), mode: "reverse"),
                    CreateMappingDocumentEntry(typeof(IFailureReplayReverseMissingMethodProxy), typeof(FailureReplayReverseMissingMethodTarget), mode: "reverse"),
                    CreateMappingDocumentEntry(typeof(IFailureReplayReverseMissingPropertyProxy), typeof(FailureReplayReverseMissingPropertyTarget), mode: "reverse"),
                    CreateMappingDocumentEntry(typeof(FailureReplayEmptyDuckCopyProxy), typeof(FailureReplayEmptyDuckCopyTarget)),
                    CreateMappingDocumentEntry(typeof(IFailureReplayReverseAbstractImplementorProxy), typeof(FailureReplayReverseAbstractImplementorTarget), mode: "reverse"),
                    CreateMappingDocumentEntry(typeof(IFailureReplayReverseInterfaceImplementorProxy), typeof(IFailureReplayReverseInterfaceImplementorTarget), mode: "reverse"),
                    CreateMappingDocumentEntry(typeof(IFailureReplayReverseNamedArgumentAttributeProxy), typeof(FailureReplayReverseNamedArgumentAttributeTarget), mode: "reverse"),
                }
            };
            File.WriteAllText(mapFilePath, JsonConvert.SerializeObject(mapDocument, Formatting.Indented));

            var options = new DuckTypeAotGenerateOptions(
                proxyAssemblies: new[] { proxyAssemblyPath },
                targetAssemblies: new[] { proxyAssemblyPath },
                targetFolders: Array.Empty<string>(),
                targetFilters: new[] { "*.dll" },
                mapFile: mapFilePath,
                mappingCatalog: null,
                genericInstantiationsFile: null,
                outputPath: outputPath,
                assemblyName: "Datadog.Trace.DuckType.AotRegistry.FailureReplayParity",
                trimmerDescriptorPath: trimmerDescriptorPath,
                propsPath: propsPath);

            var exitCode = DuckTypeAotGenerateProcessor.Process(options);
            exitCode.Should().Be(0);

            var compatibilityMatrixPath = $"{outputPath}.compat.json";
            var matrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            matrix.Should().NotBeNull();
            matrix!.Mappings.Should().HaveCount(13);

            AssertMappingDiagnostic(matrix, typeof(IFailureReplayPropertyCantBeReadProxy), typeof(FailureReplayPropertyCantBeReadTarget), DuckTypeAotCompatibilityStatuses.MissingTargetMethod, "DTAOT0207");
            AssertMappingDiagnostic(matrix, typeof(IFailureReplayPropertyArgumentsLengthProxy), typeof(FailureReplayPropertyArgumentsLengthTarget), DuckTypeAotCompatibilityStatuses.MissingTargetMethod, "DTAOT0207");
            AssertMappingDiagnostic(matrix, typeof(IFailureReplayPropertyOrFieldNotFoundProxy), typeof(FailureReplayPropertyOrFieldNotFoundTarget), DuckTypeAotCompatibilityStatuses.MissingTargetMethod, "DTAOT0207");
            AssertMappingDiagnostic(matrix, typeof(IFailureReplayProxyMethodParameterMissingProxy), typeof(FailureReplayProxyMethodParameterMissingTarget), DuckTypeAotCompatibilityStatuses.MissingTargetMethod, "DTAOT0207");
            AssertMappingDiagnostic(matrix, typeof(IFailureReplayInvalidTypeConversionProxy), typeof(FailureReplayInvalidTypeConversionTarget), DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature, "DTAOT0209");
            AssertMappingDiagnostic(matrix, typeof(IFailureReplayReverseGenericProxy), typeof(FailureReplayReverseGenericTarget), DuckTypeAotCompatibilityStatuses.MissingTargetMethod, "DTAOT0207");
            AssertMappingDiagnostic(matrix, typeof(IFailureReplayReverseAttributeMismatchProxy), typeof(FailureReplayReverseAttributeMismatchTarget), DuckTypeAotCompatibilityStatuses.MissingTargetMethod, "DTAOT0207");
            AssertMappingDiagnostic(matrix, typeof(IFailureReplayReverseMissingMethodProxy), typeof(FailureReplayReverseMissingMethodTarget), DuckTypeAotCompatibilityStatuses.MissingTargetMethod, "DTAOT0207");
            AssertMappingDiagnostic(matrix, typeof(IFailureReplayReverseMissingPropertyProxy), typeof(FailureReplayReverseMissingPropertyTarget), DuckTypeAotCompatibilityStatuses.MissingTargetMethod, "DTAOT0207");
            AssertMappingDiagnostic(matrix, typeof(FailureReplayEmptyDuckCopyProxy), typeof(FailureReplayEmptyDuckCopyTarget), DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature, "DTAOT0209");
            AssertMappingDiagnostic(matrix, typeof(IFailureReplayReverseAbstractImplementorProxy), typeof(FailureReplayReverseAbstractImplementorTarget), DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature, "DTAOT0209");
            AssertMappingDiagnostic(matrix, typeof(IFailureReplayReverseInterfaceImplementorProxy), typeof(IFailureReplayReverseInterfaceImplementorTarget), DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature, "DTAOT0209");
            AssertMappingDiagnostic(matrix, typeof(IFailureReplayReverseNamedArgumentAttributeProxy), typeof(FailureReplayReverseNamedArgumentAttributeTarget), DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature, "DTAOT0214");

            using (var generatedModule = ModuleDefMD.Load(outputPath))
            {
                var bootstrapTypeDef = generatedModule.Find("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap", isReflectionName: false);
                bootstrapTypeDef.Should().NotBeNull();
                var bootstrapInstructions = bootstrapTypeDef!.Methods
                                                            .Where(method => method.Body is not null)
                                                            .SelectMany(method => method.Body!.Instructions)
                                                            .ToList();
                AssertBootstrapDoesNotUseReflectionForGeneratedRegistrations(bootstrapInstructions);

                var failureRegistrationCalls = bootstrapInstructions
                                              .Where(instruction =>
                                                   instruction.OpCode == OpCodes.Call &&
                                                   instruction.Operand is IMethod method &&
                                                   (string.Equals(method.Name, "RegisterAotProxyFailure", StringComparison.Ordinal) ||
                                                    string.Equals(method.Name, "RegisterAotReverseProxyFailure", StringComparison.Ordinal)))
                                              .Select(instruction => (IMethod)instruction.Operand)
                                              .ToList();
                failureRegistrationCalls.Should().NotBeEmpty();
                failureRegistrationCalls.Should().OnlyContain(method => method.MethodSig.Params.Last().FullName == "System.Action");
                AssertDirectDelegateRegistrationCalls(bootstrapTypeDef, "RegisterAotProxyFailure", "ThrowFailure_", "System.Action");
                AssertDirectDelegateRegistrationCalls(bootstrapTypeDef, "RegisterAotReverseProxyFailure", "ThrowFailure_", "System.Action");

                bootstrapInstructions.Any(
                    instruction =>
                        instruction.OpCode == OpCodes.Ldtoken &&
                        instruction.Operand is IMethod method &&
                        method.Name.StartsWith("ThrowFailure_", StringComparison.Ordinal))
                                     .Should()
                                     .BeFalse("generated failure registrations should not resolve throwers from RuntimeMethodHandle");
                bootstrapInstructions.Any(
                    instruction =>
                        instruction.OpCode == OpCodes.Ldftn &&
                        instruction.Operand is IMethod method &&
                        method.Name.StartsWith("ThrowFailure_", StringComparison.Ordinal))
                                     .Should()
                                     .BeTrue();
                bootstrapInstructions.Any(
                    instruction =>
                        instruction.OpCode == OpCodes.Newobj &&
                        instruction.Operand is IMethod method &&
                        string.Equals(method.DeclaringType.FullName, "System.Action", StringComparison.Ordinal))
                                     .Should()
                                     .BeTrue();
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-FailureReplayParity", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var bootstrapType = generatedAssembly.GetType("Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap");
                bootstrapType.Should().NotBeNull();
                var initializeMethod = bootstrapType!.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initializeMethod.Should().NotBeNull();
                _ = initializeMethod!.Invoke(obj: null, parameters: null);

                AssertThrowsExactDuckTypeFailure<DuckTypePropertyCantBeReadException>(() => DuckType.Create(typeof(IFailureReplayPropertyCantBeReadProxy), new FailureReplayPropertyCantBeReadTarget()));
                AssertThrowsExactDuckTypeFailure<DuckTypePropertyArgumentsLengthException>(() => DuckType.Create(typeof(IFailureReplayPropertyArgumentsLengthProxy), new FailureReplayPropertyArgumentsLengthTarget()));
                AssertThrowsExactDuckTypeFailure<DuckTypePropertyOrFieldNotFoundException>(() => DuckType.Create(typeof(IFailureReplayPropertyOrFieldNotFoundProxy), new FailureReplayPropertyOrFieldNotFoundTarget()));
                AssertThrowsExactDuckTypeFailure<DuckTypeProxyMethodParameterIsMissingException>(() => DuckType.Create(typeof(IFailureReplayProxyMethodParameterMissingProxy), new FailureReplayProxyMethodParameterMissingTarget()));
                AssertThrowsExactDuckTypeFailure<DuckTypeInvalidTypeConversionException>(() => DuckType.Create(typeof(IFailureReplayInvalidTypeConversionProxy), new FailureReplayInvalidTypeConversionTarget()));
                AssertThrowsExactDuckTypeFailure<DuckTypeReverseProxyMustImplementGenericMethodAsGenericException>(() => DuckType.CreateReverse(typeof(IFailureReplayReverseGenericProxy), new FailureReplayReverseGenericTarget()));
                AssertThrowsExactDuckTypeFailure<DuckTypeReverseAttributeParameterNamesMismatchException>(() => DuckType.CreateReverse(typeof(IFailureReplayReverseAttributeMismatchProxy), new FailureReplayReverseAttributeMismatchTarget()));
                AssertThrowsExactDuckTypeFailure<DuckTypeReverseProxyMissingMethodImplementationException>(() => DuckType.CreateReverse(typeof(IFailureReplayReverseMissingMethodProxy), new FailureReplayReverseMissingMethodTarget()));
                AssertThrowsExactDuckTypeFailure<DuckTypeReverseProxyMissingPropertyImplementationException>(() => DuckType.CreateReverse(typeof(IFailureReplayReverseMissingPropertyProxy), new FailureReplayReverseMissingPropertyTarget()));
                AssertThrowsExactDuckTypeFailure<DuckTypeDuckCopyStructDoesNotContainsAnyField>(() => DuckType.Create(typeof(FailureReplayEmptyDuckCopyProxy), new FailureReplayEmptyDuckCopyTarget()));
                AssertThrowsExactDuckTypeFailure<DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException>(() => _ = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IFailureReplayReverseAbstractImplementorProxy), typeof(FailureReplayReverseAbstractImplementorTarget)).ProxyType);
                AssertThrowsExactDuckTypeFailure<DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException>(() => _ = DuckTypeAotEngine.GetOrCreateReverseProxyType(typeof(IFailureReplayReverseInterfaceImplementorProxy), typeof(IFailureReplayReverseInterfaceImplementorTarget)).ProxyType);
                AssertThrowsExactDuckTypeFailure<DuckTypeCustomAttributeHasNamedArgumentsException>(() => DuckType.CreateReverse(typeof(IFailureReplayReverseNamedArgumentAttributeProxy), new FailureReplayReverseNamedArgumentAttributeTarget()));
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "dd-trace-ducktype-aot-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static string CreateDuplicateAssignableTargetAssembly(string tempDirectory, string assemblyName)
    {
        var projectDirectory = Path.Combine(tempDirectory, assemblyName);
        Directory.CreateDirectory(projectDirectory);

        var projectPath = Path.Combine(projectDirectory, $"{assemblyName}.csproj");
        var projectContents =
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 <TargetFramework>net8.0</TargetFramework>
                 <AssemblyName>{assemblyName}</AssemblyName>
                 <ImplicitUsings>disable</ImplicitUsings>
                 <Nullable>enable</Nullable>
               </PropertyGroup>
             </Project>
             """;
        File.WriteAllText(projectPath, projectContents);

        var targetsContents =
            """
            namespace Duplicate.Targets
            {
                public class SharedBaseTarget
                {
                    public SharedBaseTarget(string value)
                    {
                        Value = value;
                    }

                    public virtual string Value { get; }
                }

                public class SharedDerivedTarget : SharedBaseTarget
                {
                    public SharedDerivedTarget(string value)
                        : base(value)
                    {
                    }
                }
            }
            """;
        File.WriteAllText(Path.Combine(projectDirectory, "Targets.cs"), targetsContents);

        var buildResult = RunProcess(
            "dotnet",
            projectDirectory,
            timeoutMilliseconds: 120_000,
            captureOutput: true,
            "build",
            projectPath,
            "-c",
            "Release",
            "-f",
            "net8.0",
            "/nologo");
        buildResult.ExitCode.Should().Be(
            0,
            $"temporary duplicate target assembly should build.{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StandardError}");

        var assemblyPath = Path.Combine(projectDirectory, "bin", "Release", "net8.0", $"{assemblyName}.dll");
        File.Exists(assemblyPath).Should().BeTrue($"temporary duplicate target assembly should exist at '{assemblyPath}'");
        return assemblyPath;
    }

    private static string CreateConstantOnlyOptionalTargetAssembly(string tempDirectory)
    {
        const string assemblyName = "DuckTypeAotConstantOnlyOptionalTarget";
        var projectDirectory = Path.Combine(tempDirectory, assemblyName);
        Directory.CreateDirectory(projectDirectory);

        var projectPath = Path.Combine(projectDirectory, $"{assemblyName}.csproj");
        var projectContents =
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 <TargetFramework>net8.0</TargetFramework>
                 <AssemblyName>{assemblyName}</AssemblyName>
                 <ImplicitUsings>disable</ImplicitUsings>
                 <Nullable>enable</Nullable>
               </PropertyGroup>
             </Project>
             """;
        File.WriteAllText(projectPath, projectContents);

        var targetContents =
            """
            namespace ExternalOptional
            {
                public sealed class ConstantOnlyOptionalParameterTarget
                {
                    public int Add(int value, int optional = 7)
                    {
                        return value + optional;
                    }
                }
            }
            """;
        File.WriteAllText(Path.Combine(projectDirectory, "Targets.cs"), targetContents);

        var buildResult = RunProcess(
            "dotnet",
            projectDirectory,
            timeoutMilliseconds: 120_000,
            captureOutput: true,
            "build",
            projectPath,
            "-c",
            "Release",
            "-f",
            "net8.0",
            "/nologo");
        buildResult.ExitCode.Should().Be(
            0,
            $"temporary constant-only optional target assembly should build.{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StandardError}");

        var assemblyPath = Path.Combine(projectDirectory, "bin", "Release", "net8.0", $"{assemblyName}.dll");
        var mutatedAssemblyPath = Path.Combine(projectDirectory, "bin", "Release", "net8.0", $"{assemblyName}.mutated.dll");
        using (var module = ModuleDefMD.Load(assemblyPath))
        {
            var targetType = module.Find("ExternalOptional.ConstantOnlyOptionalParameterTarget", isReflectionName: false);
            targetType.Should().NotBeNull();
            var addMethod = targetType!.FindMethod("Add");
            addMethod.Should().NotBeNull();
            var trailingParameter = addMethod!.Parameters.Single(parameter => parameter.MethodSigIndex == 1);
            trailingParameter.ParamDef.Should().NotBeNull();
            trailingParameter.ParamDef!.Constant.Should().NotBeNull();
            trailingParameter.ParamDef.Attributes &= ~dnlib.DotNet.ParamAttributes.Optional;
            module.Write(mutatedAssemblyPath);
        }

        File.Exists(mutatedAssemblyPath).Should().BeTrue($"temporary mutated target assembly should exist at '{mutatedAssemblyPath}'");
        return mutatedAssemblyPath;
    }

    private static (string TargetAssemblyPath, string DependencyAssemblyPath) CreateGenericParameterTypeNameAssemblies(string tempDirectory)
    {
        const string dependencyAssemblyName = "ExternalGenericArgs";
        var dependencyProjectDirectory = Path.Combine(tempDirectory, dependencyAssemblyName);
        Directory.CreateDirectory(dependencyProjectDirectory);

        var dependencyProjectPath = Path.Combine(dependencyProjectDirectory, $"{dependencyAssemblyName}.csproj");
        var dependencyProjectContents =
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 <TargetFramework>net8.0</TargetFramework>
                 <AssemblyName>{dependencyAssemblyName}</AssemblyName>
                 <ImplicitUsings>disable</ImplicitUsings>
                 <Nullable>enable</Nullable>
               </PropertyGroup>
             </Project>
             """;
        File.WriteAllText(dependencyProjectPath, dependencyProjectContents);

        var dependencyContents =
            """
            namespace ExternalGenericArgs
            {
                public sealed class GenericArgument
                {
                }
            }
            """;
        File.WriteAllText(Path.Combine(dependencyProjectDirectory, "Types.cs"), dependencyContents);

        var dependencyBuildResult = RunProcess(
            "dotnet",
            dependencyProjectDirectory,
            timeoutMilliseconds: 120_000,
            captureOutput: true,
            "build",
            dependencyProjectPath,
            "-c",
            "Release",
            "-f",
            "net8.0",
            "/nologo");
        dependencyBuildResult.ExitCode.Should().Be(
            0,
            $"temporary generic-parameter dependency assembly should build.{Environment.NewLine}STDOUT:{Environment.NewLine}{dependencyBuildResult.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{dependencyBuildResult.StandardError}");

        const string targetAssemblyName = "ExternalGenericTarget";
        var targetProjectDirectory = Path.Combine(tempDirectory, targetAssemblyName);
        Directory.CreateDirectory(targetProjectDirectory);

        var targetProjectPath = Path.Combine(targetProjectDirectory, $"{targetAssemblyName}.csproj");
        var targetProjectContents =
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 <TargetFramework>net8.0</TargetFramework>
                 <AssemblyName>{targetAssemblyName}</AssemblyName>
                 <ImplicitUsings>disable</ImplicitUsings>
                 <Nullable>enable</Nullable>
               </PropertyGroup>
             </Project>
             """;
        File.WriteAllText(targetProjectPath, targetProjectContents);

        var targetContents =
            """
            namespace ExternalGenericArgs
            {
                public sealed class GenericParameterTypeNameTarget
                {
                    public string Resolve<T>()
                    {
                        return typeof(T).FullName!;
                    }
                }
            }
            """;
        File.WriteAllText(Path.Combine(targetProjectDirectory, "Targets.cs"), targetContents);

        var targetBuildResult = RunProcess(
            "dotnet",
            targetProjectDirectory,
            timeoutMilliseconds: 120_000,
            captureOutput: true,
            "build",
            targetProjectPath,
            "-c",
            "Release",
            "-f",
            "net8.0",
            "/nologo");
        targetBuildResult.ExitCode.Should().Be(
            0,
            $"temporary generic-parameter target assembly should build.{Environment.NewLine}STDOUT:{Environment.NewLine}{targetBuildResult.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{targetBuildResult.StandardError}");

        var targetAssemblyPath = Path.Combine(targetProjectDirectory, "bin", "Release", "net8.0", $"{targetAssemblyName}.dll");
        var dependencyAssemblyPath = Path.Combine(dependencyProjectDirectory, "bin", "Release", "net8.0", $"{dependencyAssemblyName}.dll");
        File.Exists(targetAssemblyPath).Should().BeTrue($"temporary generic-parameter target assembly should exist at '{targetAssemblyPath}'");
        File.Exists(dependencyAssemblyPath).Should().BeTrue($"temporary generic-parameter dependency assembly should exist at '{dependencyAssemblyPath}'");
        return (targetAssemblyPath, dependencyAssemblyPath);
    }

    private static void AssertMappingDiagnostic(
        DuckTypeAotCompatibilityMatrix matrix,
        Type proxyType,
        Type targetType,
        string expectedStatus,
        string? expectedDiagnosticCode)
    {
        var mapping = matrix.Mappings.Single(item =>
            string.Equals(item.ProxyType, proxyType.FullName, StringComparison.Ordinal) &&
            string.Equals(item.TargetType, targetType.FullName, StringComparison.Ordinal));

        mapping.Status.Should().Be(expectedStatus);
        if (expectedDiagnosticCode is null)
        {
            mapping.DiagnosticCode.Should().BeNullOrWhiteSpace();
        }
        else
        {
            mapping.DiagnosticCode.Should().Be(expectedDiagnosticCode);
        }
    }

    private static object CreateMappingDocumentEntry(Type proxyType, Type targetType, string mode = "forward")
    {
        return new
        {
            mode,
            proxyType = proxyType.FullName,
            proxyAssembly = proxyType.Assembly.GetName().Name,
            targetType = targetType.FullName,
            targetAssembly = targetType.Assembly.GetName().Name
        };
    }

    private static void AssertThrowsExactDuckTypeFailure<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ex.InnerException.Should().BeOfType<TException>();
            return;
        }
        catch (Exception ex)
        {
            ex.Should().BeOfType<TException>();
            return;
        }

        throw new Xunit.Sdk.XunitException($"Expected {typeof(TException).FullName} to be thrown.");
    }

    private static CommandResult RunProcess(
        string fileName,
        string workingDirectory,
        int timeoutMilliseconds,
        bool captureOutput,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process '{fileName}'.");
        }

        Task<string>? standardOutputTask = null;
        Task<string>? standardErrorTask = null;
        if (captureOutput)
        {
            standardOutputTask = process.StandardOutput.ReadToEndAsync();
            standardErrorTask = process.StandardError.ReadToEndAsync();
        }

        if (!process.WaitForExit(timeoutMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort cleanup after timeout.
            }

            throw new TimeoutException($"Process '{fileName}' timed out after {timeoutMilliseconds}ms.");
        }

        return new CommandResult(
            process.ExitCode,
            standardOutputTask?.GetAwaiter().GetResult() ?? string.Empty,
            standardErrorTask?.GetAwaiter().GetResult() ?? string.Empty);
    }

    private readonly struct CommandResult
    {
        public CommandResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        public int ExitCode { get; }

        public string StandardOutput { get; }

        public string StandardError { get; }
    }

    private static TField? GetCreateTypeResultField<TField>(DuckType.CreateTypeResult result, string fieldName)
    {
        var field = typeof(DuckType.CreateTypeResult).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return (TField?)field!.GetValue(result);
    }

    private static ConstructorInfo? GetDuckProxyConstructor(Type proxyType)
    {
        return proxyType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .SingleOrDefault(constructor => constructor.GetParameters().Length == 1);
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

    private static string GetVendoredStrongNameKeyPath([CallerFilePath] string sourceFilePath = "")
    {
        var testsDirectory = Path.GetDirectoryName(sourceFilePath);
        testsDirectory.Should().NotBeNullOrWhiteSpace();

        return Path.GetFullPath(
            Path.Combine(
                testsDirectory!,
                "..",
                "..",
                "src",
                "Datadog.Trace",
                "Vendors",
                "StatsdClient",
                "StatsdClient.snk"));
    }

    private static string GetDatadogTraceNetStandardAssemblyPath([CallerFilePath] string sourceFilePath = "")
    {
        var testsDirectory = Path.GetDirectoryName(sourceFilePath);
        testsDirectory.Should().NotBeNullOrWhiteSpace();

        return Path.GetFullPath(
            Path.Combine(
                testsDirectory!,
                "..",
                "..",
                "src",
                "Datadog.Trace",
                "bin",
                "Release",
                "netstandard2.0",
                "Datadog.Trace.dll"));
    }

    private static string ResolveAssemblyMvidForTest(string assemblyPath)
    {
        using var module = ModuleDefMD.Load(assemblyPath);
        return module.Mvid?.ToString("D") ?? string.Empty;
    }

    private interface IAliasForwardProxy
    {
        string Value { get; }
    }

    private interface IAliasShadowProxy
    {
        string Value { get; }
    }

    private interface INestedPrivateClosedGenericProxy
    {
        INestedPrivateClosedGenericInnerProxy Method { get; }
    }

    private interface INestedPrivateClosedGenericInnerProxy
    {
        string Value { get; }
    }

    private sealed class NestedPrivateClosedGenericTarget<TValue>
    {
        private NestedPrivateClosedGenericInner<TValue> Method { get; } = new();

        private sealed class NestedPrivateClosedGenericInner<TInner>
        {
            public string Value => typeof(TInner).FullName ?? typeof(TInner).Name;
        }
    }

    private class AliasForwardBaseTarget
    {
        public AliasForwardBaseTarget(string value)
        {
            Value = value;
        }

        public virtual string Value { get; }
    }

    private sealed class AliasForwardDerivedTarget : AliasForwardBaseTarget
    {
        public AliasForwardDerivedTarget(string value)
            : base(value)
        {
        }
    }

    private sealed class AliasForwardOtherDerivedTarget : AliasForwardBaseTarget
    {
        public AliasForwardOtherDerivedTarget(string value)
            : base(value)
        {
        }
    }

    private class AliasShadowBaseTarget
    {
        private readonly string _value;

        public AliasShadowBaseTarget(string value)
        {
            _value = value;
        }

        public string Value => $"base:{_value}";
    }

    private sealed class AliasShadowDerivedTarget : AliasShadowBaseTarget
    {
        private readonly string _value;

        public AliasShadowDerivedTarget(string value)
            : base(value)
        {
            _value = value;
        }

        public new string Value => $"derived:{_value}";
    }

    private interface IExternalInheritedCompletionProxy : ICriticalNotifyCompletion
    {
        string Value { get; }
    }

    private sealed class ExternalInheritedCompletionTarget
    {
        public string Value => "completed";

        public void OnCompleted(Action continuation)
        {
            continuation();
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            continuation();
        }
    }

    private interface IBindingFlagsIgnoreCaseProxy
    {
        [Duck(Name = "property", BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        string Property { get; }

        [DuckField(Name = "field", BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        string Field { get; }

        [Duck(Name = "method", BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        string Method(string value);
    }

    private sealed class BindingFlagsIgnoreCaseTarget
    {
        public string Property => "property";

#pragma warning disable SA1401 // The target intentionally exposes a field to exercise DuckField binding.
        public string Field = "field";
#pragma warning restore SA1401

        public string Method(string value) => $"method:{value}";
    }

    private interface IIgnoreCaseRelaxedFallbackProxy
    {
        [Duck(Name = "read", BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase, ExplicitInterfaceTypeName = "*")]
        string Read(IIgnoreCaseRelaxedFallbackInnerProxy value);
    }

    private interface IIgnoreCaseRelaxedFallbackInnerProxy
    {
        string Name { get; }
    }

    private interface IIgnoreCaseRelaxedFallbackTargetContract
    {
        string Read(IgnoreCaseRelaxedFallbackInnerTarget value);
    }

    private sealed class IgnoreCaseRelaxedFallbackTarget : IIgnoreCaseRelaxedFallbackTargetContract
    {
        string IIgnoreCaseRelaxedFallbackTargetContract.Read(IgnoreCaseRelaxedFallbackInnerTarget value) => value.Name;
    }

    private sealed class IgnoreCaseRelaxedFallbackInnerTarget
    {
        public string Name => "inner";
    }

    private interface IBindingFlagsPublicOnlyFieldProxy
    {
        [DuckField(Name = "_hidden", BindingFlags = BindingFlags.Public | BindingFlags.Instance)]
        string Hidden { get; }
    }

    private sealed class BindingFlagsPrivateFieldTarget
    {
#pragma warning disable CS0414, SA1401 // The target intentionally uses fields to exercise BindingFlags filtering.
        private readonly string _hidden = "hidden";
#pragma warning restore CS0414, SA1401
    }

    private interface IBindingFlagsInstanceOnlyStaticFieldProxy
    {
        [DuckField(Name = "Value", BindingFlags = BindingFlags.Public | BindingFlags.Instance)]
        string Value { get; }
    }

    private sealed class BindingFlagsStaticFieldTarget
    {
#pragma warning disable SA1401 // The target intentionally uses fields to exercise BindingFlags filtering.
        public static string Value = "static";
#pragma warning restore SA1401
    }

    private interface IBindingFlagsDeclaredOnlyPropertyProxy
    {
        [Duck(Name = "Inherited", BindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)]
        string Inherited { get; }
    }

    private class BindingFlagsBaseTarget
    {
        public string Inherited => "base";
    }

    private sealed class BindingFlagsDerivedTarget : BindingFlagsBaseTarget
    {
    }

    private interface IBindingFlagsFlattenHierarchyStaticPropertyProxy
    {
        [Duck(Name = "Flattened", BindingFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)]
        string Flattened { get; }
    }

    private class BindingFlagsStaticBaseTarget
    {
        public static string Flattened => "flattened";
    }

    private sealed class BindingFlagsStaticDerivedTarget : BindingFlagsStaticBaseTarget
    {
    }

    private interface IBindingFlagsFallbackDeclaredOnlyPropertyProxy
    {
        [Duck(Name = "SecretProperty", BindingFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, FallbackToBaseTypes = true)]
        string SecretProperty { get; }
    }

    private interface IBindingFlagsFallbackDeclaredOnlyFieldProxy
    {
        [DuckField(Name = "_secretField", BindingFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, FallbackToBaseTypes = true)]
        string SecretField { get; }
    }

    private class BindingFlagsFallbackBaseTarget
    {
#pragma warning disable CS0414, SA1401 // The target intentionally uses a private field to exercise DuckField binding.
        private readonly string _secretField = "base-field";
#pragma warning restore CS0414, SA1401

        private string SecretProperty => "base-property";
    }

    private sealed class BindingFlagsFallbackDerivedTarget : BindingFlagsFallbackBaseTarget
    {
    }

    private interface IInheritedGenericBaseProxy<TValue>
    {
        TValue Value { get; }

        TValue Echo(TValue value);
    }

    private interface IInheritedGenericConstantProxy<TValue> : IInheritedGenericBaseProxy<string>
    {
    }

    private interface IInheritedGenericComposedProxy<TValue> : IInheritedGenericBaseProxy<List<TValue>>
    {
    }

    private interface IInheritedGenericByRefBaseProxy<TValue>
    {
        bool TryGet(out TValue value);

        bool TryUpdate(ref TValue value);

        TValue[,] EchoGrid(TValue[,] values);
    }

    private interface IInheritedGenericByRefComposedProxy<TValue> : IInheritedGenericByRefBaseProxy<List<TValue>>
    {
    }

    private sealed class InheritedGenericStringTarget
    {
        public string Value => "constant";

        public string Echo(string value) => $"echo:{value}";
    }

    private sealed class InheritedGenericListTarget
    {
        public List<int> Value => [1, 2, 3];

        public List<int> Echo(List<int> value)
        {
            var result = new List<int>(value);
            result.Add(9);
            return result;
        }
    }

    private sealed class InheritedGenericByRefTarget
    {
        public bool TryGet(out List<int> value)
        {
            value = [1, 2, 3];
            return true;
        }

        public bool TryUpdate(ref List<int> value)
        {
            value.Add(8);
            return true;
        }

        public List<int>[,] EchoGrid(List<int>[,] values)
        {
            values[0, 0].Add(9);
            return values;
        }
    }

    private interface IClosedGenericAliasProxy<T>
    {
        T Value { get; }
    }

    private class ClosedGenericAliasBase<T>
    {
        public ClosedGenericAliasBase(T value)
        {
            Value = value;
        }

        public T Value { get; }
    }

    private sealed class ClosedGenericAliasDerived : ClosedGenericAliasBase<int>
    {
        public ClosedGenericAliasDerived(int value)
            : base(value)
        {
        }
    }

    internal abstract class ClosedGenericClassDuckProxy<T>
    {
        protected ClosedGenericClassDuckProxy()
        {
        }

        public abstract T Value { get; }

        public abstract T Echo(T value);
    }

    private sealed class ClosedGenericClassDuckTarget<T>
    {
        public ClosedGenericClassDuckTarget(T value)
        {
            Value = value;
        }

        public T Value { get; }

        public T Echo(T value)
        {
            if (value is int number)
            {
                return (T)(object)(number + 1);
            }

            return value;
        }
    }

    internal abstract class NonPublicClassProxy
    {
        internal NonPublicClassProxy()
        {
        }

        public string InvokeInternal(string value) => InternalEcho(value);

        public string InvokePrivateProtected(string value) => PrivateProtectedEcho(value);

        internal abstract string InternalEcho(string value);

        private protected abstract string PrivateProtectedEcho(string value);
    }

    internal abstract class ConstructorOrderProxyBase
    {
        protected ConstructorOrderProxyBase()
        {
            CapturedDuringConstruction = ReadValue();
        }

        public string? CapturedDuringConstruction { get; }

        public abstract string ReadValue();
    }

    private sealed class ConstructorOrderTarget
    {
        private readonly string _value;

        public ConstructorOrderTarget(string value)
        {
            _value = value;
        }

        public string ReadValue() => _value;
    }

    internal abstract class PrivateConstructorClassProxy
    {
        private PrivateConstructorClassProxy()
        {
        }

        public abstract string ReadValue();
    }

    private sealed class PrivateConstructorClassTarget
    {
        public string ReadValue() => "private-ctor";
    }

    private interface IExternalGenericParameterTypeNameProxy
    {
        [Duck(Name = "Resolve", GenericParameterTypeNames = new[] { "ExternalGenericArgs.GenericArgument, ExternalGenericArgs" })]
        string Resolve();
    }

    private sealed class NonPublicClassTarget
    {
        internal string InternalEcho(string value) => $"internal:{value}";

        internal string PrivateProtectedEcho(string value) => $"private-protected:{value}";
    }

    private interface IObjectDuckIncludeProxy
    {
    }

    private sealed class ObjectDuckIncludeTarget
    {
        [DuckInclude]
        public override int GetHashCode()
        {
            return 1234;
        }
    }

    internal abstract class DuckIgnoreRuntimeProxyBase
    {
        public abstract string Value { get; }

        [DuckIgnore]
        public virtual string IgnoredValue => "ignored";
    }

    private sealed class DuckIgnoreRuntimeTarget
    {
        public string Value => "target";
    }

    [AttributeUsage(AttributeTargets.Class)]
    private sealed class ReverseCopiedRuntimeMarkerAttribute : Attribute
    {
        public ReverseCopiedRuntimeMarkerAttribute(string marker)
        {
            Marker = marker;
        }

        public string Marker { get; }
    }

    private interface IReverseCopiedAttributeRuntimeProxy
    {
        string Read();
    }

    [ReverseCopiedRuntimeMarker("copied")]
    private sealed class ReverseCopiedAttributeRuntimeTarget
    {
        [DuckReverseMethod]
        public string Read() => "reverse";
    }

    private interface IOptionalParameterProxy
    {
        int Add(int value);
    }

    private interface IConstantOnlyOptionalParameterProxy
    {
        int Add(int value);
    }

    private sealed class OptionalParameterTarget
    {
        public int Add(int value, int optional = 7) => value + optional;
    }

    private interface IInRefDirectionProxy
    {
        void Mutate([In] ref int value);
    }

    private sealed class InRefDirectionTarget
    {
        public void Mutate(ref int value)
        {
            value++;
        }
    }

    private interface INonByRefDuckChainNullProxy
    {
        string Read(INonByRefDuckChainNullInnerProxy value);
    }

    private interface INonByRefDuckChainNullInnerProxy
    {
        string Name { get; }
    }

    private sealed class NonByRefDuckChainNullInnerTarget
    {
        public string Name => "inner";
    }

    private sealed class NonByRefDuckChainNullTarget
    {
        public string Read(NonByRefDuckChainNullInnerTarget? value)
        {
            return value is null ? "null-preserved" : value.Name;
        }
    }

    private interface IByRefDuckChainNullProxy
    {
        bool IsNullPreserved(ref IByRefDuckChainNullInnerProxy? value);
    }

    private interface IByRefDuckChainNullInnerProxy
    {
        string Name { get; }
    }

    private sealed class ByRefDuckChainNullInnerTarget
    {
        public string Name => "inner";
    }

    private sealed class ByRefDuckChainNullTarget
    {
        public bool IsNullPreserved(ref ByRefDuckChainNullInnerTarget? value)
        {
            return value is null;
        }
    }

    private interface IFailureReplayPropertyCantBeReadProxy
    {
        string OnlySetter { get; set; }
    }

    private sealed class FailureReplayPropertyCantBeReadTarget
    {
        public string OnlySetter
        {
            set { }
        }
    }

    private interface IFailureReplayPropertyArgumentsLengthProxy
    {
        string Item { get; }
    }

    private sealed class FailureReplayPropertyArgumentsLengthTarget
    {
        public string this[string key] => key;
    }

    private interface IFailureReplayPropertyOrFieldNotFoundProxy
    {
        string Name { get; set; }
    }

    private sealed class FailureReplayPropertyOrFieldNotFoundTarget
    {
    }

    private interface IFailureReplayProxyMethodParameterMissingProxy
    {
        [Duck(ParameterTypeNames = new[] { "System.String", "System.String" })]
        void Add(string key);
    }

    private sealed class FailureReplayProxyMethodParameterMissingTarget
    {
        public void Add(string key, string value)
        {
        }
    }

    private interface IFailureReplayInvalidTypeConversionProxy
    {
        float Sum(int a, int b);
    }

    private sealed class FailureReplayInvalidTypeConversionTarget
    {
        public int Sum(int a, int b) => a + b;
    }

    private interface ITestDuckStaticClassProxy
    {
        string Value { get; set; }

        string Echo(string value);
    }

    private static class TestDuckStaticClassTarget
    {
        private static string _value = "initial";

        public static string Value
        {
            get => _value;
            set => _value = value;
        }

        public static string Echo(string value) => $"static-class:{value}";

        public static string ReadValue() => _value;

        public static void ResetValue(string value)
        {
            _value = value;
        }
    }

    private interface IReverseInheritedMethodProxy
    {
        string Echo(string value);
    }

    private class ReverseInheritedMethodBaseTarget
    {
        [DuckReverseMethod]
        public string Echo(string value) => $"base:{value}";
    }

    private sealed class ReverseInheritedMethodTarget : ReverseInheritedMethodBaseTarget
    {
    }

    private interface IReversePrivatePropertyProxy
    {
        string Value { get; set; }
    }

    private sealed class ReversePrivatePropertyTarget
    {
        [DuckReverseMethod]
        private string Value { get; set; } = "private";
    }

    private interface IReverseInheritedPublicPropertyProxy
    {
        string Value { get; set; }
    }

    private class ReverseInheritedPublicPropertyBaseTarget
    {
        private string _value;

        public ReverseInheritedPublicPropertyBaseTarget(string value)
        {
            _value = value;
        }

        [DuckReverseMethod]
        public string Value
        {
            get => _value;
            set => _value = value;
        }
    }

    private sealed class ReverseInheritedPublicPropertyTarget : ReverseInheritedPublicPropertyBaseTarget
    {
        public ReverseInheritedPublicPropertyTarget(string value)
            : base(value)
        {
        }

        public string ReadValue() => Value;
    }

    private interface IFailureReplayReverseGenericProxy
    {
        void Add<TKey, TValue>(TKey key, TValue value);
    }

    private sealed class FailureReplayReverseGenericTarget
    {
        [DuckReverseMethod]
        public void Add<TKey>(TKey key, object value)
        {
        }
    }

    private interface IFailureReplayReverseAttributeMismatchProxy
    {
        void Add(string key, string value);
    }

    private sealed class FailureReplayReverseAttributeMismatchTarget
    {
        [DuckReverseMethod(ParameterTypeNames = new[] { "System.String" })]
        public void Add(string key, string value)
        {
        }
    }

    private interface IFailureReplayReverseMissingMethodProxy
    {
        void Add(int value1, int value2);
    }

    private sealed class FailureReplayReverseMissingMethodTarget
    {
    }

    private interface IFailureReplayReverseMissingPropertyProxy
    {
        string Value { get; set; }
    }

    private sealed class FailureReplayReverseMissingPropertyTarget
    {
    }

    [DuckCopy]
    private struct FailureReplayEmptyDuckCopyProxy
    {
        public string Name { get; set; }
    }

    private sealed class FailureReplayEmptyDuckCopyTarget
    {
        public string Name => "empty";
    }

#pragma warning disable CS0649
    [DuckCopy]
    private struct TestDuckStructCopyBasePrivatePropertyDeclaredOnlyFallbackProxy
    {
        [Duck(Name = "Hidden", BindingFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, FallbackToBaseTypes = true)]
        public int Hidden;
    }

    [DuckCopy]
    private struct TestDuckStructCopyBasePrivateFieldDeclaredOnlyFallbackProxy
    {
        [DuckField(Name = "_hidden", BindingFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, FallbackToBaseTypes = true)]
        public int Hidden;
    }
#pragma warning restore CS0649

    private interface IFailureReplayReverseAbstractImplementorProxy
    {
        string Value { get; }
    }

    private abstract class FailureReplayReverseAbstractImplementorTarget
    {
        [DuckReverseMethod]
        public string Value => "abstract";
    }

    private interface IFailureReplayReverseInterfaceImplementorProxy
    {
        string Value { get; }
    }

    private interface IFailureReplayReverseInterfaceImplementorTarget
    {
        [DuckReverseMethod]
        string Value { get; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    private sealed class FailureReplayNamedArgumentAttribute : Attribute
    {
        public string? Name { get; set; }
    }

    private interface IFailureReplayReverseNamedArgumentAttributeProxy
    {
        string Value { get; }
    }

    [FailureReplayNamedArgument(Name = "named")]
    private sealed class FailureReplayReverseNamedArgumentAttributeTarget
    {
        [DuckReverseMethod]
        public string Value => "named";
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

    private static void AssertRegistrationLoadsGeneratedProxyType(
        TypeDef bootstrapType,
        string registrationMethodName,
        ITypeDefOrRef expectedGeneratedProxyType)
    {
        foreach (var bootstrapMethod in bootstrapType.Methods.Where(method => method.Body is not null))
        {
            var instructions = bootstrapMethod.Body!.Instructions;
            var registerIndex = instructions.ToList().FindIndex(
                instruction =>
                    instruction.OpCode == OpCodes.Call &&
                    instruction.Operand is IMethod method &&
                    string.Equals(method.Name, registrationMethodName, StringComparison.Ordinal));
            if (registerIndex < 0)
            {
                continue;
            }

            var registrationTypeTokens = instructions
                                        .Take(registerIndex)
                                        .Where(instruction => instruction.OpCode == OpCodes.Ldtoken && instruction.Operand is ITypeDefOrRef)
                                        .TakeLast(3)
                                        .Select(instruction => (ITypeDefOrRef)instruction.Operand)
                                        .ToList();
            registrationTypeTokens.Should().HaveCount(3);
            registrationTypeTokens[2].FullName.Should().Be(expectedGeneratedProxyType.FullName);
            return;
        }

        false.Should().BeTrue($"bootstrap should call {registrationMethodName}");
    }

    private static void AssertBootstrapDoesNotUseReflectionForGeneratedRegistrations(IReadOnlyCollection<Instruction> bootstrapInstructions)
    {
        bootstrapInstructions.Any(instruction => ReferencesSystemReflection(instruction.Operand))
                             .Should()
                             .BeFalse("generated registry bootstrap should construct delegates directly and avoid runtime reflection");
        bootstrapInstructions.Any(
            instruction =>
                instruction.OpCode == OpCodes.Call &&
                instruction.Operand is IMethod method &&
                string.Equals(method.Name, "GetMethodFromHandle", StringComparison.Ordinal))
                             .Should()
                             .BeFalse("generated registry bootstrap should not materialize MethodInfo from RuntimeMethodHandle");
        bootstrapInstructions.Any(
            instruction =>
                instruction.OpCode == OpCodes.Call &&
                instruction.Operand is IMethod method &&
                (string.Equals(method.Name, "RegisterAotProxy", StringComparison.Ordinal) ||
                 string.Equals(method.Name, "RegisterAotReverseProxy", StringComparison.Ordinal) ||
                 string.Equals(method.Name, "RegisterAotProxyFailure", StringComparison.Ordinal) ||
                 string.Equals(method.Name, "RegisterAotReverseProxyFailure", StringComparison.Ordinal)) &&
                method.MethodSig.Params.Any(parameter => string.Equals(parameter.FullName, "System.RuntimeMethodHandle", StringComparison.Ordinal)))
                             .Should()
                             .BeFalse("generated registry bootstrap should call delegate-based AOT registration overloads");
    }

    private static void AssertDirectDelegateRegistrationCalls(
        TypeDef bootstrapType,
        string registrationMethodName,
        string delegateTargetMethodPrefix,
        string delegateTypeFullNamePrefix)
    {
        var matchedCalls = 0;
        foreach (var bootstrapMethod in bootstrapType.Methods.Where(method => method.Body is not null))
        {
            var instructions = bootstrapMethod.Body!.Instructions;
            for (var i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                if (instruction.OpCode != OpCodes.Call ||
                    instruction.Operand is not IMethod registrationMethod ||
                    !string.Equals(registrationMethod.Name, registrationMethodName, StringComparison.Ordinal))
                {
                    continue;
                }

                matchedCalls++;
                registrationMethod.MethodSig.Params.Last().FullName.Should().StartWith(delegateTypeFullNamePrefix);
                i.Should().BeGreaterThanOrEqualTo(3, $"{registrationMethodName} should be preceded by delegate construction IL");
                instructions[i - 3].OpCode.Should().Be(OpCodes.Ldnull);
                instructions[i - 2].OpCode.Should().Be(OpCodes.Ldftn);
                instructions[i - 2].Operand.Should().BeAssignableTo<IMethod>();
                ((IMethod)instructions[i - 2].Operand).Name.String.Should().StartWith(delegateTargetMethodPrefix);
                instructions[i - 1].OpCode.Should().Be(OpCodes.Newobj);
                instructions[i - 1].Operand.Should().BeAssignableTo<IMethod>();
                ((IMethod)instructions[i - 1].Operand).DeclaringType.FullName.Should().StartWith(delegateTypeFullNamePrefix);
            }
        }

        matchedCalls.Should().BeGreaterThan(0, $"bootstrap should call {registrationMethodName}");
    }

    private static bool ReferencesSystemReflection(object? operand)
    {
        return operand switch
        {
            IMethod method => ReferencesSystemReflection(method.DeclaringType) || method.MethodSig.Params.Any(ReferencesSystemReflection) || ReferencesSystemReflection(method.MethodSig.RetType),
            IField field => ReferencesSystemReflection(field.DeclaringType) || ReferencesSystemReflection(field.FieldSig.GetFieldType()),
            IType type => type.FullName.StartsWith("System.Reflection.", StringComparison.Ordinal),
            _ => false
        };
    }

    private static (int Result, string Output) ProcessAndCapture(DuckTypeAotVerifyCompatOptions options)
    {
        using var capture = ConsoleOutputCapture.Redirect();
        var result = DuckTypeAotVerifyCompatProcessor.Process(options);
        return (result, capture.Output);
    }

    private sealed class ConsoleOutputCapture : IDisposable
    {
        private readonly IAnsiConsole _originalAnsiConsole;
        private readonly TextWriter _originalOutput;
        private readonly TextWriter _originalError;
        private readonly StringBuilder _output;

        private ConsoleOutputCapture()
        {
            _output = new StringBuilder();
            _originalAnsiConsole = AnsiConsole.Console;
            _originalOutput = Console.Out;
            _originalError = Console.Error;

            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new RedirectedAnsiConsoleOutput(_output) });
            Console.SetOut(new StringWriter(_output));
            Console.SetError(new StringWriter(_output));
        }

        public string Output => _output.ToString();

        public static ConsoleOutputCapture Redirect() => new();

        public void Dispose()
        {
            Console.SetOut(_originalOutput);
            Console.SetError(_originalError);
            AnsiConsole.Console = _originalAnsiConsole;
        }

        private sealed class RedirectedAnsiConsoleOutput : IAnsiConsoleOutput
        {
            public RedirectedAnsiConsoleOutput(StringBuilder output)
            {
                Writer = new StringWriter(output);
            }

            public TextWriter Writer { get; }

            public bool IsTerminal => false;

            public int Width => 640;

            public int Height => 480;

            public void SetEncoding(Encoding encoding)
            {
            }
        }
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
