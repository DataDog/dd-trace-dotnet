// <copyright file="DuckTypeAotProcessorsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class DuckTypeAotProcessorsTests
{
    public DuckTypeAotProcessorsTests()
    {
        DuckTypeAotEngine.ResetForTests();
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
            manifest.ProxyAssemblies.Should().ContainSingle(assembly => assembly.Name == proxyAssemblyName);
            manifest.TargetAssemblies.Should().ContainSingle(assembly => assembly.Name == targetAssemblyName);

            var compatibilityMatrix = JsonConvert.DeserializeObject<DuckTypeAotCompatibilityMatrix>(File.ReadAllText(compatibilityMatrixPath));
            compatibilityMatrix.Should().NotBeNull();
            compatibilityMatrix!.TotalMappings.Should().Be(1);
            compatibilityMatrix.Mappings.Should().ContainSingle(mapping => string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));
            var compatibilityMapping = compatibilityMatrix.Mappings.Single();
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
            generatedProxyType!.FindMethod("Echo").Should().NotBeNull();
            generatedProxyType.Interfaces.Any(interfaceImpl =>
                string.Equals(interfaceImpl.Interface.FullName, "Datadog.Trace.DuckTyping.IDuckType", StringComparison.Ordinal)).Should().BeTrue();
            generatedProxyType.FindMethod("get_Instance").Should().NotBeNull();
            generatedProxyType.FindMethod("get_Type").Should().NotBeNull();
            generatedProxyType.FindMethod("GetInternalDuckTypedInstance").Should().NotBeNull();

            var initializeMethod = bootstrapType!.FindMethod("Initialize");
            initializeMethod.Should().NotBeNull();
            initializeMethod!.Body.Should().NotBeNull();
            var enableAotModeInstruction = initializeMethod.Body!.Instructions.SingleOrDefault(
                instruction =>
                    instruction.OpCode == OpCodes.Call &&
                    instruction.Operand is IMethod method &&
                    string.Equals(method.Name, "EnableAotMode", StringComparison.Ordinal));
            enableAotModeInstruction.Should().NotBeNull();

            var registerAotProxyInstruction = initializeMethod.Body.Instructions.SingleOrDefault(
                instruction =>
                    instruction.OpCode == OpCodes.Call &&
                    instruction.Operand is IMethod method &&
                    string.Equals(method.Name, "RegisterAotProxy", StringComparison.Ordinal));
            registerAotProxyInstruction.Should().NotBeNull();

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

            var result = DuckTypeAotVerifyCompatProcessor.Process(new DuckTypeAotVerifyCompatOptions(reportPath, matrixPath));
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

            var result = DuckTypeAotVerifyCompatProcessor.Process(new DuckTypeAotVerifyCompatOptions(reportPath, matrixPath));
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
                var initializeMethod = bootstrapType!.FindMethod("Initialize");
                initializeMethod.Should().NotBeNull();
                var registerAotReverseInstruction = initializeMethod!.Body!.Instructions.SingleOrDefault(
                    instruction =>
                        instruction.OpCode == OpCodes.Call &&
                        instruction.Operand is IMethod method &&
                        string.Equals(method.Name, "RegisterAotReverseProxy", StringComparison.Ordinal));
                registerAotReverseInstruction.Should().NotBeNull();
            }

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ReverseInterface", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckProxy).FullName, StringComparison.Ordinal)));
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
            var initializeMethod = bootstrapType!.FindMethod("Initialize");
            initializeMethod.Should().NotBeNull();
            var registerAotReverseInstruction = initializeMethod!.Body!.Instructions.SingleOrDefault(
                instruction =>
                    instruction.OpCode == OpCodes.Call &&
                    instruction.Operand is IMethod method &&
                    string.Equals(method.Name, "RegisterAotReverseProxy", StringComparison.Ordinal));
            registerAotReverseInstruction.Should().NotBeNull();
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
                var outerConstructor = generatedOuterProxyType.GetConstructor([typeof(object)]);
                var innerConstructor = generatedInnerProxyType.GetConstructor([typeof(object)]);
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
            var ignoresAccessChecksAttribute = generatedModule.Assembly.CustomAttributes.FirstOrDefault(attribute =>
                string.Equals(attribute.AttributeType.FullName, "System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute", StringComparison.Ordinal));
            ignoresAccessChecksAttribute.Should().NotBeNull();
            ignoresAccessChecksAttribute!.ConstructorArguments.Should().HaveCount(1);
            ignoresAccessChecksAttribute.ConstructorArguments[0].Value!.ToString().Should().Be(targetAssemblyName);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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

                var fieldProxyConstructor = generatedFieldProxyType.GetConstructor([typeof(object)]);
                var innerProxyConstructor = generatedInnerProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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

                var propertyProxyConstructor = generatedPropertyProxyType.GetConstructor([typeof(object)]);
                var innerProxyConstructor = generatedInnerProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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

                var constructor = generatedOuterProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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

                var outerConstructor = generatedOuterProxyType.GetConstructor([typeof(object)]);
                var innerConstructor = generatedInnerProxyType.GetConstructor([typeof(object)]);
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

                var constructor = generatedOuterProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
    public void GenerateProcessorShouldSupportFieldAccessorsOnValueTypeTargets()
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
            matrix!.Mappings.Should().ContainSingle(mapping =>
                string.Equals(mapping.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal));

            var loadContext = new AssemblyLoadContext("DuckTypeAotProcessorsTests-ValueTypeTarget-Field", isCollectible: true);
            try
            {
                var generatedAssembly = loadContext.LoadFromAssemblyPath(outputPath);
                var contextTestAssembly = loadContext.LoadFromAssemblyPath(proxyAssemblyPath);

                var generatedProxyType = generatedAssembly.GetTypes().Single(type =>
                    string.Equals(type.Namespace, "Datadog.Trace.DuckTyping.Generated.Proxies", StringComparison.Ordinal) &&
                    type.GetInterfaces().Any(@interface => string.Equals(@interface.FullName, typeof(ITestDuckFieldProxy).FullName, StringComparison.Ordinal)));
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
                constructor.Should().NotBeNull();

                var targetType = contextTestAssembly.GetType(typeof(TestDuckStructFieldTarget).FullName!, throwOnError: true)!;
                var targetCtor = targetType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: [typeof(string)], modifiers: null);
                targetCtor.Should().NotBeNull();
                var targetInstance = targetCtor!.Invoke(["initial"]);

                var proxyInstance = constructor!.Invoke([targetInstance]);
                var getValueMethod = generatedProxyType.GetMethod("get_Value", Type.EmptyTypes);
                getValueMethod.Should().NotBeNull();
                var before = getValueMethod!.Invoke(proxyInstance, Array.Empty<object>());
                before.Should().Be("initial");

                var setValueMethod = generatedProxyType.GetMethod("set_Value", [typeof(string)]);
                setValueMethod.Should().NotBeNull();
                _ = setValueMethod!.Invoke(proxyInstance, ["after"]);

                var after = getValueMethod.Invoke(proxyInstance, Array.Empty<object>());
                after.Should().Be("after");
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
                var constructor = generatedProxyType.GetConstructor([typeof(object)]);
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
