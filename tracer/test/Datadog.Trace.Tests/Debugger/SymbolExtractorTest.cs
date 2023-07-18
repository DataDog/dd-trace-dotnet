// <copyright file="SymbolExtractorTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        Assert.True(root.Scopes.First().Scopes.First().Scopes.Count == 6);
        var settings = ConfigureVerifySettings(assembly.GetName().Name, className);
        var toVerify = GetStringToVerify(root);
        await Verifier.Verify(toVerify, settings);
    }

    private string GetStringToVerify(Root root)
    {
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
        var root = GetAssemblySymbol(assembly, "test");
        var classSymbol = _extractor.GetClassSymbols(assembly, new[] { className }).FirstOrDefault();
        root.Scopes[0].Scopes.Add(classSymbol);
        return root;
    }

    private Root GetAssemblySymbol(Assembly assembly, string serviceName)
    {
        var assemblyScope = new Datadog.Trace.Debugger.Symbols.Model.Scope
        {
            Name = assembly.FullName,
            ScopeType = SymbolType.Assembly,
            SourceFile = assembly.Location,
            StartLine = -1,
            EndLine = int.MaxValue,
            Scopes = new List<Trace.Debugger.Symbols.Model.Scope>()
        };

        var root = new Root
        {
            Service = serviceName,
            Env = string.Empty,
            Language = "dotnet",
            Version = string.Empty,
            Scopes = new[] { assemblyScope }
        };

        return root;
    }
}
