// <copyright file="BackwardBranchCoverageTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Models.Tests;
using Datadog.Trace.Ci.Coverage.Util;
using Datadog.Trace.Coverage.Collector;
using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Datadog.Trace.Tools.Runner.Tests;

[Collection(nameof(CoverageReporterHandlerCollection))]
public class BackwardBranchCoverageTests
{
    private const int LoopLine = 42;

    // F# compiles `let rec loop n = ...` without other visible sequence points into a loop that branches back to the
    // first instruction, which is also the method's only visible sequence point.
    [Theory]
    [InlineData("LineExecution")]
    [InlineData("LineCallCount")]
    public void SingleSequencePointLoopToFirstInstructionCanExecuteAndRecordCoverage(string coverageMode)
    {
        var directory = Path.Combine(Path.GetTempPath(), "coverage-backward-branch-" + Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "BackwardBranchFixture.dll");
        var previousHandler = CoverageReporter.Handler;
        try
        {
            WriteFixture(path);
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

    private static void WriteFixture(string path)
    {
        File.Copy("CoverageRewriterAssembly.dll", path);
        File.Copy("CoverageRewriterAssembly.pdb", Path.ChangeExtension(path, ".pdb"));
        using var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { ReadSymbols = true, InMemory = true });
        var module = assembly.MainModule;
        var type = new TypeDefinition(string.Empty, "BackwardBranchFixture", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class, module.TypeSystem.Object);
        module.Types.Add(type);

        // static int Loop(int value) { L0: value--; if (value != 0) goto L0; return value; }
        var loop = new MethodDefinition("Loop", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, module.TypeSystem.Int32);
        loop.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, module.TypeSystem.Int32));
        type.Methods.Add(loop);
        var il = loop.Body.GetILProcessor();
        var start = il.Create(OpCodes.Ldarg_0);
        il.Append(start);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Sub);
        il.Emit(OpCodes.Starg, loop.Parameters[0]);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Brtrue, start);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ret);
        loop.DebugInformation.SequencePoints.Add(new SequencePoint(start, new Document("BackwardBranchFixture.fs"))
        {
            StartLine = LoopLine,
            EndLine = LoopLine,
            StartColumn = 1,
            EndColumn = 40,
        });

        AssemblyProcessor.WriteTargetAssembly(assembly, path, strongNameKeyBlob: null);
    }
}
