// <copyright file="AssemblyLoadContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

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
    public void DuckFieldThrowsMissingFieldExceptionAcrossAssemblyLoadContexts()
    {
        var defaultContext = AssemblyLoadContext.Default;
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var fixtureDirectory = Path.Combine(assemblyDirectory, "AssemblyLoadContextFixtures");
        var sharedAssemblyPath = Path.Combine(fixtureDirectory, SharedAssemblyName + ".dll");
        var targetAssemblyPath = Path.Combine(fixtureDirectory, TargetAssemblyName + ".dll");
        var targetContext = new AssemblyLoadContext("DuckTypingTarget");

        var defaultSharedAssembly = typeof(FieldValue).Assembly;
        AssemblyLoadContext.GetLoadContext(typeof(DuckType).Assembly).Should().BeSameAs(defaultContext);
        AssemblyLoadContext.GetLoadContext(typeof(ProxyRunner).Assembly).Should().BeSameAs(defaultContext);
        AssemblyLoadContext.GetLoadContext(defaultSharedAssembly).Should().BeSameAs(defaultContext);
        defaultContext.Assemblies.Should().NotContain(assembly => assembly.GetName().Name == TargetAssemblyName);

        // The target field and the generated proxy resolve the same dependency identity in different contexts,
        // matching Azure Functions where Event Grid has a separate Azure.Core copy in its load context.
        var targetSharedAssembly = targetContext.LoadFromAssemblyPath(sharedAssemblyPath);
        targetSharedAssembly.FullName.Should().Be(defaultSharedAssembly.FullName);
        targetSharedAssembly.Should().NotBeSameAs(defaultSharedAssembly);
        AssemblyLoadContext.GetLoadContext(targetSharedAssembly).Should().BeSameAs(targetContext);

        var targetAssembly = targetContext.LoadFromAssemblyPath(targetAssemblyPath);
        AssemblyLoadContext.GetLoadContext(targetAssembly).Should().BeSameAs(targetContext);
        var targetType = targetAssembly.GetType(TargetTypeName, throwOnError: true)!;
        var targetField = targetType.GetField("_field", BindingFlags.Instance | BindingFlags.NonPublic)!;
        targetField.FieldType.Assembly.Should().BeSameAs(targetSharedAssembly);
        var target = Activator.CreateInstance(targetType)!;

        Assert.Throws<MissingFieldException>(() => ProxyRunner.AccessField(target));

        var proxyType = DuckType.GetOrCreateProxyType(typeof(ProxyRunner.ITargetProxy), targetType).ProxyType!;
        proxyType.Assembly.IsDynamic.Should().BeTrue();
        AssemblyLoadContext.GetLoadContext(proxyType.Assembly).Should().BeSameAs(defaultContext);
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
}

#endif
