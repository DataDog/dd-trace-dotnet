// <copyright file="CallTargetDuckTypeRegistryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Tools.Runner.Aot.CallTarget;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The DuckType AOT registry of the proxies the NativeAOT CallTarget adapters create.
/// </summary>
public class CallTargetDuckTypeRegistryTests
{
    [Fact]
    public void ProxiesLookedUpByRuntimeTypeGetAProxyPerRuntimeTypeUnlessTheyBindNoMember()
    {
        // The slow begin path (more than 8 arguments) looks the proxies of the arguments up by runtime type
        // (IntegrationMapper.ConvertType): the proxy of each type assignable to the static type, like dynamic duck typing creates
        // them. A marker interface binds no member: the proxy of System.Object serves every type, instead of an alias per type of
        // the inputs (for System.Object, thousands, which the NativeAOT compiler of Windows can't compile).
        var assemblyPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [typeof(CallTargetDuckTypeRegistryTests).Assembly.GetName().Name!] = typeof(CallTargetDuckTypeRegistryTests).Assembly.Location,
            ["System.Runtime"] = Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll"),
            ["System.Private.CoreLib"] = typeof(object).Assembly.Location,
            ["Datadog.Trace"] = typeof(Tracer).Assembly.Location,
        };
        var modules = assemblyPaths.Values.Select(path => ModuleDefMD.Load(path)).ToList();
        var module = modules.Single(m => m.Location == typeof(CallTargetDuckTypeRegistryTests).Assembly.Location);
        var resolver = new LoadedModulesTypeResolver(modules);
        TypeSig Sig(Type type) => module.Find(type.FullName!, isReflectionName: true)!.ToTypeSig();

        var directory = Path.Combine(Path.GetTempPath(), "dd-trace-aot-calltarget-registry", Guid.NewGuid().ToString("N"));
        try
        {
            var registry = CallTargetDuckTypeRegistry.Build(
                [],
                [(Sig(typeof(IMarkerProxy)), module.CorLibTypes.Object), (Sig(typeof(IMarkerProxy)), Sig(typeof(RuntimeTarget))), (Sig(typeof(INameProxy)), Sig(typeof(RuntimeTarget)))],
                [],
                assemblyPaths,
                directory,
                "Datadog.Trace.DuckType.AotRegistry.CallTargetDuckTypeRegistryTests",
                typeof(Tracer).Assembly.Location,
                resolver.Resolve);

            registry.Should().NotBeNull();
            var proxies = registry!.Module.GetTypes()
                                   .Where(type => type.Interfaces.Any(i => i.Interface.FullName == "Datadog.Trace.DuckTyping.IDuckType"))
                                   .Select(type => type.Fields.Single(field => field.Name == "_currentInstance").FieldSig.Type.FullName)
                                   .ToList();
            proxies.Should().BeEquivalentTo(
                "System.Object",
                typeof(RuntimeTarget).FullName!.Replace('+', '/'),
                typeof(DerivedRuntimeTarget).FullName!.Replace('+', '/'));
        }
        finally
        {
            modules.ForEach(m => m.Dispose());
            Directory.Delete(directory, recursive: true);
        }
    }

    public interface IMarkerProxy
    {
    }

    public interface INameProxy
    {
        string Name { get; }
    }

    public class RuntimeTarget
    {
        public string Name => "base";
    }

    public class DerivedRuntimeTarget : RuntimeTarget
    {
        public new string Name => "derived";
    }
}
#endif
