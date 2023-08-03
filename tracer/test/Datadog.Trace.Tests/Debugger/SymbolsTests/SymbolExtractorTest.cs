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
using Castle.Components.DictionaryAdapter;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Debugger.Symbols.Model;
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
        typeof(SymbolExtractorTest).Assembly.GetTypes().Where(t => t.Namespace == TestSamplesNamespace && !t.Name.StartsWith("<")).Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(TestSamples))]
    private async Task Test(Type type)
    {
        var assembly = Assembly.GetAssembly(type);
        Assert.NotNull(assembly);
        var root = GetSymbols(assembly, type.FullName);
        Assert.True(root.Scopes.Count == 1);
        Assert.True(root.Scopes.First().Scopes.Count == 1);
        Assert.True(root.Scopes.First().Scopes.First().Name == type.FullName);
        var settings = ConfigureVerifySettings(assembly.GetName().Name, type.FullName);
        var toVerify = GetStringToVerify(root);
        await Verifier.Verify(toVerify, settings);
    }

    [Fact]
    private void CompilerGeneratedClassTest()
    {
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
    }

    private string GetStringToVerify(Root root)
    {
        var assembly = root.Scopes[0];
        assembly.SourceFile = null;
        assembly.LanguageSpecifics = null;
        var classes = assembly.Scopes;
        var classesScope = new List<Trace.Debugger.Symbols.Model.Scope>();
        for (int i = 0; i < classes.Count; i++)
        {
            var @class = classes[0];
            @class.SourceFile = null;
            if (@class.LanguageSpecifics.HasValue)
            {
                var ls = @class.LanguageSpecifics.Value;
                ls.AccessModifiers = null; // depends on .net version
                @class.LanguageSpecifics = ls;
            }

            var methods = @class.Scopes;
            List<Trace.Debugger.Symbols.Model.Scope> methodsScope = new EditableList<Trace.Debugger.Symbols.Model.Scope>();
            for (int j = 0; j < methods.Count; j++)
            {
                var method = methods[j];
                if (method.LanguageSpecifics.HasValue)
                {
                    var ls = method.LanguageSpecifics.Value;
                    ls.AccessModifiers = null; // depends on .net version
                    method.LanguageSpecifics = ls;
                }

                methodsScope.Add(method);
            }

            @class.Scopes = methodsScope;
            classesScope.Add(@class);
        }

        assembly.Scopes = classesScope;
        root.Scopes = new[] { assembly };

        return JsonConvert.SerializeObject(root, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
    }

    private VerifySettings ConfigureVerifySettings(string assemblyName, string className)
    {
        var settings = new VerifySettings();
        settings.UseFileName($"{nameof(SymbolExtractorTest)}.{assemblyName}.{className}");
        settings.DisableRequireUniquePrefix();
        settings.UseDirectory("SymbolExtractor/Approvals");
        return settings;
    }

    private Root GetSymbols(Assembly assembly, string className)
    {
        var root = GetRoot();
        var extractor = SymbolExtractor.Create(assembly);
        Assert.NotNull(extractor);
        var assemblyScope = extractor.GetAssemblySymbol();
        var classSymbol = extractor.GetClassSymbols(className);
        assemblyScope.Scopes = classSymbol.HasValue ? new[] { classSymbol.Value } : null;
        root.Scopes = new[] { assemblyScope };
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
}
