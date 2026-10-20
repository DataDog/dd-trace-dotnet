// <copyright file="AotDuckTypeDeclarationsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.IO;
using System.Linq;
using Datadog.Trace.Tools.Runner.Aot;
using Datadog.Trace.Tools.Runner.Aot.CallTarget;
using Datadog.Trace.Tools.Runner.DuckTypeAot;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using dnlib.DotNet;
using FluentAssertions;
using Xunit;

#pragma warning disable SA1201 // Nested test fixtures follow the test methods.

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// The duck typing mappings Datadog.Trace declares with attributes ([DuckType], [DuckCopy], [DuckReverseDelegation],
/// [assembly: DuckTypeMapping]), which a NativeAOT build generates the proxies of.
/// </summary>
public class AotDuckTypeDeclarationsTests
{
    [Fact]
    public void DeclarationsAreWellFormed()
    {
        var result = DuckTypeAotAttributeDiscovery.Discover([typeof(Tracer).Assembly.Location]);
        result.Errors.Should().BeEmpty();
        result.Warnings.Where(w => w.StartsWith("Skipping", System.StringComparison.Ordinal)).Should().BeEmpty();

        // A renamed or removed proxy of Datadog.Trace would leave a declaration a NativeAOT build reports as incompatible.
        var datadogTrace = ModuleDefMD.Load(typeof(Tracer).Assembly.Location);
        var datadogTypes = result.Mappings.Select(m => m.Mode == DuckTypeAotMappingMode.Forward ? (m.ProxyAssemblyName, m.ProxyTypeName) : (m.TargetAssemblyName, m.TargetTypeName))
                                 .Where(t => t.Item1 == "Datadog.Trace")
                                 .Select(t => t.Item2.Split('[')[0])
                                 .Distinct()
                                 .ToList();
        datadogTypes.Should().NotBeEmpty();
        datadogTypes.Where(name => datadogTrace.Find(name, isReflectionName: true) is null).Should().BeEmpty();

        result.Mappings.Should().Contain(m => m.IncludesDerivedTypes && m.TargetTypeName == "Amazon.Runtime.AmazonWebServiceResponse");
        result.Mappings.Should().Contain(m => m.Mode == DuckTypeAotMappingMode.Reverse && m.ProxyTypeName == "Microsoft.Extensions.Logging.ILogger");
        result.Mappings.Should().Contain(m => m.ProxyAssemblyName == "Datadog.Trace.Manual" && m.ProxyTypeName == "Datadog.Trace.IScope");
        result.Mappings.Should().Contain(m => DuckTypeAotRegistryAssemblyEmitter.IsGenericProxyMapping(m) && m.TargetTypeName == "Grpc.Net.Client.Internal.GrpcCall`2");
    }

    [Fact]
    public void RecordedMappingsTheDeclarationsMissAreListed()
    {
        var module = ModuleDefMD.Load(typeof(AotDuckTypeDeclarationsTests).Assembly.Location);
        var assemblyName = module.Assembly.Name.String;
        TypeDef? FindType(string assembly, string name) => assembly == assemblyName ? module.Find(name, isReflectionName: true) : null;
        TypeSig Sig(Type type) => FindType(assemblyName, type.FullName!)!.ToTypeSig();

        var declared = new[]
        {
            Forward(typeof(IDeclaredProxy), typeof(DeclaredTarget)),
            Forward(typeof(IDeclaredProxy), typeof(DeclaredBase)).WithDerivedTypes(),
            Forward(typeof(IRequestedProxy), typeof(GenericDeclaredBase<>)).WithDerivedTypes(),
            Forward(typeof(IRequestedProxy), typeof(GenericDeclaredTarget<>)),
        };
        var requests = new[] { (Sig(typeof(IRequestedProxy)), Sig(typeof(DeclaredTarget))) };
        var undeclaredTarget = Forward(typeof(IDeclaredProxy), typeof(UndeclaredTarget));
        var undeclaredReverse = new DuckTypeAotMapping(typeof(DeclaredBase).FullName!, "App", typeof(DeclaredTarget).FullName!, assemblyName, DuckTypeAotMappingMode.Reverse, DuckTypeAotMappingSource.MapFile);
        var recorded = new[]
        {
            Forward(typeof(IDeclaredProxy), typeof(DeclaredTarget)),
            Forward(typeof(IDeclaredProxy), typeof(DeclaredGrandChild)),
            Forward(typeof(IRequestedProxy), typeof(DeclaredTarget)),
            Forward(typeof(IRequestedProxy), typeof(GenericDeclaredTarget<UndeclaredTarget>)),
            Forward(typeof(IRequestedProxy), typeof(GenericDeclaredChild<UndeclaredTarget>)),
            undeclaredTarget,
            undeclaredReverse,
            new DuckTypeAotMapping(typeof(IDeclaredProxy).FullName!, "App", typeof(UndeclaredTarget).FullName!, assemblyName, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile),
        };

        CallTargetDuckTypeRegistry.FindUndeclared(requests, [], declared, recorded, FindType, type => type.ResolveTypeDef())
                                  .Should().BeEquivalentTo(undeclaredTarget.Key, undeclaredReverse.Key);

        DuckTypeAotMapping Forward(Type proxy, Type target)
            => new(proxy.FullName!, assemblyName, target.FullName!, assemblyName, DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile);
    }

    [Fact]
    public void CheckOfRecordedMapsListsTheUndeclaredMappings()
    {
        // The map of an application (as the integration tests record them): the mappings of CallTarget's constraints, the ones
        // dynamic duck typing doesn't create, and the excluded proxies are left out.
        var directory = Path.Combine(Path.GetTempPath(), "dd-trace-aot-check-declarations", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var datadogTrace = typeof(Tracer).Assembly.Location;
            var mapPath = Path.Combine(directory, "Samples.App-0123456789abcdef0123456789abcdef.json");
            File.WriteAllText(mapPath, new JObject
            {
                ["applicationDirectory"] = Path.GetDirectoryName(datadogTrace),
                ["mappings"] = new JArray(
                    Entry("Datadog.Trace.Undeclared.IProxy", "Library.Target"),
                    // A proxy declared for the types deriving from a base type, with a target whose assembly isn't found.
                    Entry("Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.ObjectResult", "Library.Result"),
                    Entry("Datadog.Trace.Undeclared.IConstraintProxy", "Library.Target", callTarget: true),
                    Entry("Datadog.Trace.Undeclared.IFailingProxy", "Library.Target", canCreate: false),
                    Entry("Datadog.Trace.Excluded.IProxy", "Library.Target"),
                    Entry("Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcNetClient.IGrpcCall", "Grpc.Net.Client.Internal.GrpcCall`2[[App.Request, App],[App.Response, App]]", targetAssembly: "Grpc.Net.Client")),
            }.ToString());
            File.WriteAllText(Path.Combine(directory, "not-a-map.json"), "[]");
            var reportPath = Path.Combine(directory, "report.txt");

            AotDeclarationsCheck.Run(datadogTrace, [directory], ["Datadog.Trace.Excluded."], reportPath).Should().Be(1);

            var undeclared = JObject.Parse(File.ReadAllText(reportPath))["undeclared"]!.Select(entry => ((string)entry["mapping"]!, ((JArray)entry["applications"]!).Select(application => (string)application!).Single())).ToList();
            undeclared.Should().Equal(
                new[]
                {
                    (new DuckTypeAotMapping("Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.ObjectResult", "Datadog.Trace", "Library.Result", "Library", DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile).Key, "Samples.App"),
                    (new DuckTypeAotMapping("Datadog.Trace.Undeclared.IProxy", "Datadog.Trace", "Library.Target", "Library", DuckTypeAotMappingMode.Forward, DuckTypeAotMappingSource.MapFile).Key, "Samples.App"),
                });

            File.Delete(mapPath);
            AotDeclarationsCheck.Run(datadogTrace, [directory], [], reportPath: null).Should().Be(0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        static JObject Entry(string proxyType, string targetType, bool callTarget = false, bool canCreate = true, string targetAssembly = "Library")
        {
            var entry = new JObject { ["mode"] = "forward", ["proxyType"] = proxyType, ["proxyAssembly"] = "Datadog.Trace", ["targetType"] = targetType, ["targetAssembly"] = targetAssembly };
            if (callTarget)
            {
                entry["callTarget"] = true;
            }

            if (!canCreate)
            {
                entry["canCreate"] = false;
            }

            return entry;
        }
    }

    public interface IDeclaredProxy
    {
    }

    public interface IRequestedProxy
    {
    }

    public class DeclaredTarget
    {
    }

    public class DeclaredBase
    {
    }

    public class DeclaredChild : DeclaredBase
    {
    }

    public class DeclaredGrandChild : DeclaredChild
    {
    }

    public class UndeclaredTarget
    {
    }

    public class GenericDeclaredTarget<T>
    {
    }

    public class GenericDeclaredBase<T>
    {
    }

    public class GenericDeclaredChild<T> : GenericDeclaredBase<T>
    {
    }
}
#endif
