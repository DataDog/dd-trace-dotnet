// <copyright file="BackwardBranchCoverageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Models.Tests;
using Datadog.Trace.Ci.Coverage.Util;
using Datadog.Trace.Coverage.Collector;
using FluentAssertions;
using Xunit;
using CecilAssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using CecilReaderParameters = Mono.Cecil.ReaderParameters;

namespace Datadog.Trace.Tools.Runner.Tests;

[Collection(nameof(CoverageReporterHandlerCollection))]
public class BackwardBranchCoverageTests
{
    // F# compiles `let rec loop n = ...` without other visible sequence points into a loop that branches back to the
    // first instruction, which is also the method's only visible sequence point. In C#, an optimized build where
    // everything but the loop's first statement is hidden produces the same shape.
    private const string FixtureSource = """
        public static class BackwardBranchFixture
        {
            public static int Loop(int value)
        #line hidden
            {
        #line default
            start: value--;
        #line hidden
                if (value != 0) goto start;
                return value;
            }
        }
        """;

    private static readonly int LoopLine = FixtureSource.Split('\n').ToList().FindIndex(line => line.Contains("start: value--;")) + 1;

    [Theory]
    [CombinatorialData]
    public void SingleSequencePointLoopToFirstInstructionCanExecuteAndRecordCoverage(
        [CombinatorialValues("LineExecution", "LineCallCount")] string coverageMode)
    {
        var directory = Path.Combine(Path.GetTempPath(), "coverage-backward-branch-" + Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "BackwardBranchFixture.dll");
        var previousHandler = CoverageReporter.Handler;
        try
        {
            TestAssemblyCompiler.CompileCSharp(path, optimize: true, ("BackwardBranchFixture.cs", FixtureSource));
            AssertFixtureLoopsBackToItsOnlySequencePoint(path);
            RuntimeHelpers.PrepareMethod(GetLoop(Assembly.Load(File.ReadAllBytes(path))).MethodHandle);

            var settings = new CoverageSettings(null, string.Empty);
            settings.TestOptimization.SetCodeCoverageMode(coverageMode);
            new AssemblyProcessor(path, settings).Process();

            var loop = GetLoop(Assembly.Load(File.ReadAllBytes(path)));
            RuntimeHelpers.PrepareMethod(loop.MethodHandle);
            var handler = new DefaultCoverageEventHandler();
            CoverageReporter.Handler = handler;
            var session = handler.StartSession("xunit");
            object coverage;
            try
            {
                loop.Invoke(null, [3]).Should().Be(0);
            }
            finally
            {
                coverage = handler.EndSession(session);
            }

            var file = coverage.Should().BeOfType<TestCoverage>().Subject.Files.Should().ContainSingle().Subject;
            using var bitmap = new FileBitmap(file.Bitmap!);
            bitmap.Get(LoopLine).Should().BeTrue();
        }
        finally
        {
            CoverageReporter.Handler = previousHandler;
            Directory.Delete(directory, recursive: true);
        }
    }

    private static MethodInfo GetLoop(Assembly assembly)
        => assembly.GetType("BackwardBranchFixture", throwOnError: true)!.GetMethod("Loop", BindingFlags.Static | BindingFlags.Public)!;

    // The fix only matters for this shape; fail loudly if a compiler change stops producing it.
    private static void AssertFixtureLoopsBackToItsOnlySequencePoint(string assemblyPath)
    {
        using var assembly = CecilAssemblyDefinition.ReadAssembly(assemblyPath, new CecilReaderParameters { ReadSymbols = true, InMemory = true });
        var loop = assembly.MainModule.GetType("BackwardBranchFixture").Methods.Single(method => method.Name == "Loop");
        var firstInstruction = loop.Body.Instructions[0];

        loop.DebugInformation.SequencePoints.Where(sequencePoint => !sequencePoint.IsHidden)
            .Should().ContainSingle()
            .Which.Offset.Should().Be(firstInstruction.Offset);
        loop.Body.HasExceptionHandlers.Should().BeFalse();
        loop.Body.Instructions.Should().Contain(instruction => instruction.Operand == firstInstruction);
    }
}
