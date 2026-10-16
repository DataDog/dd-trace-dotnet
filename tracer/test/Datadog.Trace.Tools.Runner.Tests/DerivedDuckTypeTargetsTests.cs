// <copyright file="DerivedDuckTypeTargetsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.IO;
using System.Linq;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.Aot.CallTarget;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.
#pragma warning disable SA1402 // The derived classes of the test are its fixtures.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The classes deriving from the target class of a <c>[DuckType(IncludeDerivedTypes = true)]</c> declaration that dynamic duck
/// typing binds differently from the target class, which get proxies of their own in NativeAOT.
/// </summary>
public class DerivedDuckTypeTargetsTests
{
    [Fact]
    public void ClassesDeclaringAMemberTheProxyBindsGetAProxyOfTheirOwn()
    {
        var assembly = typeof(DerivedDuckTypeTargetsTests).Assembly;
        var modules = new[] { assembly.Location, typeof(object).Assembly.Location, Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll") }
                     .Select(path => ModuleDefMD.Load(path))
                     .ToList();
        try
        {
            var module = modules[0];
            var resolver = new LoadedModulesTypeResolver(modules);
            var mapping = new DuckTypeAotMapping(typeof(IDerivedTargetProxy).FullName!, assembly.GetName().Name!, typeof(DerivedTargetBase).FullName!, assembly.GetName().Name!, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.Attribute, includesDerivedTypes: true);
            TypeDef? FindType(string assemblyName, string typeName) => assemblyName == assembly.GetName().Name ? module.Find(typeName, isReflectionName: true) : null;

            var derived = DerivedDuckTypeTargets.Find([mapping], [module], FindType, resolver.Resolve);

            derived.Should().OnlyContain(d => d.Mapping == mapping);
            derived.Select(d => d.Target.ReflectionFullName).Should().BeEquivalentTo(
                typeof(DerivedHidingName).FullName,
                typeof(DerivedOfHiding).FullName,
                typeof(DerivedAddingFallbackName).FullName,
                typeof(DerivedWithSameNamedField).FullName);

            // Without IncludeDerivedTypes, the derived classes have no runtime lookup through the target's proxy.
            DerivedDuckTypeTargets.Find([new DuckTypeAotMapping(mapping.ProxyTypeName, mapping.ProxyAssemblyName, mapping.TargetTypeName, mapping.TargetAssemblyName, mapping.Mode, mapping.Source)], [module], FindType, resolver.Resolve)
                                  .Should().BeEmpty();
        }
        finally
        {
            modules.ForEach(m => m.Dispose());
        }
    }

    public interface IDerivedTargetProxy
    {
        string Name { get; }

        [Duck(Name = "Missing, Value")]
        int Value { get; }

        string Echo(string value);
    }
}

public class DerivedTargetBase
{
    private string _label = "base";

    public virtual string Name => "base";

    public int Value => 1;

    public string Echo(string value) => value + _label;
}

// Overrides are called through virtual dispatch by the proxy of the base class.
public class DerivedOverridingName : DerivedTargetBase
{
    public override string Name => "override";
}

public class DerivedHidingName : DerivedTargetBase
{
    public new string Name => "hidden";
}

public class DerivedOfHiding : DerivedHidingName
{
}

public class DerivedAddingFallbackName : DerivedTargetBase
{
    public int Missing => 2;
}

public class DerivedWithSameNamedField : DerivedTargetBase
{
#pragma warning disable SA1401, SA1306 // A field named like a bound member.
    protected new string? Echo;
#pragma warning restore SA1401, SA1306
}

public class DerivedWithUnrelatedMember : DerivedTargetBase
{
    public string Other => "other";
}
#endif
