// <copyright file="FinalizerCoverageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
using ExceptionHandlerType = Mono.Cecil.Cil.ExceptionHandlerType;

namespace Datadog.Trace.Tools.Runner.Tests;

[Collection(nameof(CoverageReporterHandlerCollection))]
public class FinalizerCoverageTests
{
    // Compiled when the test runs so it follows what the C# compiler emits: the finalizer becomes
    // `try { body } finally { base.Finalize(); }` and its only visible sequence point is the try entry.
    private const string FixtureSource = """
        public class FinalizerFixture
        {
            public static int CleanupCount;

            ~FinalizerFixture() => CleanupCount++;
        }
        """;

    private static readonly int FinalizerLine = FixtureSource.Split('\n').ToList().FindIndex(line => line.Contains("~FinalizerFixture()")) + 1;

    [Theory]
    [CombinatorialData]
    public void ExpressionBodiedFinalizerCanExecuteAndRecordCoverage(
        bool optimize,
        [CombinatorialValues("LineExecution", "LineCallCount")] string coverageMode)
    {
        var directory = Path.Combine(Path.GetTempPath(), "coverage-finalizer-" + Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "FinalizerFixture.dll");
        var previousHandler = CoverageReporter.Handler;
        try
        {
            TestAssemblyCompiler.CompileCSharp(path, optimize, ("FinalizerFixture.cs", FixtureSource));
            AssertFixtureNeedsTheCountersLocal(path);
            var original = Assembly.Load(File.ReadAllBytes(path));
            RuntimeHelpers.PrepareMethod(GetFinalizer(original).MethodHandle);

            var settings = new CoverageSettings(null, string.Empty);
            settings.TestOptimization.SetCodeCoverageMode(coverageMode);
            new AssemblyProcessor(path, settings).Process();

            var assembly = Assembly.Load(File.ReadAllBytes(path));
            var finalizer = GetFinalizer(assembly);
            RuntimeHelpers.PrepareMethod(finalizer.MethodHandle);
            var handler = new DefaultCoverageEventHandler();
            CoverageReporter.Handler = handler;
            var session = handler.StartSession("xunit");
            object coverage;
            try
            {
                var instance = Activator.CreateInstance(finalizer.DeclaringType!);
                GC.SuppressFinalize(instance!);
                finalizer.Invoke(instance, null);
                finalizer.DeclaringType!.GetField("CleanupCount")!.GetValue(null).Should().Be(1);
            }
            finally
            {
                coverage = handler.EndSession(session);
            }

            var file = coverage.Should().BeOfType<TestCoverage>().Subject.Files.Should().ContainSingle().Subject;
            using var bitmap = new FileBitmap(file.Bitmap!);
            bitmap.Get(FinalizerLine).Should().BeTrue();
        }
        finally
        {
            CoverageReporter.Handler = previousHandler;
            Directory.Delete(directory, recursive: true);
        }
    }

    private static MethodInfo GetFinalizer(Assembly assembly)
        => assembly.GetType("FinalizerFixture", throwOnError: true)!.GetMethod(
            "Finalize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!;

    // The fix only matters for this shape; fail loudly if a compiler change stops producing it.
    private static void AssertFixtureNeedsTheCountersLocal(string assemblyPath)
    {
        using var assembly = CecilAssemblyDefinition.ReadAssembly(assemblyPath, new CecilReaderParameters { ReadSymbols = true, InMemory = true });
        var finalizer = assembly.MainModule.GetType("FinalizerFixture").Methods.Single(method => method.Name == "Finalize");
        var firstInstruction = finalizer.Body.Instructions[0];

        finalizer.DebugInformation.SequencePoints.Where(sequencePoint => !sequencePoint.IsHidden)
                 .Should().ContainSingle()
                 .Which.Offset.Should().Be(firstInstruction.Offset);
        finalizer.Body.ExceptionHandlers
                 .Should().ContainSingle(handler => handler.HandlerType == ExceptionHandlerType.Finally && handler.TryStart == firstInstruction);
    }
}
