// <copyright file="EmulatedRuntimeModuleFilesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Datadog.Trace.Tools.Runner.Aot.Native;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The compiler's inputs and reference directories also have native libraries (e.g. on Windows with .NET 10), which the
/// instrumentation skips instead of loading them as modules.
/// </summary>
public class EmulatedRuntimeModuleFilesTests
{
    [Fact]
    public void OnlyNetAssembliesAreModules()
    {
        EmulatedRuntime.IsManagedAssembly(typeof(object).Assembly.Location).Should().BeTrue();

        var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
        var nativeRuntime = new[] { "coreclr.dll", "libcoreclr.so", "libcoreclr.dylib" }.Select(name => Path.Combine(runtimeDirectory, name)).First(File.Exists);
        EmulatedRuntime.IsManagedAssembly(nativeRuntime).Should().BeFalse();

        var text = Path.GetTempFileName();
        try
        {
            File.WriteAllText(text, "not a module");
            EmulatedRuntime.IsManagedAssembly(text).Should().BeFalse();
        }
        finally
        {
            File.Delete(text);
        }
    }
}
#endif
