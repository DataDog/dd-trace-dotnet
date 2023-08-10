// <copyright file="CoverageRewriteTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Coverage.Collector;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests
{
    [UsesVerify]
    public class CoverageRewriteTests
    {
        [Fact]
        public async Task CoverageRewriteTest()
        {
            const string assemblyFileName = "CoverageRewriterAssembly.dll";

            // Copy assembly and symbols to a temporal folder (we need to rewrite it)
            var temporalFileName = Path.GetFileNameWithoutExtension(Path.GetTempFileName()) + ".dll";
            File.Copy(assemblyFileName, temporalFileName, true);
            File.Copy(Path.GetFileNameWithoutExtension(assemblyFileName) + ".pdb", Path.GetFileNameWithoutExtension(temporalFileName) + ".pdb", true);

            // Verify settings
            var settings = new DecompilerSettings();
            VerifierSettings.DerivePathInfo(
                (sourceFile, projectDirectory, type, method) =>
                {
                    return new(directory: Path.Combine(projectDirectory, "..", "snapshots"));
                });

            // Decompile original code
            var decompilerOriginalCode = new CSharpDecompiler(temporalFileName, settings);
            var originalCode = decompilerOriginalCode.DecompileWholeModuleAsString();

            var originalVerifySettings = new VerifySettings();
            originalVerifySettings.UseFileName("CoverageRewriteTests.Original");
            await Verifier.Verify(originalCode, originalVerifySettings);

            // Apply rewriter process
            var covSettings = new CoverageSettings(null, string.Empty, null);
            var asmProcessor = new AssemblyProcessor(temporalFileName, covSettings);
            asmProcessor.Process();

            // Decompile rewritten code
            var decompilerTransCode = new CSharpDecompiler(temporalFileName, settings);
            var transCode = decompilerTransCode.DecompileWholeModuleAsString();

            var transVerifySettings = new VerifySettings();
            transVerifySettings.UseFileName("CoverageRewriteTests.Rewritten");
            await Verifier.Verify(transCode, transVerifySettings);
        }
    }
}
