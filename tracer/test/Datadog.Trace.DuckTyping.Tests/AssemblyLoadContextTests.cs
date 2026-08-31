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
        var testAssemblyPath = Assembly.GetExecutingAssembly().Location;
        var assemblyDirectory = Path.GetDirectoryName(testAssemblyPath)!;
        var sharedAssemblyPath = Path.Combine(assemblyDirectory, SharedAssemblyName + ".dll");
        var targetAssemblyPath = Path.Combine(assemblyDirectory, TargetAssemblyName + ".dll");
        var targetContext = new AssemblyLoadContext("DuckTypingTarget");
        var proxyContext = new ProxyLoadContext(testAssemblyPath);

        // The target field and the generated proxy resolve the same dependency identity in different contexts.
        var targetSharedAssembly = targetContext.LoadFromAssemblyPath(sharedAssemblyPath);
        var proxySharedAssembly = proxyContext.LoadFromAssemblyPath(sharedAssemblyPath);
        targetSharedAssembly.FullName.Should().Be(proxySharedAssembly.FullName);
        targetSharedAssembly.Should().NotBeSameAs(proxySharedAssembly);

        var targetAssembly = targetContext.LoadFromAssemblyPath(targetAssemblyPath);
        var targetType = targetAssembly.GetType(TargetTypeName, throwOnError: true)!;
        var target = Activator.CreateInstance(targetType)!;

        var proxyAssembly = proxyContext.LoadFromAssemblyPath(testAssemblyPath);
        var proxyRunner = proxyAssembly.GetType(typeof(ProxyRunner).FullName!, throwOnError: true)!;

        var exception = Assert.Throws<TargetInvocationException>(
            () => proxyRunner.GetMethod(nameof(ProxyRunner.AccessField))!.Invoke(null, [target]));
        exception.InnerException.Should().BeOfType<MissingFieldException>();
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

    private sealed class ProxyLoadContext : AssemblyLoadContext
    {
        private readonly string _directory;

        public ProxyLoadContext(string testAssemblyPath)
            : base("DuckTypingProxy")
        {
            _directory = Path.GetDirectoryName(testAssemblyPath)!;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name != typeof(DuckType).Assembly.GetName().Name)
            {
                return null;
            }

            return LoadFromAssemblyPath(Path.Combine(_directory, assemblyName.Name + ".dll"));
        }
    }
}

#endif
