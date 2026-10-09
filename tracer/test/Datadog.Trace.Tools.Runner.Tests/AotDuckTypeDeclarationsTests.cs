// <copyright file="AotDuckTypeDeclarationsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Linq;
using Datadog.Trace.Tools.Runner.Aot.CallTarget;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The duck typing mappings Datadog.Trace declares with attributes ([DuckType], [DuckCopy], [DuckReverseDelegation],
/// [assembly: DuckTypeMapping]), which a NativeAOT build generates the proxies of.
/// </summary>
public class AotDuckTypeDeclarationsTests
{
    [Fact]
    public void DeclarationsAreWellFormed()
    {
        var result = DuckTypeAotAttributeDiscovery.Discover([typeof(Tracer).Assembly.Location]);
        result.Errors.Should().BeEmpty();
        result.Warnings.Where(w => w.StartsWith("Skipping", System.StringComparison.Ordinal)).Should().BeEmpty();

        // A renamed or removed proxy of Datadog.Trace would leave a declaration a NativeAOT build reports as incompatible.
        var datadogTrace = ModuleDefMD.Load(typeof(Tracer).Assembly.Location);
        var datadogTypes = result.Mappings.Select(m => m.Mode == DuckTypeAotMappingMode.Forward ? (m.ProxyAssemblyName, m.ProxyTypeName) : (m.TargetAssemblyName, m.TargetTypeName))
                                 .Where(t => t.Item1 == "Datadog.Trace")
                                 .Select(t => t.Item2.Split('[')[0])
                                 .Distinct()
                                 .ToList();
        datadogTypes.Should().NotBeEmpty();
        datadogTypes.Where(name => datadogTrace.Find(name, isReflectionName: true) is null).Should().BeEmpty();

        result.Mappings.Should().Contain(m => m.IncludesDerivedTypes && m.TargetTypeName == "Amazon.Runtime.AmazonWebServiceResponse");
        result.Mappings.Should().Contain(m => m.Mode == DuckTypeAotMappingMode.Reverse && m.ProxyTypeName == "Microsoft.Extensions.Logging.ILogger");
        result.Mappings.Should().Contain(m => m.ProxyAssemblyName == "Datadog.Trace.Manual" && m.ProxyTypeName == "Datadog.Trace.IScope");
    }

    [Fact]
    public void RecordedMappingsTheDeclarationsMissAreListed()
    {
        var module = ModuleDefMD.Load(typeof(AotDuckTypeDeclarationsTests).Assembly.Location);
        var assemblyName = module.Assembly.Name.String;
        TypeDef? FindType(string assembly, string name) => assembly == assemblyName ? module.Find(name, isReflectionName: true) : null;
        TypeSig Sig(Type type) => FindType(assemblyName, type.FullName!)!.ToTypeSig();

        var declared = new[] { Forward(typeof(IDeclaredProxy), typeof(DeclaredTarget)), Forward(typeof(IDeclaredProxy), typeof(DeclaredBase)).WithDerivedTypes() };
        var requests = new[] { (Sig(typeof(IRequestedProxy)), Sig(typeof(DeclaredTarget))) };
        var undeclaredTarget = Forward(typeof(IDeclaredProxy), typeof(UndeclaredTarget));
        var undeclaredReverse = new DuckTypeAotMapping(typeof(DeclaredBase).FullName!, "App", typeof(DeclaredTarget).FullName!, assemblyName, DuckTypeAotMappingMode.Reverse, DuckTypeAotMappingSource.MapFile);
        var recorded = new[]
        {
            Forward(typeof(IDeclaredProxy), typeof(DeclaredTarget)),
            Forward(typeof(IDeclaredProxy), typeof(DeclaredGrandChild)),
            Forward(typeof(IRequestedProxy), typeof(DeclaredTarget)),
            undeclaredTarget,
            undeclaredReverse,
            new DuckTypeAotMapping(typeof(IDeclaredProxy).FullName!, "App", typeof(UndeclaredTarget).FullName!, assemblyName, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile),
        };

        CallTargetDuckTypeRegistry.FindUndeclared(requests, [], declared, recorded, FindType, type => type.ResolveTypeDef())
                                  .Should().BeEquivalentTo(undeclaredTarget.Key, undeclaredReverse.Key);

        DuckTypeAotMapping Forward(Type proxy, Type target)
            => new(proxy.FullName!, assemblyName, target.FullName!, assemblyName, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile);
    }

    public interface IDeclaredProxy
    {
    }

    public interface IRequestedProxy
    {
    }

    public class DeclaredTarget
    {
    }

    public class DeclaredBase
    {
    }

    public class DeclaredChild : DeclaredBase
    {
    }

    public class DeclaredGrandChild : DeclaredChild
    {
    }

    public class UndeclaredTarget
    {
    }
}
#endif
