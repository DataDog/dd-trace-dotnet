// <copyright file="AssemblyLoadContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP2_1_OR_GREATER

#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Datadog.Trace.DuckTyping.Tests.Fixtures.Shared;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests;

public class AssemblyLoadContextTests
{
    private const string SharedAssemblyName = "Datadog.Trace.DuckTyping.Tests.Fixtures.Shared";
    private const string TargetAssemblyName = "Datadog.Trace.DuckTyping.Tests.Fixtures.Target";
    private const string TargetTypeName = "Datadog.Trace.DuckTyping.Tests.Fixtures.Target.DuckTypingTarget";

    [Fact]
    public void DuckFieldAcrossAssemblyLoadContexts()
    {
        var defaultContext = AssemblyLoadContext.Default;
        var targetContext = new TestAssemblyLoadContext();

        var defaultSharedAssembly = typeof(FieldValue).Assembly;
        AssemblyLoadContext.GetLoadContext(typeof(DuckType).Assembly).Should().BeSameAs(defaultContext);
        AssemblyLoadContext.GetLoadContext(typeof(ProxyRunner).Assembly).Should().BeSameAs(defaultContext);
        AssemblyLoadContext.GetLoadContext(defaultSharedAssembly).Should().BeSameAs(defaultContext);
        AppDomain.CurrentDomain.GetAssemblies().Should().NotContain(
            assembly => AssemblyLoadContext.GetLoadContext(assembly) == defaultContext && assembly.GetName().Name == TargetAssemblyName);

        // The target field and the generated proxy resolve the same dependency identity in different contexts,
        // matching Azure Functions where Event Grid has a separate Azure.Core copy in its load context.
        var targetType = LoadTargetType(targetContext, out var targetSharedAssembly);
        targetSharedAssembly.FullName.Should().Be(defaultSharedAssembly.FullName);
        targetSharedAssembly.Should().NotBeSameAs(defaultSharedAssembly);
        AssemblyLoadContext.GetLoadContext(targetSharedAssembly).Should().BeSameAs(targetContext);
        var targetField = targetType.GetField("_field", BindingFlags.Instance | BindingFlags.NonPublic)!;
        targetField.FieldType.Assembly.Should().BeSameAs(targetSharedAssembly);
        var target = Activator.CreateInstance(targetType)!;

        ProxyRunner.AccessField(target);

        var proxyType = DuckType.GetOrCreateProxyType(typeof(ProxyRunner.ITargetProxy), targetType).ProxyType!;
        proxyType.Assembly.IsDynamic.Should().BeTrue();
        AssemblyLoadContext.GetLoadContext(proxyType.Assembly).Should().BeSameAs(targetContext);
    }

    [Fact]
    public void CachesProxyPerTargetAssemblyLoadContext()
    {
        var firstContext = new TestAssemblyLoadContext();
        var secondContext = new TestAssemblyLoadContext();
        var firstTargetType = LoadTargetType(firstContext, out _);
        var secondTargetType = LoadTargetType(secondContext, out _);

        firstTargetType.FullName.Should().Be(secondTargetType.FullName);
        firstTargetType.Assembly.FullName.Should().Be(secondTargetType.Assembly.FullName);
        firstTargetType.Should().NotBeSameAs(secondTargetType);

        ProxyRunner.AccessField(Activator.CreateInstance(firstTargetType)!);
        ProxyRunner.AccessField(Activator.CreateInstance(secondTargetType)!);

        var firstProxyType = DuckType.GetOrCreateProxyType(typeof(ProxyRunner.ITargetProxy), firstTargetType).ProxyType!;
        var cachedFirstProxyType = DuckType.GetOrCreateProxyType(typeof(ProxyRunner.ITargetProxy), firstTargetType).ProxyType!;
        var secondProxyType = DuckType.GetOrCreateProxyType(typeof(ProxyRunner.ITargetProxy), secondTargetType).ProxyType!;

        cachedFirstProxyType.Should().BeSameAs(firstProxyType);
        secondProxyType.Should().NotBeSameAs(firstProxyType);
        AssemblyLoadContext.GetLoadContext(firstProxyType.Assembly).Should().BeSameAs(firstContext);
        AssemblyLoadContext.GetLoadContext(secondProxyType.Assembly).Should().BeSameAs(secondContext);
    }

    private static Type LoadTargetType(AssemblyLoadContext targetContext, out Assembly targetSharedAssembly)
    {
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var fixtureDirectory = Path.Combine(assemblyDirectory, "AssemblyLoadContextFixtures");
        var sharedAssemblyPath = Path.Combine(fixtureDirectory, SharedAssemblyName + ".dll");
        var targetAssemblyPath = Path.Combine(fixtureDirectory, TargetAssemblyName + ".dll");

        targetSharedAssembly = targetContext.LoadFromAssemblyPath(sharedAssemblyPath);
        var targetAssembly = targetContext.LoadFromAssemblyPath(targetAssemblyPath);
        AssemblyLoadContext.GetLoadContext(targetAssembly).Should().BeSameAs(targetContext);
        return targetAssembly.GetType(TargetTypeName, throwOnError: true)!;
    }

    public static class ProxyRunner
    {
        internal interface ITargetProxy
        {
            [DuckField(Name = "_field")]
            IFieldValue? Field { get; }
        }

        internal interface IFieldValue
        {
        }

        public static void AccessField(object target)
        {
            _ = target.DuckCast<ITargetProxy>().Field;
        }
    }

    private sealed class TestAssemblyLoadContext : AssemblyLoadContext
    {
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}

#endif
