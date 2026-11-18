// <copyright file="WindowsPdbCoverageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage.Attributes;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Coverage.Collector;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Microsoft.CodeAnalysis.Emit;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using Mono.Collections.Generic;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Covers rewriting assemblies built with Windows PDBs (DebugType full or pdbonly). Cecil writes those symbols through the
/// COM ISymUnmanagedWriter2, whose Close() has crashed the data collector with access violations.
/// </summary>
public class WindowsPdbCoverageTests
{
    private const string EnumsSource = """
        using System;

        namespace Fixture.Enums
        {
            public enum Shade : byte
            {
                Light = 1,
                Dark = 2,
            }

            [AttributeUsage(AttributeTargets.All)]
            public sealed class ShadeAttribute : Attribute
            {
                public ShadeAttribute(Shade shade)
                {
                    Shade = shade;
                }

                public Shade Shade { get; }
            }

            public static class ShadeNames
            {
                public static string Describe(Shade shade)
                {
                    return shade == Shade.Dark ? "dark" : "light";
                }
            }
        }
        """;

    private const string LibrarySource = """
        using Fixture.Enums;

        namespace Fixture.Library
        {
            [Shade(Shade.Dark)]
            public static class Painter
            {
                public const Shade DefaultShade = Shade.Light;

                public static int Paint(Shade shade = Shade.Dark)
                {
                    var value = (int)shade;
                    return value + 1;
                }
            }
        }
        """;

    private const string ConsumerSource = """
        using Fixture.Enums;
        using Fixture.Library;

        namespace Fixture.Consumer
        {
            public static class Consumer
            {
                public static string Run()
                {
                    var painted = Painter.Paint();
                    return ShadeNames.Describe(Painter.DefaultShade) + painted;
                }
            }
        }
        """;

    private static readonly byte[] PortablePdbSignature = Encoding.ASCII.GetBytes("BSJB");
    private static readonly byte[] WindowsPdbSignature = Encoding.ASCII.GetBytes("Microsoft C/C++ MSF 7.00");

    [Fact]
    public void WriteTargetAssemblyWritesPortablePdbForWindowsPdbSymbols()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var assemblyPath = Path.Combine(directory, "CoverageRewriterAssembly.dll");
            File.Copy("CoverageRewriterAssembly.dll", assemblyPath);
            File.Copy("CoverageRewriterAssembly.pdb", Path.ChangeExtension(assemblyPath, ".pdb"));
            var readerParameters = new ReaderParameters { ReadSymbols = true, InMemory = true, SymbolReaderProvider = new WindowsPdbSymbolReaderProvider() };
            using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters))
            {
                assembly.MainModule.SymbolReader.GetWriterProvider().Should().BeOfType<NativePdbWriterProvider>();

                AssemblyProcessor.WriteTargetAssembly(assembly, assemblyPath, strongNameKeyBlob: null, writePortablePdb: true);
            }

            AssertPdbFormat(assemblyPath, PortablePdbSignature);
            using var rewrittenAssembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { ReadSymbols = true });
            rewrittenAssembly.MainModule.GetTypes().SelectMany(type => type.Methods).Should().Contain(method => method.DebugInformation.HasSequencePoints);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [SkippableTheory]
    [CombinatorialData]
    public void ProcessingInterdependentWindowsPdbAssembliesWritesTheConfiguredPdbFormat(bool portablePdbEnabled)
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

        var directory = CreateTemporaryDirectory();
        try
        {
            var assemblyPaths = CompileInterdependentWindowsPdbAssemblies(directory);
            var settings = CreateCoverageSettings(portablePdbEnabled);

            // Same shape as CoverageCollector.ProcessFolder: every assembly of the folder is rewritten in parallel.
            Parallel.ForEach(assemblyPaths, path => new AssemblyProcessor(path, settings).Process());

            foreach (var assemblyPath in assemblyPaths)
            {
                AssertPdbFormat(assemblyPath, portablePdbEnabled ? PortablePdbSignature : WindowsPdbSignature);
                using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { ReadSymbols = true });
                assembly.CustomAttributes.Should().Contain(attribute => attribute.AttributeType.FullName == typeof(CoveredAssemblyAttribute).FullName);
                assembly.MainModule.GetTypes().SelectMany(type => type.Methods).Should().Contain(method => method.DebugInformation.HasSequencePoints);
            }

            using var library = AssemblyDefinition.ReadAssembly(assemblyPaths[1]);
            var painter = library.MainModule.GetType("Fixture.Library.Painter");
            painter.Methods.Single(method => method.Name == "Paint").Parameters.Single().Constant.Should().Be((byte)2);
            painter.Fields.Single(field => field.Name == "DefaultShade").Constant.Should().Be((byte)1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [SkippableTheory]
    [CombinatorialData]
    public void WindowsPdbWritesAreSerializedOnlyWhenTheyKeepTheWindowsPdb(bool writePortablePdb)
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);

        var directory = CreateTemporaryDirectory();
        try
        {
            var assemblyPath = CompileInterdependentWindowsPdbAssemblies(directory)[0];
            using var resolver = new CoverageAssemblyResolver(new ConsoleCollectorLogger(), assemblyPath);
            resolver.AddSearchDirectory(directory);
            using var assembly = AssemblyProcessor.ReadTargetAssembly(assemblyPath, resolver);
            var publishedUnderWindowsPdbWriteLock = new List<bool>();

            AssemblyProcessor.WriteTargetAssembly(
                assembly,
                assemblyPath,
                strongNameKeyBlob: null,
                writePortablePdb,
                (source, destination, backup) =>
                {
                    publishedUnderWindowsPdbWriteLock.Add(Monitor.IsEntered(AssemblyProcessor.WindowsPdbWriteLock));
                    File.Replace(source, destination, backup);
                });

            publishedUnderWindowsPdbWriteLock.Should().Equal(!writePortablePdb, !writePortablePdb);
            AssertPdbFormat(assemblyPath, writePortablePdb ? PortablePdbSignature : WindowsPdbSignature);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string[] CompileInterdependentWindowsPdbAssemblies(string directory)
    {
        var enumsPath = Path.Combine(directory, "Fixture.Enums.dll");
        var libraryPath = Path.Combine(directory, "Fixture.Library.dll");
        var consumerPath = Path.Combine(directory, "Fixture.Consumer.dll");
        TestAssemblyCompiler.CompileCSharp(enumsPath, optimize: false, DebugInformationFormat.Pdb, [], ("Enums.cs", EnumsSource));
        TestAssemblyCompiler.CompileCSharp(libraryPath, optimize: false, DebugInformationFormat.Pdb, [enumsPath], ("Library.cs", LibrarySource));
        TestAssemblyCompiler.CompileCSharp(consumerPath, optimize: false, DebugInformationFormat.Pdb, [enumsPath, libraryPath], ("Consumer.cs", ConsumerSource));

        string[] assemblyPaths = [enumsPath, libraryPath, consumerPath];
        foreach (var assemblyPath in assemblyPaths)
        {
            AssertPdbFormat(assemblyPath, WindowsPdbSignature);
        }

        return assemblyPaths;
    }

    private static CoverageSettings CreateCoverageSettings(bool portablePdbEnabled)
    {
        var source = new DictionaryConfigurationSource(new Dictionary<string, string>
        {
            [ConfigurationKeys.CIVisibility.CodeCoveragePortablePdbEnabled] = portablePdbEnabled ? "true" : "false",
        });
        return new CoverageSettings(null, string.Empty, new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance));
    }

    private static void AssertPdbFormat(string assemblyPath, byte[] expectedSignature)
    {
        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
        File.ReadAllBytes(pdbPath).Take(expectedSignature.Length).Should().Equal(expectedSignature, $"'{pdbPath}' should start with '{Encoding.ASCII.GetString(expectedSignature)}'");
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "coverage-windows-pdb-" + Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// Reads portable PDB symbols but reports the Windows PDB writer, which is what Cecil does for a Windows PDB.
    /// Windows PDBs can't be produced outside Windows, so this keeps the format selection covered on every platform.
    /// </summary>
    private sealed class WindowsPdbSymbolReaderProvider : ISymbolReaderProvider
    {
        public ISymbolReader GetSymbolReader(ModuleDefinition module, string fileName)
            => new WindowsPdbSymbolReader(new PortablePdbReaderProvider().GetSymbolReader(module, fileName));

        public ISymbolReader GetSymbolReader(ModuleDefinition module, Stream symbolStream)
            => new WindowsPdbSymbolReader(new PortablePdbReaderProvider().GetSymbolReader(module, symbolStream));
    }

    private sealed class WindowsPdbSymbolReader : ISymbolReader
    {
        private readonly ISymbolReader _portablePdbReader;

        public WindowsPdbSymbolReader(ISymbolReader portablePdbReader)
        {
            _portablePdbReader = portablePdbReader;
        }

        public ISymbolWriterProvider GetWriterProvider() => new NativePdbWriterProvider();

        public bool ProcessDebugHeader(ImageDebugHeader header) => _portablePdbReader.ProcessDebugHeader(header);

        public MethodDebugInformation Read(MethodDefinition method) => _portablePdbReader.Read(method);

        public Collection<CustomDebugInformation> Read(ICustomDebugInformationProvider provider) => _portablePdbReader.Read(provider);

        public void Dispose() => _portablePdbReader.Dispose();
    }
}
