// <copyright file="DuckTypeAotEmitterMethodBindingTests.cs" company="Datadog">
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
/// The method binding layer of the registry emitter: the target method a proxy method calls is the one dynamic duck typing
/// selects (names, explicit interface names, the default binder), and the conversions of the return value.
/// </summary>
public class DuckTypeAotEmitterMethodBindingTests : IDisposable
{
    private static readonly string AssemblyName = typeof(DuckTypeAotEmitterMethodBindingTests).Assembly.GetName().Name!;
    private readonly ModuleDefMD _module = ModuleDefMD.Load(typeof(DuckTypeAotEmitterMethodBindingTests).Assembly.Location);

    public interface IOverloadsProxy
    {
        string Describe(string value);

        string Describe(object value);

        [Duck(Name = "Missing, Describe")]
        string DescribeWithFallback(int value);
    }

    public void Dispose() => _module.Dispose();

    [Theory]
    [InlineData(typeof(string), typeof(string), true)]
    [InlineData(typeof(string), typeof(object), true)]
    [InlineData(typeof(object), typeof(string), false)]
    [InlineData(typeof(int), typeof(long), true)]
    [InlineData(typeof(long), typeof(int), false)]
    [InlineData(typeof(OverloadsTarget), typeof(object), true)]
    public void TheDefaultBinderAcceptsTheArgumentsReflectionAccepts(Type argumentType, Type parameterType, bool expected)
    {
        // Type.DefaultBinder: an argument binds to a parameter of the same type, of a base type, or of a wider primitive.
        Invoke("IsDefaultBinderArgumentCompatible", new[] { typeof(TypeSig), typeof(TypeSig) }, Sig(argumentType), Sig(parameterType)).Should().Be(expected);
    }

    [Fact]
    public void TheTargetMethodNamesAreTheFallbackNamesOfTheDuckAttribute()
    {
        var proxy = FindType(typeof(IOverloadsProxy));
        var withFallback = proxy.Methods.Single(method => method.Name == nameof(IOverloadsProxy.DescribeWithFallback));
        var plain = proxy.Methods.First(method => method.Name == nameof(IOverloadsProxy.Describe));

        ((IReadOnlyList<string>)Invoke("GetForwardTargetMethodNames", new[] { typeof(MethodDef) }, withFallback)!).Should().Equal("Missing", "Describe");
        ((IReadOnlyList<string>)Invoke("GetForwardTargetMethodNames", new[] { typeof(MethodDef) }, plain)!).Should().Equal("Describe");
    }

    [Fact]
    public void ExplicitInterfaceImplementationsMatchTheirInterfaceName()
    {
        var types = new[] { typeof(string), typeof(string), typeof(IReadOnlyList<string>), typeof(bool), typeof(bool) };
        Invoke("IsForwardTargetMethodNameMatch", types, "Library.IContract.Run", "Run", new[] { "Library.IContract" }, false, false).Should().Be(true);
        Invoke("IsForwardTargetMethodNameMatch", types, "Library.Outer.IContract.Run", "Run", new[] { "Library.Outer+IContract" }, false, false).Should().Be(true);
        Invoke("IsForwardTargetMethodNameMatch", types, "Library.IOther.Run", "Run", new[] { "Library.IContract" }, false, false).Should().Be(false);
        Invoke("IsForwardTargetMethodNameMatch", types, "Library.IOther.Run", "Run", Array.Empty<string>(), true, false).Should().Be(true);
        Invoke("IsForwardTargetMethodNameMatch", types, "run", "Run", Array.Empty<string>(), false, true).Should().Be(true);
    }

    [Theory]
    [InlineData(typeof(object), typeof(int), "TypeConversion")]
    [InlineData(typeof(int), typeof(object), "TypeConversion")]
    [InlineData(typeof(string), typeof(string), "None")]
    [InlineData(typeof(IOverloadsProxy), typeof(OverloadsTarget), "DuckChainToProxy")]
    public void ReturnValuesAreConvertedLikeDynamicDuckTypingConvertsThem(Type proxyReturnType, Type targetReturnType, string expectedKind)
    {
        var arguments = new object?[] { Sig(proxyReturnType), Sig(targetReturnType), null };
        Invoke("TryCreateReturnConversion", new[] { typeof(TypeSig), typeof(TypeSig), Type.GetType("Datadog.Trace.Tools.Runner.DuckTypeAot.DuckTypeAotRegistryAssemblyEmitter+MethodReturnConversion, " + typeof(DuckTypeAotMapping).Assembly.FullName)!.MakeByRefType() }, arguments).Should().Be(true);
        var kind = arguments[2]!.GetType().GetProperty("Kind", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(arguments[2]);
        kind!.ToString().Should().Be(expectedKind);
    }

    private static object? Invoke(string name, Type[]? parameterTypes, params object?[] arguments)
    {
        var emitter = typeof(DuckTypeAotMapping).Assembly.GetType("Datadog.Trace.Tools.Runner.DuckTypeAot.DuckTypeAotRegistryAssemblyEmitter")!;
        var method = parameterTypes is null
                         ? emitter.GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(m => m.Name == name && m.GetParameters().Length == arguments.Length)
                         : emitter.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic, binder: null, parameterTypes, modifiers: null);
        method.Should().NotBeNull($"the emitter has {name}");
        return method!.Invoke(null, arguments);
    }

    private TypeSig Sig(Type type) => type.IsPrimitive || type == typeof(string) || type == typeof(object)
                                          ? _module.CorLibTypes.GetCorLibTypeSig(new TypeRefUser(_module, type.Namespace, type.Name, _module.CorLibTypes.AssemblyRef))!
                                          : FindType(type).ToTypeSig();

    private TypeDef FindType(Type type) => _module.Find(type.FullName, isReflectionName: true)!;

    public class OverloadsTarget
    {
        public string Describe(string value) => "string";

        public string Describe(object value) => "object";

        public string Describe(int value) => "int";
    }
}
