// <copyright file="SymbolExtractorTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests;

[UsesVerify]
public class SymbolExtractorTest
{
    private const string TestSamplesNamespace = "Datadog.Trace.Tests.Debugger.SymbolsTests.TestSamples";

    public static IEnumerable<object[]> TestSamples =>
        typeof(SymbolExtractorTest).Assembly.GetTypes().Where(t => t.Namespace == TestSamplesNamespace && !t.Name.StartsWith("<") && !t.IsNested).Select(type => new object[] { type });

    [SkippableTheory]
    [Trait("Category", "LinuxUnsupported")]
    [MemberData(nameof(TestSamples))]
    private async Task Test(Type type)
    {
#if DEBUG
        // silence compiler warnings
        _ = type;
        await Task.Yield();

        throw new SkipException("This test requires RELEASE mode and will always fail in DEBUG mode");
#elif NETFRAMEWORK
        // silence compiler warnings
        _ = type;
        await Task.Yield();

        throw new SkipException("This test is flaky - The .NET Framework snapshots produced are different in CI and locally");
#else
        if (!EnvironmentTools.IsWindows())
        {
            throw new SkipException("PDB test only on windows");
        }

        var assembly = Assembly.GetAssembly(type);
        Assert.NotNull(assembly);
        var root = GetSymbols(assembly, type.FullName);
        Assert.True(root.Scopes.Count == 1);
        Assert.True(root.Scopes.First().Scopes.Length == 1);
        Assert.True(root.Scopes.First().Scopes.First().Name == type.FullName);
        var settings = ConfigureVerifySettings(type.Name);
        var toVerify = GetStringToVerify(root);
        await Verifier.Verify(toVerify, settings);
#endif
    }

    [SkippableTheory(Skip = "Implement this")]
    [MemberData(nameof(TestSamples))]
    private async Task TestDnlib(Type type)
    {
        if (!EnvironmentTools.IsWindows())
        {
            throw new SkipException("PDB test only on windows");
        }

        var assembly = Assembly.GetAssembly(type);
        Assert.NotNull(assembly);
        var root = GetSymbols(assembly, type.FullName);
        Assert.True(root.Scopes.Count == 1);
        Assert.True(root.Scopes.First().Scopes.Length == 1);
        Assert.True(root.Scopes.First().Scopes.First().Name == type.FullName);
        var settings = ConfigureVerifySettings(type.Name);
        var toVerify = GetStringToVerify(root);
        await Verifier.Verify(toVerify, settings);
    }

    [SkippableFact]
    private void CompilerGeneratedClassTest()
    {
#if NETFRAMEWORK
        throw new SkipException("This test is flaky - root.Scopes.First().Scopes is sometimes not null on .NET Framework");
#else
        var assembly = Assembly.GetExecutingAssembly();
        Assert.NotNull(assembly);
        var compilerGeneratedTypes = CompilerGeneratedTypes(10);
        Assert.True(compilerGeneratedTypes.Count == 10);
        foreach (var generatedType in compilerGeneratedTypes)
        {
            Assert.NotNull(generatedType);
            var root = GetSymbols(assembly, generatedType.FullName);
            Assert.True(root.Scopes.Count == 1);
            Assert.Null(root.Scopes.First().Scopes);
        }

        List<Type> CompilerGeneratedTypes(int numberOfTypes)
        {
            return assembly.GetTypes().SelectMany(t => t.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)).Where(t => t.GetCustomAttribute<CompilerGeneratedAttribute>() != null).Take(numberOfTypes).ToList();
        }
#endif
    }

    [SkippableFact]
    private void StaticMethodClosureScopeDoesNotExposeThisSymbol()
    {
#if DEBUG
        throw new SkipException("This test requires RELEASE mode and will always fail in DEBUG mode");
#elif NETFRAMEWORK
        throw new SkipException("This test is flaky - The .NET Framework snapshots produced are different in CI and locally");
#else
        if (!EnvironmentTools.IsWindows())
        {
            throw new SkipException("PDB test only on windows");
        }

        var type = typeof(TestSamples.StaticHoistedLocalsInStateMachine);
        var assembly = Assembly.GetAssembly(type);
        Assert.NotNull(assembly);

        var root = GetSymbols(assembly, type.FullName);
        var classScope = root.Scopes.Single().Scopes.Single();

        Assert.NotNull(classScope.Scopes);
        var doMethod = classScope.Scopes.Single(s => s.ScopeType == ScopeType.Method && s.Name == "DoAsyncWork");

        Assert.NotNull(doMethod.Scopes);
        var closureScopes = doMethod.Scopes.Where(s => s.ScopeType == ScopeType.Closure).ToArray();
        Assert.NotEmpty(closureScopes);

        foreach (var closureScope in closureScopes)
        {
            if (closureScope.Symbols is null)
            {
                continue;
            }

            Assert.DoesNotContain(closureScope.Symbols, s => s.SymbolType == SymbolType.Arg && s.Name == "this");
        }
#endif
    }

    [SkippableFact]
    private void StaticMethodClosureScopeWithOtherArgsStillDoesNotExposeThisSymbol()
    {
#if DEBUG
        throw new SkipException("This test requires RELEASE mode and will always fail in DEBUG mode");
#elif NETFRAMEWORK
        throw new SkipException("This test is flaky - The .NET Framework snapshots produced are different in CI and locally");
#else
        if (!EnvironmentTools.IsWindows())
        {
            throw new SkipException("PDB test only on windows");
        }

        var type = typeof(TestSamples.StaticLambdaWithParameter);
        var assembly = Assembly.GetAssembly(type);
        Assert.NotNull(assembly);

        var root = GetSymbols(assembly, type.FullName);
        var classScope = root.Scopes.Single().Scopes.Single();

        Assert.NotNull(classScope.Scopes);
        var fooMethod = classScope.Scopes.Single(s => s.ScopeType == ScopeType.Method && s.Name == "Foo");

        Assert.NotNull(fooMethod.Scopes);
        var closureScopes = fooMethod.Scopes.Where(s => s.ScopeType == ScopeType.Closure).ToArray();
        Assert.NotEmpty(closureScopes);

        // Ensure we have at least one closure scope that still exposes non-this args,
        // and verify none of them include a 'this' arg.
        var foundClosureWithArgs = false;
        foreach (var closureScope in closureScopes)
        {
            if (closureScope.Symbols is { Length: > 0 } symbols)
            {
                foundClosureWithArgs = true;
                Assert.DoesNotContain(symbols, s => s.SymbolType == SymbolType.Arg && s.Name == "this");
            }
        }

        Assert.True(foundClosureWithArgs);
#endif
    }

    private string GetStringToVerify(Root root)
    {
        var assembly = root.Scopes[0];
        assembly.SourceFile = null;
        assembly.LanguageSpecifics = null;
        for (int i = 0; i < assembly.Scopes.Length; i++)
        {
            SanitizeScopeToVerify(ref assembly.Scopes[i]);
        }

        root.Scopes = new[] { assembly };

        return JsonConvert.SerializeObject(root, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        void SanitizeScopeToVerify(ref Trace.Debugger.Symbols.Model.Scope scope)
        {
            if (scope == default)
            {
                return;
            }

            var indexOfPathStart = scope.SourceFile?.IndexOf("\\tracer", StringComparison.Ordinal) + 1;
            if (indexOfPathStart > 0)
            {
                // relative path
                scope.SourceFile = scope.SourceFile.Substring(indexOfPathStart.Value);
            }

            if (scope.LanguageSpecifics.HasValue)
            {
                var ls = scope.LanguageSpecifics.Value;
                // depends on .net version
                ls.AccessModifiers = ls.AccessModifiers?.Count > 0 ? new[] { "sanitized" } : null;

                // depends on build optimization
                ls.StartColumn = ls.StartColumn is > 0 ? 999 : null;
                ls.EndColumn = ls.EndColumn is > 0 ? 999 : null;
                scope.LanguageSpecifics = ls;
            }

            // depends on build optimization
            scope.EndLine = scope.EndLine > 0 ? 999 : 0;

            if (scope.Scopes == null)
            {
                return;
            }

            for (int i = 0; i < scope.Scopes.Length; i++)
            {
                SanitizeScopeToVerify(ref scope.Scopes[i]);
            }
        }
    }

    private VerifySettings ConfigureVerifySettings(string className)
    {
        var settings = new VerifySettings();
        settings.UseFileName($"{nameof(SymbolExtractorTest)}.{className}");
        settings.DisableRequireUniquePrefix();
        settings.UseDirectory("SymbolExtractor/Approvals");
        return settings;
    }

    private Root GetSymbols(Assembly assembly, string className)
    {
        var root = GetRoot();
        var extractor = SymbolExtractor.Create(assembly);
        Assert.NotNull(extractor);
        var result = extractor.TryGetAssemblySymbol(out var assemblyScope);
        Assert.True(result);
        var classSymbol = extractor.GetClassSymbols(className);
        assemblyScope.Scopes = classSymbol.HasValue ? [classSymbol.Value] : null;
        root.Scopes = [assemblyScope];
        return root;
    }

    private Root GetRoot()
    {
        var root = new Root
        {
            Service = "test",
            Env = "test",
            Language = "dotnet",
            Version = "0",
        };

        return root;
    }

    [SkippableFact]
    [Trait("Category", "LinuxUnsupported")]
    private void DoesNotDropClosureMethodsWhenThereAreMany()
    {
#if DEBUG
        throw new SkipException("This test requires RELEASE mode (PDB/sequence points differ in DEBUG mode)");
#elif NETFRAMEWORK
        throw new SkipException("PDB-based symbol extraction tests are flaky on .NET Framework");
#else
        if (!EnvironmentTools.IsWindows())
        {
            throw new SkipException("PDB test only on windows");
        }

        var type = typeof(ClosureOverflowSamples.ManyClosures);
        var assembly = Assembly.GetAssembly(type);
        Assert.NotNull(assembly);

        var extractor = SymbolExtractor.Create(assembly);
        Assert.NotNull(extractor);

        var classSymbol = extractor.GetClassSymbols(type.FullName);
        Assert.True(classSymbol.HasValue);

        var classScope = classSymbol.Value;
        Assert.NotNull(classScope.Scopes);

        var runMethodScope = classScope.Scopes.Single(s => s.ScopeType == ScopeType.Method && s.Name == nameof(ClosureOverflowSamples.ManyClosures.Run));
        var closureCount = runMethodScope.Scopes?.Count(s => s.ScopeType == ScopeType.Closure) ?? 0;

        Assert.Equal(ClosureOverflowSamples.ManyClosures.ClosureCount, closureCount);
#endif
    }
}
