// <copyright file="AotRuntimeMetricsDirectivesTests.cs" company="Datadog">
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
/// The long instantiations of Meter.CreateObservableUpDownCounter the .NET 8 polyfill of the System.Runtime meter makes by
/// reflection are kept in NativeAOT applications older than .NET 9.
/// </summary>
public class AotRuntimeMetricsDirectivesTests
{
    [Fact]
    public void TheInstantiationIsKeptBeforeNet9()
    {
        var path = Path.GetTempFileName();
        try
        {
            RuntimeMetricsDirectives.Write([CoreLib(8), DiagnosticSource(withMethod: true)], path).Should().Be(1);

            XNamespace directives = "http://schemas.microsoft.com/netfx/2013/01/metadata";
            var method = XDocument.Load(path).Descendants(directives + "Method").Single();
            method.Attribute("Name")!.Value.Should().Be("CreateObservableUpDownCounter");
            method.Elements(directives + "GenericArgument").Select(a => a.Attribute("Name")!.Value).Should().Equal("System.Int64, System.Private.CoreLib");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(9, true)] // The runtime publishes the meter
    [InlineData(8, false)] // DiagnosticSource without the method
    public void NothingIsKeptOtherwise(int runtimeMajor, bool withMethod)
    {
        var path = Path.GetTempFileName();
        try
        {
            RuntimeMetricsDirectives.Write([CoreLib(runtimeMajor), DiagnosticSource(withMethod)], path).Should().Be(0);
            File.Exists(path).Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ModuleDef CoreLib(int major) => Module("System.Private.CoreLib", new Version(major, 0, 0, 0));

    private static ModuleDef DiagnosticSource(bool withMethod)
    {
        var module = Module("System.Diagnostics.DiagnosticSource", new Version(8, 0, 0, 0));
        var meter = new TypeDefUser("System.Diagnostics.Metrics", "Meter", module.CorLibTypes.Object.TypeDefOrRef);
        module.Types.Add(meter);
        if (withMethod)
        {
            var method = new MethodDefUser("CreateObservableUpDownCounter", MethodSig.CreateInstanceGeneric(1, module.CorLibTypes.Object));
            method.GenericParameters.Add(new GenericParamUser(0, GenericParamAttributes.NonVariant, "T"));
            meter.Methods.Add(method);
        }

        return module;
    }

    private static ModuleDef Module(string assemblyName, Version version)
    {
        var module = new ModuleDefUser(assemblyName + ".dll");
        new AssemblyDefUser(assemblyName, version).Modules.Add(module);
        return module;
    }
}
#endif
