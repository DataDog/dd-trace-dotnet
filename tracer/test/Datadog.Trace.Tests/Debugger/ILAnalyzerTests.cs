// <copyright file="ILAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Xunit;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation;

public class ILAnalyzerTests
{
    // Known working case - similar to production usage
    [Fact]
    public void HasDirectCallTo_ExceptionDispatchInfoThrow_ReturnsTrue()
    {
        // Arrange
        var method = typeof(TestClass).GetMethod(
            nameof(TestClass.MethodWithExceptionDispatchInfoThrow),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        method.Should().NotBeNull("Test setup failed - method not found");

        // Act
        var result = ILAnalyzer.HasDirectCallTo(
            method!,
            typeof(ExceptionDispatchInfo),
            "Throw");

        // Assert
        result.Should().BeTrue();
    }

    // Regular method tests
    [Fact]
    public void HasDirectCallTo_SimpleMethodWithDirectCall_ReturnsTrue()
    {
        // Arrange
        var method = typeof(TestClass).GetMethod(
            nameof(TestClass.MethodWithThrow),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        method.Should().NotBeNull("Test setup failed - method not found");

        // Act
        var result = ILAnalyzer.HasDirectCallTo(
            method!,
            typeof(Exception),
            "ToString");

        // Assert
        result.Should().BeTrue();
    }

    // Async method test
    [Fact]
    public void HasDirectCallTo_AsyncMethodWithDirectCall_ReturnsTrue()
    {
        // Arrange
        var method = typeof(TestClass).GetMethod(
            nameof(TestClass.AsyncMethodWithThrow),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        method.Should().NotBeNull("Test setup failed - method not found");

        // Act
        var result = ILAnalyzer.HasDirectCallTo(
            method!,
            typeof(Exception),
            "ToString");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasDirectCallTo_MethodWithoutTargetCall_ReturnsFalse()
    {
        // Arrange
        var method = typeof(TestClass).GetMethod(
            nameof(TestClass.MethodWithoutTargetCall),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        method.Should().NotBeNull("Test setup failed - method not found");

        // Act
        var result = ILAnalyzer.HasDirectCallTo(
            method!,
            typeof(Exception),
            "ToString");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasDirectCallTo_UnavailableDependencyCall_DoesNotResolveDependency()
    {
        var assemblyName = "ILAnalyzerDependency_" + Guid.NewGuid().ToString("N");
        var dependencyPath = Path.Combine(Path.GetTempPath(), assemblyName + ".dll");
        var targetPath = Path.Combine(Path.GetTempPath(), assemblyName + ".Target.dll");
        var dependencyResolveCount = 0;

        ResolveEventHandler resolver = (_, args) =>
        {
            if (args.Name.StartsWith(assemblyName, StringComparison.Ordinal))
            {
                dependencyResolveCount++;
            }

            return null;
        };

        AppDomain.CurrentDomain.AssemblyResolve += resolver;
        try
        {
            var dependencySource = """
                                   namespace MissingDependency
                                   {
                                       public class DependencyType
                                       {
                                           public string GetValue() => nameof(DependencyType);
                                       }
                                   }
                                   """;
            var targetSource = """
                               namespace GeneratedTarget
                               {
                                   public class TargetType
                                   {
                                       public void ContainsUnavailableDependencyCall()
                                       {
                                           if (System.DateTime.UtcNow.Ticks == long.MinValue)
                                           {
                                               _ = new MissingDependency.DependencyType().GetValue();
                                           }
                                       }
                                   }
                               }
                               """;

            EmitAssembly(dependencyPath, assemblyName, dependencySource);
            EmitAssembly(targetPath, assemblyName + ".Target", targetSource, MetadataReference.CreateFromFile(dependencyPath));

            File.Delete(dependencyPath);

            var targetAssembly = Assembly.LoadFile(targetPath);
            var method = targetAssembly.GetType("GeneratedTarget.TargetType")!.GetMethod("ContainsUnavailableDependencyCall");
            method.Should().NotBeNull("Test setup failed - method not found");

            var result = ILAnalyzer.HasDirectCallTo(method!, typeof(ExceptionDispatchInfo), "Throw");

            result.Should().BeFalse();
            dependencyResolveCount.Should().Be(0, "metadata scanning should not resolve call tokens through the runtime loader");
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolver;
            TryDelete(dependencyPath);
            TryDelete(targetPath);
        }
    }

    private static void EmitAssembly(string path, string assemblyName, string source, params MetadataReference[] additionalReferences)
    {
        var references = new MetadataReference[additionalReferences.Length + 1];
        references[0] = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        Array.Copy(additionalReferences, 0, references, 1, additionalReferences.Length);

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var emitResult = compilation.Emit(path);
        emitResult.Success.Should().BeTrue(GetErrors(emitResult));
    }

    private static string GetErrors(EmitResult emitResult)
    {
        return string.Join(
            Environment.NewLine,
            emitResult.Diagnostics
                      .Where(d => d.Severity == DiagnosticSeverity.Error)
                      .Select(d => $"{d.Id}: {d.GetMessage()}"));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup for assemblies that may remain locked by the test runtime.
        }
    }

    // Test helper class
    private class TestClass
    {
        public void MethodWithExceptionDispatchInfoThrow()
        {
            try
            {
                throw new Exception("Test");
            }
            catch (Exception ex)
            {
                var edi = ExceptionDispatchInfo.Capture(ex);
                edi.Throw();
            }
        }

        public void MethodWithThrow()
        {
            var ex = new Exception("Test");
            ex.ToString();
        }

        public void MethodWithoutTargetCall()
        {
            var str = "Test";
            str.GetHashCode();
        }

        public async Task AsyncMethodWithThrow()
        {
            await Task.Delay(1);
            var ex = new Exception("Test");
            ex.ToString();
        }
    }
}
