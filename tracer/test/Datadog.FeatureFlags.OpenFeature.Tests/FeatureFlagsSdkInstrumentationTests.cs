// <copyright file="FeatureFlagsSdkInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

extern alias Tracer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Xunit;
using InstrumentMethodAttribute = Tracer::Datadog.Trace.ClrProfiler.InstrumentMethodAttribute;
using IntegrationMapper = Tracer::Datadog.Trace.ClrProfiler.CallTarget.Handlers.IntegrationMapper;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

// The tracer reaches this package only by intercepting FeatureFlagsSdk stubs. A stub whose name or
// signature drifts from its integration is never intercepted, and the provider then silently gets
// the stub's default (no evaluation, no committed exposure).
public class FeatureFlagsSdkInstrumentationTests
{
    private static readonly Assembly ProviderAssembly = typeof(DatadogProvider).Assembly;

    public static IEnumerable<object[]> Integrations()
        => TracerTypes()
                                              .SelectMany(t => t.GetCustomAttributes<InstrumentMethodAttribute>(false).Select(a => (Type: t, Attribute: a)))
                                              .Where(x => x.Attribute.AssemblyNames is { } names ? names.Contains(ProviderAssembly.GetName().Name) : x.Attribute.AssemblyName == ProviderAssembly.GetName().Name)
                                              .Select(x => new object[] { x.Type.Name })
                                              .ToList();

    [Fact]
    public void TheDeferredExposureStubsAreInstrumented()
    {
        Integrations().Select(row => (string)row[0]).Should().Contain(["OpenFeatureSdkEvaluateDeferredIntegration", "OpenFeatureSdkCommitExposureIntegration", "OpenFeatureSdkCanDeferExposuresIntegration"]);
    }

    [Theory]
    [MemberData(nameof(Integrations))]
    public void EachIntegrationMatchesItsStubAndBinds(string integrationName)
    {
        var integration = TracerTypes().Single(t => t.Name == integrationName && t.GetCustomAttributes<InstrumentMethodAttribute>(false).Any());
        var attribute = integration.GetCustomAttribute<InstrumentMethodAttribute>()!;
        var targetType = ProviderAssembly.GetType(attribute.TypeName, throwOnError: false);
        targetType.Should().NotBeNull($"{integrationName} targets {attribute.TypeName}");

        var stub = targetType!.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                              .Where(m => m.Name == attribute.MethodName && m.GetParameters().Select(p => ClrName(p.ParameterType)).SequenceEqual(attribute.ParameterTypeNames ?? []))
                              .Should().ContainSingle($"{integrationName} targets {attribute.MethodName}({string.Join(", ", attribute.ParameterTypeNames ?? [])})").Subject;
        ClrName(stub.ReturnType).Should().Be(attribute.ReturnTypeName);
        stub.MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining).Should().BeTrue("an inlined stub cannot be intercepted");

        // The same binding CallTarget performs at runtime, with the stub's real argument and return types.
        var argumentTypes = stub.GetParameters().Select(p => p.ParameterType).ToArray();
        if (integration.GetMethod("OnMethodBegin", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) is not null)
        {
            var begin = argumentTypes.Length <= 8
                            ? IntegrationMapper.CreateBeginMethodDelegate(integration, typeof(object), argumentTypes.Select(t => t.MakeByRefType()).ToArray())
                            : IntegrationMapper.CreateSlowBeginMethodDelegate(integration, typeof(object));
            begin.Should().NotBeNull();
        }

        if (integration.GetMethod("OnMethodEnd", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) is not null)
        {
            var end = stub.ReturnType == typeof(void)
                          ? IntegrationMapper.CreateEndMethodDelegate(integration, typeof(object))
                          : IntegrationMapper.CreateEndMethodDelegate(integration, typeof(object), stub.ReturnType);
            end.Should().NotBeNull();
        }
    }

    // Some tracer types reference optional integration assemblies this test project does not load.
    private static IEnumerable<Type> TracerTypes()
    {
        try
        {
            return typeof(Tracer::Datadog.Trace.Tracer).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static string ClrName(Type type)
        => type.IsGenericType
               ? $"{type.GetGenericTypeDefinition().FullName}[{string.Join(",", type.GetGenericArguments().Select(ClrName))}]"
               : type.FullName!;
}
