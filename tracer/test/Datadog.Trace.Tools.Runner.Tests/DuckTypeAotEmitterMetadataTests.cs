// <copyright file="DuckTypeAotEmitterMetadataTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The metadata layer of the registry emitter: it reads the duck typing attributes of a proxy from metadata the way dynamic
/// duck typing reads them through reflection, and compares metadata types.
/// </summary>
public class DuckTypeAotEmitterMetadataTests : IDisposable
{
    private readonly ModuleDefMD _module = ModuleDefMD.Load(typeof(DuckTypeAotEmitterMetadataTests).Assembly.Location);

    public interface IMetadataReadingProxy
    {
        [Duck(Name = "First, Second", BindingFlags = BindingFlags.NonPublic | BindingFlags.Instance, FallbackToBaseTypes = true)]
        string Value { get; }

        [DuckIgnore]
        void Ignored();
    }

    public void Dispose() => _module.Dispose();

    [Fact]
    public void DuckAttributesAreReadFromMetadataLikeReflectionReadsThem()
    {
        var proxy = FindType(typeof(IMetadataReadingProxy));
        var value = proxy.Properties.Single(property => property.Name == nameof(IMetadataReadingProxy.Value));
        var ignored = proxy.Methods.Single(method => method.Name == nameof(IMetadataReadingProxy.Ignored));

        var arguments = new object?[] { value.CustomAttributes, null };
        Invoke<bool>("TryGetDuckAttributeNames", new[] { typeof(IList<CustomAttribute>), typeof(IReadOnlyList<string>).MakeByRefType() }, arguments).Should().BeTrue();
        ((IReadOnlyList<string>)arguments[1]!).Should().Equal("First", "Second");
        Invoke<BindingFlags>("GetDuckBindingFlags", new[] { typeof(IList<CustomAttribute>) }, value.CustomAttributes).Should().Be(BindingFlags.NonPublic | BindingFlags.Instance);
        Invoke<bool>("IsFallbackToBaseTypesEnabled", new[] { typeof(IList<CustomAttribute>) }, value.CustomAttributes).Should().BeTrue();
        Invoke<bool>("IsDuckIgnoreAttribute", new[] { typeof(CustomAttribute) }, ignored.CustomAttributes.Single()).Should().BeTrue();
        Invoke<bool>("IsDuckAttribute", new[] { typeof(CustomAttribute) }, value.CustomAttributes.Single()).Should().BeTrue();
    }

    [Fact]
    public void FallbackNamesAreSplitOutsideGenericArguments()
    {
        Invoke<IEnumerable<string>>("SplitDuckNames", new[] { typeof(string) }, "IDictionary<String,Object>.TryGetValue, TryGetValue")
           .Should().Equal("IDictionary<String,Object>.TryGetValue", "TryGetValue");
    }

    [Fact]
    public void AssignabilityAndEquivalenceAreDecidedFromMetadata()
    {
        var baseType = FindType(typeof(MetadataBase));
        var derivedType = FindType(typeof(MetadataDerived));
        var types = new[] { typeof(ITypeDefOrRef), typeof(ITypeDefOrRef) };

        Invoke<bool>("IsAssignableFrom", types, baseType, derivedType).Should().BeTrue();
        Invoke<bool>("IsAssignableFrom", types, derivedType, baseType).Should().BeFalse();

        using var otherLoad = ModuleDefMD.Load(typeof(DuckTypeAotEmitterMetadataTests).Assembly.Location);
        var sameTypeElsewhere = otherLoad.Find(typeof(MetadataDerived).FullName, isReflectionName: true)!;
        Invoke<bool>("AreTypesEquivalent", new[] { typeof(TypeSig), typeof(TypeSig) }, derivedType.ToTypeSig(), sameTypeElsewhere.ToTypeSig()).Should().BeTrue();
        Invoke<bool>("AreTypesEquivalent", new[] { typeof(TypeSig), typeof(TypeSig) }, derivedType.ToTypeSig(), baseType.ToTypeSig()).Should().BeFalse();
    }

    [Fact]
    public void ShortHashesAreStable()
    {
        var hash = Invoke<string>("ComputeStableShortHash", new[] { typeof(string) }, "Proxies.IProxy|Targets.Target");
        Invoke<string>("ComputeStableShortHash", new[] { typeof(string) }, "Proxies.IProxy|Targets.Target").Should().Be(hash);
        Invoke<string>("ComputeStableShortHash", new[] { typeof(string) }, "Proxies.IProxy|Targets.Other").Should().NotBe(hash);
    }

    private static T Invoke<T>(string name, Type[] parameterTypes, params object?[] arguments)
    {
        var method = typeof(DuckTypeAotMapping).Assembly.GetType("Datadog.Trace.Tools.Runner.DuckTypeAot.DuckTypeAotRegistryAssemblyEmitter")!
                                               .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic, binder: null, parameterTypes, modifiers: null);
        method.Should().NotBeNull($"the emitter has {name}");
        return (T)method!.Invoke(null, arguments)!;
    }

    private TypeDef FindType(Type type) => _module.Find(type.FullName, isReflectionName: true)!;

    public class MetadataBase
    {
    }

    public class MetadataDerived : MetadataBase
    {
    }
}
