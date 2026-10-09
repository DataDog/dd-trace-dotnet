// <copyright file="StackFrameMethodTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class StackFrameMethodTests
{
    [Fact]
    public void ReflectionFrameUsesItsMethod()
    {
        var frame = GetFrame();

        StackFrameMethod.TryGet(frame, out var method).Should().BeTrue();
        method.Method.Should().BeSameAs(frame.GetMethod());
        method.AssemblyName.Should().Be(typeof(StackFrameMethodTests).Assembly.GetName().Name);
        method.Namespace.Should().Be(typeof(StackFrameMethodTests).Namespace);
        method.TypeName.Should().Be(nameof(StackFrameMethodTests));
        method.TypeFullName.Should().Be(typeof(StackFrameMethodTests).FullName);
        method.Name.Should().Be(nameof(GetFrame));
    }

    // .NET 9+ NativeAOT: the names of DiagnosticMethodInfo, like Type.Namespace, Type.Name and Type.FullName.
    [Theory]
    [InlineData("My.Ns.Outer+Nested`1", "Inner", "App, Version=1.0.0.0", "App", "My.Ns", "Nested`1")]
    [InlineData("My.Ns.Type", "Run", "App", "App", "My.Ns", "Type")]
    [InlineData("Program+<>c", "<<Main>$>b__0_1", null, null, null, "<>c")]
    [InlineData("Program", "<Main>$", "App", "App", null, "Program")]
    public void DiagnosticMethodInfoNames(string declaringTypeName, string name, string? assemblyName, string? expectedAssembly, string? expectedNamespace, string expectedType)
    {
        StackFrameMethod.TryCreate(declaringTypeName, name, assemblyName, out var method).Should().BeTrue();
        method.AssemblyName.Should().Be(expectedAssembly);
        method.Namespace.Should().Be(expectedNamespace);
        method.TypeName.Should().Be(expectedType);
        method.TypeFullName.Should().Be(declaringTypeName);
        method.Name.Should().Be(name);
        method.AssemblyNameForFilters.Should().Be(expectedAssembly ?? declaringTypeName);
    }

    // .NET 8 NativeAOT: the text of the frame, which names nested types with dots.
    [Theory]
    [InlineData("My.Ns.Outer.Nested`1.Inner(T) + 0xdb at offset 219 in file:line:column <filename unknown>:0:0", "My.Ns.Outer", "Nested`1", "Inner")]
    [InlineData("Program.<>c.<<Main>$>b__0_1() + 0x5 at offset 5 in file:line:column <filename unknown>:0:0", "Program", "<>c", "<<Main>$>b__0_1")]
    [InlineData("Microsoft.AspNetCore.Routing.EndpointMiddleware.Invoke(HttpContext httpContext) + 0x39c", "Microsoft.AspNetCore.Routing", "EndpointMiddleware", "Invoke")]
    [InlineData("Program.Main(String[]) + 0x6", null, "Program", "Main")]
    [InlineData("System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start[TStateMachine](TStateMachine&) + 0x5", "System.Runtime.CompilerServices", "AsyncMethodBuilderCore", "Start")]
    [InlineData("System.Collections.Generic.List`1[System.Int32].Add(Int32) + 0x1", "System.Collections.Generic", "List`1", "Add")]
    public void FrameText(string text, string? expectedNamespace, string expectedType, string expectedMethod)
    {
        StackFrameMethod.TryParse(text, out var method).Should().BeTrue();
        method.AssemblyName.Should().BeNull();
        method.Namespace.Should().Be(expectedNamespace);
        method.TypeName.Should().Be(expectedType);
        method.Name.Should().Be(expectedMethod);
        method.AssemblyNameForFilters.Should().Be(method.TypeFullName);
    }

    [Theory]
    [InlineData("App!<BaseAddress>+0x14a214 at offset 180 in file:line:column <filename unknown>:0:0")]
    [InlineData("Main(String[]) + 0x6")]
    [InlineData("")]
    [InlineData(null)]
    public void FrameTextWithoutMethod(string? text)
        => StackFrameMethod.TryParse(text, out _).Should().BeFalse();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static StackFrame GetFrame() => new StackTrace().GetFrame(0)!;
}
