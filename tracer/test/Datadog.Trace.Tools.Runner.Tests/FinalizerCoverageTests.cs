// <copyright file="FinalizerCoverageTests.cs" company="Datadog">
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
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Datadog.Trace.Tools.Runner.Tests;

[Collection(nameof(FinalizerCoverageCollection))]
public class FinalizerCoverageTests
{
    private const int FinalizerLine = 42;

    [Theory]
    [InlineData("LineExecution")]
    [InlineData("LineCallCount")]
    public void SingleSequencePointFinalizerCanExecuteAndRecordCoverage(string coverageMode)
    {
        var directory = Path.Combine(Path.GetTempPath(), "coverage-finalizer-" + Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "FinalizerFixture.dll");
        var previousHandler = CoverageReporter.Handler;
        try
        {
            WriteFixture(path);
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

    private static void WriteFixture(string path)
    {
        File.Copy("CoverageRewriterAssembly.dll", path);
        File.Copy("CoverageRewriterAssembly.pdb", Path.ChangeExtension(path, ".pdb"));
        using var assembly = AssemblyDefinition.ReadAssembly(
            path, new ReaderParameters { ReadSymbols = true, InMemory = true });
        var module = assembly.MainModule;
        var type = new TypeDefinition(string.Empty, "FinalizerFixture", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        module.Types.Add(type);
        var count = new FieldDefinition("CleanupCount", FieldAttributes.Public | FieldAttributes.Static, module.TypeSystem.Int32);
        type.Fields.Add(count);

        var constructor = new MethodDefinition(
            ".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.TypeSystem.Void);
        type.Methods.Add(constructor);
        var constructorIl = constructor.Body.GetILProcessor();
        constructorIl.Emit(OpCodes.Ldarg_0);
        constructorIl.Emit(OpCodes.Call, module.ImportReference(typeof(object).GetConstructor(Type.EmptyTypes)!));
        constructorIl.Emit(OpCodes.Ret);

        var finalizer = new MethodDefinition("Finalize", MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig, module.TypeSystem.Void);
        type.Methods.Add(finalizer);
        var il = finalizer.Body.GetILProcessor();
        var start = il.Create(OpCodes.Ldsfld, count);
        var handlerStart = il.Create(OpCodes.Ldarg_0);
        var end = il.Create(OpCodes.Ret);
        il.Append(start);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stsfld, count);
        il.Emit(OpCodes.Leave, end);
        il.Append(handlerStart);
        il.Emit(OpCodes.Call, module.ImportReference(typeof(object).GetMethod("Finalize", BindingFlags.Instance | BindingFlags.NonPublic)!));
        il.Emit(OpCodes.Endfinally);
        il.Append(end);
        finalizer.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally)
        {
            TryStart = start,
            TryEnd = handlerStart,
            HandlerStart = handlerStart,
            HandlerEnd = end,
        });

        // Expression-bodied finalizers have one visible point at the try entry.
        finalizer.DebugInformation.SequencePoints.Add(new SequencePoint(start, new Document("FinalizerFixture.cs"))
        {
            StartLine = FinalizerLine,
            EndLine = FinalizerLine,
            StartColumn = 1,
            EndColumn = 40,
        });
        AssemblyProcessor.WriteTargetAssembly(assembly, path, strongNameKeyBlob: null);
    }
}
