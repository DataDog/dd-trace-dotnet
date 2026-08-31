// <copyright file="AssemblyLoadContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.DuckTyping.Tests;

public class AssemblyLoadContextTests
{
    [Fact]
    public void DuckFieldThrowsMissingFieldExceptionAcrossAssemblyLoadContexts()
    {
        var testAssemblyPath = Assembly.GetExecutingAssembly().Location;
        var targetContext = new AssemblyLoadContext("DuckTypingTarget");
        var proxyContext = new ProxyLoadContext(testAssemblyPath);

        var target = CreateGeneratedTarget(targetContext, testAssemblyPath);
        var proxyAssembly = proxyContext.LoadFromAssemblyPath(testAssemblyPath);
        var proxyRunner = proxyAssembly.GetType(typeof(ProxyRunner).FullName!, throwOnError: true)!;

        var exception = Assert.Throws<TargetInvocationException>(
            () => proxyRunner.GetMethod(nameof(ProxyRunner.AccessField))!.Invoke(null, [target]));
        exception.InnerException.Should().BeOfType<MissingFieldException>();
    }

    private static object CreateGeneratedTarget(AssemblyLoadContext targetContext, string testAssemblyPath)
    {
        var targetAssembly = targetContext.LoadFromAssemblyPath(testAssemblyPath);
        var fieldType = targetAssembly.GetType(typeof(FieldValue).FullName!, throwOnError: true)!;

        // An ordinary type also present in the proxy assembly does not reproduce the mismatch.
        // Contextual reflection makes this generated type belong only to targetContext.
        using (AssemblyLoadContext.EnterContextualReflection(targetAssembly))
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("DuckTypingTargetAssembly"), AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("MainModule").DefineType("DuckTypingTarget", TypeAttributes.Public);
            type.DefineField("_field", fieldType, FieldAttributes.Private);
            return Activator.CreateInstance(type.CreateType()!)!;
        }
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

    public sealed class FieldValue
    {
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
