// <copyright file="CoverageRewriteTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Datadog.Trace.Coverage.Collector;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

[UsesVerify]
public class CoverageRewriteTests
{
    public CoverageRewriteTests()
    {
        VerifierSettings.DerivePathInfo(
            (sourceFile, projectDirectory, type, method) =>
            {
                return new(directory: Path.Combine(projectDirectory, "..", "snapshots"));
            });
    }

    public static IEnumerable<object[]> FiltersData()
    {
        yield return
        [
            "CoverageRewriteTests.Rewritten.CoverletFilterByAttribute",
            @"<?xml version=""1.0"" encoding=""utf-8""?>
            <Configuration>
                <!-- Coverlet configuration -->
                <ExcludeByAttribute>CompilerGeneratedAttribute</ExcludeByAttribute>
            </Configuration>"
        ];

        yield return
        [
            "CoverageRewriteTests.Rewritten.NetFrameworkSettingsFilterByAttribute",
            @"<?xml version=""1.0"" encoding=""utf-8""?>
            <Configuration>
                <!-- Old .NET Framework configuration -->
                <CodeCoverage>
                    <Attributes>
                        <Exclude>
                            <Attribute>^System\.Runtime\.CompilerServices\.CompilerGeneratedAttribute$</Attribute>
                        </Exclude>
                    </Attributes>
                </CodeCoverage>
            </Configuration>"
        ];

        yield return
        [
            "CoverageRewriteTests.Rewritten.CoverletFilterBySourceFile",
            @"<?xml version=""1.0"" encoding=""utf-8""?>
            <Configuration>
                <!-- Coverlet configuration -->
                <ExcludeByFile>**/CoverageRewriterAssembly/Class1.cs</ExcludeByFile>
            </Configuration>"
        ];

        yield return
        [
            "CoverageRewriteTests.Rewritten.NetFrameworkSettingsFilterBySourceFile",
            @"<?xml version=""1.0"" encoding=""utf-8""?>
            <Configuration>
                <!-- Old .NET Framework configuration -->
                <CodeCoverage>
                    <Sources>
                        <Exclude>
                            <Source>.*/CoverageRewriterAssembly/Class1.cs$</Source>
                        </Exclude>
                    </Sources>
                </CodeCoverage>
            </Configuration>"
        ];

        yield return
        [
            "CoverageRewriteTests.Rewritten.CoverletFilterByAssemblyType",
            @"<?xml version=""1.0"" encoding=""utf-8""?>
            <Configuration>
                <!-- Coverlet configuration -->
                <Exclude>[*]CoverageRewriterAssembly.Class1</Exclude>
            </Configuration>"
        ];

        yield return
        [
            "CoverageRewriteTests.Rewritten.CoverletFilterByAssemblyAttribute",
            @"<?xml version=""1.0"" encoding=""utf-8""?>
            <Configuration>
                <!-- Coverlet configuration -->
                <ExcludeByAttribute>AssemblyFileVersionAttribute</ExcludeByAttribute>
            </Configuration>"
        ];
    }

    public static IEnumerable<object[]> CoverageModeData()
    {
        yield return ["LineExecution"];
        yield return ["LineCallCount"];
    }

    public static IEnumerable<object[]> FiltersByCoverageModeData()
    {
        foreach (var filter in FiltersData())
        {
            foreach (var coverageMode in CoverageModeData())
            {
                yield return filter.Concat(coverageMode).ToArray();
            }
        }
    }

#if NETCOREAPP2_1
    // Due to a BCL Bug in .NET Core 2.1 [DirectoryInfo.GetDirectories()] triggered by this test, we need to skip the test in some cases
    [SkippableTheory(typeof(NullReferenceException), typeof(IndexOutOfRangeException))]
#else
    [SkippableTheory]
#endif
    [MemberData(nameof(CoverageModeData))]
    public async Task NoFilter(string coverageMode)
    {
        var tempFileName = GetTempFile();

        // Verify settings
        var settings = new DecompilerSettings();

        // Decompile original code
        var decompilerOriginalCode = new CSharpDecompiler(tempFileName, settings);
        var originalCode = decompilerOriginalCode.DecompileWholeModuleAsString();

        var originalVerifySettings = new VerifySettings();
        originalVerifySettings.DisableRequireUniquePrefix();
        originalVerifySettings.UseFileName("CoverageRewriteTests.Original");
        await Verifier.Verify(originalCode, originalVerifySettings);

        // Apply rewriter process
        var covSettings = new CoverageSettings(null, string.Empty);
        covSettings.CIVisibility.SetCodeCoverageMode(coverageMode);
        var asmProcessor = new AssemblyProcessor(tempFileName, covSettings);
        asmProcessor.Process();

        // Decompile rewritten code
        var decompilerTransCode = new CSharpDecompiler(tempFileName, settings);
        var transCode = decompilerTransCode.DecompileWholeModuleAsString();

        var transVerifySettings = new VerifySettings();
        transVerifySettings.UseFileName($"CoverageRewriteTests.Rewritten.{coverageMode}");
        await Verifier.Verify(transCode, transVerifySettings);
    }

#if NETCOREAPP2_1
    // Due to a BCL Bug in .NET Core 2.1 [DirectoryInfo.GetDirectories()] triggered by this test, we need to skip the test in some cases
    [SkippableTheory(typeof(NullReferenceException), typeof(IndexOutOfRangeException))]
#else
    [SkippableTheory]
#endif
    [MemberData(nameof(FiltersByCoverageModeData))]
    public async Task WithFilters(string targetSnapshot, string configurationSettingsXml, string coverageMode)
    {
        var tempFileName = GetTempFile();

        // Verify settings
        var settings = new DecompilerSettings();

        var configurationElement = new XmlDocument();
        configurationElement.LoadXml(configurationSettingsXml);

        var covSettings = new CoverageSettings(configurationElement.DocumentElement, string.Empty);
        covSettings.CIVisibility.SetCodeCoverageMode(coverageMode);
        var asmProcessor = new AssemblyProcessor(tempFileName, covSettings);
        asmProcessor.Process();

        // Decompile rewritten code
        var decompilerTransCode = new CSharpDecompiler(tempFileName, settings);
        var transCode = decompilerTransCode.DecompileWholeModuleAsString();

        var transVerifySettings = new VerifySettings();
        transVerifySettings.UseFileName($"{targetSnapshot}.{coverageMode}");
        await Verifier.Verify(transCode, transVerifySettings);
    }

    private string GetTempFile()
    {
        const string assemblyFileName = "CoverageRewriterAssembly.dll";

        // Copy assembly and symbols to a temp folder (we need to rewrite it)
        var tempFileName = Path.GetFileNameWithoutExtension(Path.GetTempFileName()) + ".dll";
        File.Copy(assemblyFileName, tempFileName, true);
        File.Copy(Path.GetFileNameWithoutExtension(assemblyFileName) + ".pdb", Path.GetFileNameWithoutExtension(tempFileName) + ".pdb", true);
        return tempFileName;
    }
}
