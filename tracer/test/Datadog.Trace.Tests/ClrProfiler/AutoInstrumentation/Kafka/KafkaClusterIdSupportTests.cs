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
    [InlineData(0x010601ff, false)]
    [InlineData(0x020200ff, false)]
    [InlineData(0x02030000, false)]
    [InlineData(0x020300fe, false)]
    [InlineData(0x020300ff, true)]
    [InlineData(0x020800ff, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void ChecksLoadedNativeVersion(int nativeVersion, bool supported)
    {
        var assembly = CreateAssembly($"public static int Version {{ get {{ Calls++; return {nativeVersion}; }} }}");

        KafkaClusterIdSupport.GetDescribeClusterOptionsType(assembly).Should().Be(supported ? assembly.GetType("Confluent.Kafka.Admin.DescribeClusterOptions") : null);
        KafkaHelper.GetClusterId(Guid.NewGuid().ToString(), CreateClient(assembly)).Should().Be(supported ? "test-cluster" : null);
        GetCalls(assembly, "Confluent.Kafka.DependentAdminClientBuilder").Should().Be(supported ? 1 : 0);
        GetCalls(assembly, "Confluent.Kafka.Library").Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("public static string Version => \"2.8.0\";")]
    [InlineData("public int Version => 0x020800ff;")]
    [InlineData("public static int Version => throw new System.InvalidOperationException();")]
    public void UnreadableVersionSkipsDiscovery(string versionProperty)
    {
        var assembly = CreateAssembly(versionProperty);

        KafkaHelper.GetClusterId(Guid.NewGuid().ToString(), CreateClient(assembly)).Should().BeNull();
        GetCalls(assembly, "Confluent.Kafka.DependentAdminClientBuilder").Should().Be(0);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void MissingManagedTypeSkipsDiscovery(bool includeLibrary, bool includeOptions)
    {
        var assembly = CreateAssembly("public static int Version { get { Calls++; return 0x020800ff; } }", includeLibrary, includeOptions);

        KafkaHelper.GetClusterId(Guid.NewGuid().ToString(), CreateClient(assembly)).Should().BeNull();
        GetCalls(assembly, "Confluent.Kafka.DependentAdminClientBuilder").Should().Be(0);
        if (includeLibrary)
        {
            GetCalls(assembly, "Confluent.Kafka.Library").Should().Be(0);
        }
    }

    [Theory]
    [InlineData("return 0x020800ff;")]
    [InlineData("return 0x010601ff;")]
    [InlineData("throw new System.InvalidOperationException();")]
    public void CachesConcurrentVersionQueries(string getterBody)
    {
        var assembly = CreateAssembly($"public static int Version {{ get {{ System.Threading.Interlocked.Increment(ref Calls); {getterBody} }} }}");

        Parallel.For(0, 32, _ => KafkaClusterIdSupport.GetDescribeClusterOptionsType(assembly));

        GetCalls(assembly, "Confluent.Kafka.Library").Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CapabilityIsIndependentForAssembliesUsingTheSameBootstrapServers(bool unsupportedFirst)
    {
        var unsupported = CreateAssembly("public static int Version => 0x010601ff;");
        var supported = CreateAssembly("public static int Version => 0x020800ff;");
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

    private static Assembly CreateAssembly(string versionProperty, bool includeLibrary = true, bool includeOptions = true)
    {
        var source = $$"""
                       using System;
                       using System.Threading.Tasks;

                       namespace Confluent.Kafka
                       {
                           {{(includeLibrary ? $"public class Library {{ public static int Calls; {versionProperty} }}" : string.Empty)}}
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
                           {{(includeOptions ? "public class DescribeClusterOptions { public TimeSpan? RequestTimeout { get; set; } }" : string.Empty)}}
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
