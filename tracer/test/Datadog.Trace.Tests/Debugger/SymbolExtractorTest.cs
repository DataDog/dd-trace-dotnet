// <copyright file="SymbolExtractorTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Castle.Components.DictionaryAdapter;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

[UsesVerify]
public class SymbolExtractorTest
{
    private readonly SymbolExtractor _extractor;

    public SymbolExtractorTest()
    {
        _extractor = new SymbolExtractor();
    }

    [Theory]
    [InlineData(typeof(SymbolExtractorTest), "Datadog.Trace.Tests.Debugger.SymbolExtractorTest")]
    private async Task Test(System.Type type, string className)
    {
        var assembly = Assembly.GetAssembly(type);
        Assert.NotNull(assembly);
        var root = GetSymbols(assembly, className);
        Assert.True(root.Scopes.Count == 1);
        Assert.True(root.Scopes.First().Scopes.Count == 1);
        Assert.True(root.Scopes.First().Scopes.First().Name == className);
        var settings = ConfigureVerifySettings(assembly.GetName().Name, className);
        var toVerify = GetStringToVerify(root);
        await Verifier.Verify(toVerify, settings);
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
                ls.AccessModifiers = null;
                @class.LanguageSpecifics = ls;
            }

            var methods = @class.Scopes;
            List<Trace.Debugger.Symbols.Model.Scope> methodsScope = new EditableList<Trace.Debugger.Symbols.Model.Scope>();
            for (int j = 0; j < methods.Count; j++)
            {
                var method = methods[0];
                if (method.LanguageSpecifics.HasValue)
                {
                    var ls = method.LanguageSpecifics.Value;
                    ls.AccessModifiers = null;
                    method.LanguageSpecifics = ls;
                }

                methodsScope.Add(method);
            }

            @class.Scopes = methodsScope;
            classesScope.Add(@class);
        }

        assembly.Scopes = classesScope;
        root.Scopes = new[] { assembly };

        return JsonConvert.SerializeObject(root, Formatting.Indented);
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
        var assemblyScope = GetAssemblySymbol(assembly);
        var classSymbol = _extractor.GetClassSymbols(assembly, new[] { className }).FirstOrDefault();
        Assert.NotNull(classSymbol);
        assemblyScope.Scopes = new[] { classSymbol.Value };
        root.Scopes = new[] { assemblyScope };
        return root;
    }

    private Datadog.Trace.Debugger.Symbols.Model.Scope GetAssemblySymbol(Assembly assembly)
    {
        var assemblyScope = new Datadog.Trace.Debugger.Symbols.Model.Scope
        {
            Name = assembly.FullName,
            ScopeType = SymbolType.Assembly,
            SourceFile = assembly.Location,
            StartLine = -1,
            EndLine = int.MaxValue,
        };

        return assemblyScope;
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
