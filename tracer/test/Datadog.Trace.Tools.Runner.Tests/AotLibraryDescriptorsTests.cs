// <copyright file="AotLibraryDescriptorsTests.cs" company="Datadog">
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
/// What the instrumented libraries use by reflection, without which they don't run in NativeAOT (Confluent.Kafka and the
/// librdkafka bindings), is kept in the applications that have them.
/// </summary>
public class AotLibraryDescriptorsTests
{
    [Fact]
    public void TheLibrdkafkaBindingsAreKeptForConfluentKafka()
    {
        var path = Path.GetTempFileName();
        try
        {
            LibraryDescriptors.Write([Module("Samples.Kafka"), Module("Confluent.Kafka")], path).Should().Be(1);

            var assembly = XDocument.Load(path).Root!.Elements("assembly").Should().ContainSingle().Which;
            assembly.Attribute("fullname")!.Value.Should().Be("Confluent.Kafka");
            assembly.Elements("type").Select(t => t.Attribute("fullname")!.Value).Should().Equal("Confluent.Kafka.Impl.Librdkafka");
            assembly.Elements("namespace").Select(n => n.Attribute("fullname")!.Value).Should().Equal("Confluent.Kafka.Impl.NativeMethods");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NothingIsWrittenWithoutThoseLibraries()
    {
        var path = Path.GetTempFileName();
        try
        {
            LibraryDescriptors.Write([Module("Samples.Console")], path).Should().Be(0);
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
