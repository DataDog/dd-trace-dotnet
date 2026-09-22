// <copyright file="KafkaClusterIdSupportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Kafka;

public class KafkaClusterIdSupportTests
{
    [Theory]
    [InlineData(0x020300fe, false)]
    [InlineData(0x020300ff, true)]
    public void ChecksLoadedNativeVersion(int nativeVersion, bool supported)
    {
        var assembly = CreateAssembly(nativeVersion);

        KafkaClusterIdSupport.GetDescribeClusterOptionsType(assembly).Should().Be(supported ? assembly.GetType("Confluent.Kafka.Admin.DescribeClusterOptions") : null);
        KafkaHelper.GetClusterId(Guid.NewGuid().ToString(), CreateClient(assembly)).Should().Be(supported ? "test-cluster" : null);
        GetCalls(assembly, "Confluent.Kafka.DependentAdminClientBuilder").Should().Be(supported ? 1 : 0);
        GetCalls(assembly, "Confluent.Kafka.Library").Should().Be(1);
    }

    [Theory]
    [InlineData(0x010601ff)]
    [InlineData(0x020800ff)]
    public void CachesConcurrentVersionQueries(int nativeVersion)
    {
        var assembly = CreateAssembly(nativeVersion);

        Parallel.For(0, 32, _ => KafkaClusterIdSupport.GetDescribeClusterOptionsType(assembly));

        GetCalls(assembly, "Confluent.Kafka.Library").Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CapabilityIsIndependentForAssembliesUsingTheSameBootstrapServers(bool unsupportedFirst)
    {
        var unsupported = CreateAssembly(0x010601ff);
        var supported = CreateAssembly(0x020800ff);
        var bootstrapServers = Guid.NewGuid().ToString();

        if (unsupportedFirst)
        {
            KafkaHelper.GetClusterId(bootstrapServers, CreateClient(unsupported)).Should().BeNull();
        }

        KafkaHelper.GetClusterId(bootstrapServers, CreateClient(supported)).Should().Be("test-cluster");
        KafkaHelper.GetClusterId(bootstrapServers, CreateClient(unsupported)).Should().BeNull();
        KafkaHelper.GetClusterId(bootstrapServers, CreateClient(supported)).Should().Be("test-cluster");
        GetCalls(unsupported, "Confluent.Kafka.DependentAdminClientBuilder").Should().Be(0);
        GetCalls(supported, "Confluent.Kafka.DependentAdminClientBuilder").Should().Be(1);
    }

    private static object CreateClient(Assembly assembly) => Activator.CreateInstance(assembly.GetType("Confluent.Kafka.Client"));

    private static int GetCalls(Assembly assembly, string typeName) => (int)assembly.GetType(typeName).GetField("Calls").GetValue(null);

    private static Assembly CreateAssembly(int nativeVersion)
    {
        // Reflection and the assembly-scoped cache require independent assemblies with Kafka's type names.
        var source = $$"""
                       using System;
                       using System.Threading;
                       using System.Threading.Tasks;

                       namespace Confluent.Kafka
                       {
                           public class Library
                           {
                               public static int Calls;
                               public static int Version
                               {
                                   get
                                   {
                                       Interlocked.Increment(ref Calls);
                                       return {{nativeVersion}};
                                   }
                               }
                           }
                           public class Client { public object Handle => new object(); }
                           public class DependentAdminClientBuilder
                           {
                               public static int Calls;
                               public DependentAdminClientBuilder(object handle) { Calls++; }
                               public AdminClient Build() => new AdminClient();
                           }
                           public class AdminClient : IDisposable
                           {
                               public Task<DescribeClusterResult> DescribeClusterAsync(object options) => Task.FromResult(new DescribeClusterResult());
                               public void Dispose() { }
                           }
                           public class DescribeClusterResult { public string ClusterId => "test-cluster"; }
                       }
                       namespace Confluent.Kafka.Admin
                       {
                           public class DescribeClusterOptions { public TimeSpan? RequestTimeout { get; set; } }
                       }
                       """;
        var compilation = CSharpCompilation.Create(
            $"KafkaStub_{Guid.NewGuid():N}",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        result.Success.Should().BeTrue(string.Join(Environment.NewLine, result.Diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error)));
        return Assembly.Load(stream.ToArray());
    }
}
