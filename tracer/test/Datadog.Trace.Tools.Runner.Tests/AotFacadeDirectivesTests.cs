// <copyright file="AotFacadeDirectivesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Datadog.Trace.Tools.Runner.Aot;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The facades libraries resolve their stored type names through (Hangfire: mscorlib) are kept in NativeAOT applications
/// that have those libraries.
/// </summary>
public class AotFacadeDirectivesTests
{
    [Fact]
    public void MscorlibIsKeptForHangfire()
    {
        var path = Path.GetTempFileName();
        try
        {
            FacadeDirectives.Write([Module("Samples.Hangfire"), Module("Hangfire.Core")], path).Should().Be(1);

            XNamespace directives = "http://schemas.microsoft.com/netfx/2013/01/metadata";
            XDocument.Load(path).Descendants(directives + "Assembly").Select(a => a.Attribute("Name")!.Value).Should().Equal("mscorlib");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NothingIsKeptWithoutThoseLibraries()
    {
        var path = Path.GetTempFileName();
        try
        {
            FacadeDirectives.Write([Module("Samples.Console")], path).Should().Be(0);
            File.Exists(path).Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ModuleDef Module(string assemblyName)
    {
        var module = new ModuleDefUser(assemblyName + ".dll");
        new AssemblyDefUser(assemblyName, new Version(1, 0, 0, 0)).Modules.Add(module);
        return module;
    }
}
#endif
