// <copyright file="CoverageRewriteTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
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
        yield return new object[]
        {
            "CoverageRewriteTests.Rewritten.CoverletFilterByAttribute",
            @"<?xml version=""1.0"" encoding=""utf-8""?>
            <Configuration>
                <!-- Coverlet configuration -->
                <ExcludeByAttribute>CompilerGeneratedAttribute</ExcludeByAttribute>
            </Configuration>",
        };

        yield return new object[]
        {
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
            </Configuration>",
        };

        yield return new object[]
        {
            "CoverageRewriteTests.Rewritten.CoverletFilterBySourceFile",
            @"<?xml version=""1.0"" encoding=""utf-8""?>
            <Configuration>
                <!-- Coverlet configuration -->
                <ExcludeByFile>**/CoverageRewriterAssembly/Class1.cs</ExcludeByFile>
            </Configuration>",
        };

        yield return new object[]
        {
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
            </Configuration>",
        };

        yield return new object[]
        {
            "CoverageRewriteTests.Rewritten.CoverletFilterByAssemblyType",
            @"<?xml version=""1.0"" encoding=""utf-8""?>
            <Configuration>
                <!-- Coverlet configuration -->
                <Exclude>[*]CoverageRewriterAssembly.Class1</Exclude>
            </Configuration>",
        };
    }

    [Fact]
    public async Task NoFilter()
    {
        var tempFileName = GetTempFile();

        // Verify settings
        var settings = new DecompilerSettings();

        // Decompile original code
        var decompilerOriginalCode = new CSharpDecompiler(tempFileName, settings);
        var originalCode = decompilerOriginalCode.DecompileWholeModuleAsString();

        var originalVerifySettings = new VerifySettings();
        originalVerifySettings.UseFileName("CoverageRewriteTests.Original");
        await Verifier.Verify(originalCode, originalVerifySettings);

        // Apply rewriter process
        var covSettings = new CoverageSettings(null, string.Empty, null);
        var asmProcessor = new AssemblyProcessor(tempFileName, covSettings);
        asmProcessor.Process();

        // Decompile rewritten code
        var decompilerTransCode = new CSharpDecompiler(tempFileName, settings);
        var transCode = decompilerTransCode.DecompileWholeModuleAsString();

        var transVerifySettings = new VerifySettings();
        transVerifySettings.UseFileName("CoverageRewriteTests.Rewritten");
        await Verifier.Verify(transCode, transVerifySettings);
    }

    [Theory]
    [MemberData(nameof(FiltersData))]
    public async Task WithFilters(string targetSnapshot, string configurationSettingsXml)
    {
        var tempFileName = GetTempFile();

        // Verify settings
        var settings = new DecompilerSettings();

        var configurationElement = new XmlDocument();
        configurationElement.LoadXml(configurationSettingsXml);

        var covSettings = new CoverageSettings(configurationElement.DocumentElement, string.Empty, null);
        var asmProcessor = new AssemblyProcessor(tempFileName, covSettings);
        asmProcessor.Process();

        // Decompile rewritten code
        var decompilerTransCode = new CSharpDecompiler(tempFileName, settings);
        var transCode = decompilerTransCode.DecompileWholeModuleAsString();

        var transVerifySettings = new VerifySettings();
        transVerifySettings.UseFileName(targetSnapshot);
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
