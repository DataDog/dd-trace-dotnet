// <copyright file="KafkaDeliveryReportDirectivesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Datadog.Trace.Tools.Runner.Aot;
using Datadog.Trace.Tools.Runner.Aot.CallTarget;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The instantiations over value types of Kafka's <c>DeliveryReport&lt;TKey, TValue&gt;</c>, which the default delivery handler
/// of a producer creates with <c>MakeGenericType</c> (H-87).
/// </summary>
public class KafkaDeliveryReportDirectivesTests
{
    [Fact]
    public void TheDeliveryReportsOfProducersOverValueTypesAreKept()
    {
        var module = new ModuleDefUser("Confluent.Kafka.dll") { Kind = ModuleKind.Dll };
        new AssemblyDefUser("Confluent.Kafka", new Version(2, 0, 0, 0)).Modules.Add(module);
        var producer = new TypeDefUser("Confluent.Kafka", "Producer`2", module.CorLibTypes.Object.TypeDefOrRef);
        producer.GenericParameters.Add(new GenericParamUser(0, GenericParamAttributes.NonVariant, "TKey"));
        producer.GenericParameters.Add(new GenericParamUser(1, GenericParamAttributes.NonVariant, "TValue"));
        module.Types.Add(producer);
        var constructor = new MethodDefUser(".ctor", MethodSig.CreateInstance(module.CorLibTypes.Void), MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
        producer.Methods.Add(constructor);

        var corlib = ModuleDefMD.Load(typeof(object).Assembly.Location);
        var resolver = new LoadedModulesTypeResolver(new ModuleDef[] { module, corlib });
        GenericInstantiationDiscovery.Instantiation Instantiation(params TypeSig[] arguments) => new(arguments, Array.Empty<TypeSig>());
        var found = new Dictionary<MethodDef, List<GenericInstantiationDiscovery.Instantiation>>
        {
            // Producer<int, string> gets its delivery report; Producer<string, string>, over reference types, has its canonical code.
            [constructor] = [Instantiation(corlib.CorLibTypes.Int32, corlib.CorLibTypes.String), Instantiation(corlib.CorLibTypes.String, corlib.CorLibTypes.String)],
        };

        var path = Path.Combine(Path.GetTempPath(), $"Datadog.Trace.Kafka.{Guid.NewGuid():N}.rd.xml");
        try
        {
            KafkaDeliveryReportDirectives.Write(found, resolver.Resolve, path).Should().Be(1);
            XDocument.Load(path).Descendants().Where(e => e.Name.LocalName == "Type").Select(e => e.Attribute("Name")!.Value).Should().BeEquivalentTo(
                "Confluent.Kafka.DeliveryReport`2[[System.Int32, System.Private.CoreLib],[System.String, System.Private.CoreLib]]",
                "System.Action`1[[Confluent.Kafka.DeliveryReport`2[[System.Int32, System.Private.CoreLib],[System.String, System.Private.CoreLib]], Confluent.Kafka]]");

            // Without instantiations over value types, there's no file.
            found[constructor] = [Instantiation(corlib.CorLibTypes.String, corlib.CorLibTypes.String)];
            KafkaDeliveryReportDirectives.Write(found, resolver.Resolve, path).Should().Be(0);
            File.Exists(path).Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
            corlib.Dispose();
        }
    }
}
#endif
