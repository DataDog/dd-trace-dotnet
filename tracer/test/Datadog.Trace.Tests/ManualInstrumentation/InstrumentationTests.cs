// <copyright file="InstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
extern alias DatadogTraceManual;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.ClrProfiler;
using FluentAssertions;
using Xunit;
using ManualInstrumented = DatadogTraceManual::Datadog.Trace.SourceGenerators.InstrumentedAttribute;
using ManualTracer = DatadogTraceManual::Datadog.Trace.Tracer;

namespace Datadog.Trace.Tests.ManualInstrumentation;

public abstract class InstrumentationTests<T>
{
    [Fact]
    public void DatadogTraceManual_AllInstrumentedMethodsAreDecoratedWithInstrumentedAttribute()
    {
        var manualAssembly = typeof(T).Assembly;
        var assemblyName = manualAssembly.GetName().Name;

        // find all types in Datadog.Trace that instrument methods in the target assembly
        var instrumentations = GetAllInstrumentations(assemblyName).GroupBy(x => x.Attribute.TypeName);

        var attributedMembers = manualAssembly
                               .GetTypes()
                               .SelectMany(
                                    x => x.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                          .Where(y => y.GetCustomAttribute<ManualInstrumented>() != null))
                               .ToList();

        foreach (var @group in instrumentations)
        {
            var targetType = manualAssembly.GetType(@group.Key, throwOnError: false);
            targetType.Should().NotBeNull($"target {targetType} required for instrumentation");
            var allMembers = targetType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var (instrumentationType, attribute) in @group)
            {
                var methodName = attribute.MethodName;
                var parameters = attribute.ParameterTypeNames?.Select(x => GetType(x, manualAssembly)).ToList();

                var member = allMembers
                            .Should()
                            .ContainSingle(
                                 x => IsTarget(x, methodName, parameters),
                                 $"type '{targetType.Name}' should have member '{methodName}'")
                            .Subject;

                // allow for decorating the property
                var instrumentedAttribute = member.GetCustomAttribute<ManualInstrumented>();
                if (instrumentedAttribute is null && (methodName.StartsWith("get_") || methodName.StartsWith("set_")))
                {
                    var snippedName = methodName.Substring(4);
                    (var property, instrumentedAttribute) = allMembers
                                           .Where(x => x is PropertyInfo && x.Name == snippedName)
                                           .Select(x => (x, x.GetCustomAttribute<ManualInstrumented>()))
                                           .SingleOrDefault();
                    attributedMembers.Remove(property);
                }

                instrumentedAttribute.Should().NotBeNull($"'{targetType.Name}.{methodName}' should have [Instrumented] attribute");
                attributedMembers.Remove(member);
            }
        }

        attributedMembers.Should().BeEmpty("Every member instrumented with [Instrumented] should be instrumented");
    }

    private static Type GetType(string type, Assembly targetAssembly)
    {
        // this is a royal pain, because we don't know _where_ to load the type from
        // so just try a bunch of stuff
        return Type.GetType(type) // currently executing or mscorlib.dll/System.Private.CoreLib.dll
            ?? targetAssembly.GetType(type) // the target assembly
            ?? typeof(ManualTracer).Assembly.GetType(type) // the manual assembly
            ?? (type.StartsWith("System.") ? Type.GetType($"{type}, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089") : null); // maybe in System?
    }

    private static IEnumerable<(Type Type, InstrumentMethodAttribute Attribute)> GetAllInstrumentations(string assemblyName)
    {
        return typeof(Datadog.Trace.Tracer)
              .Assembly
              .GetTypes()
              .SelectMany(t => t.GetCustomAttributes(typeof(InstrumentMethodAttribute), false)
                                .Select(a => (Type: t, Attribute: (InstrumentMethodAttribute)a)))
              .Where(x => x.Attribute.AssemblyNames is { } names
                              ? names.Contains(assemblyName)
                              : x.Attribute.AssemblyName == assemblyName);
    }

    private static bool IsTarget(MemberInfo memberInfo, string methodName, List<Type> expectedParams)
    {
        if (memberInfo.Name != methodName)
        {
            return false;
        }

        if (memberInfo is MethodInfo method && expectedParams is not null)
        {
            var parameters = method.GetParameters();
            return CompareParameters(expectedParams, parameters);
        }

        if (memberInfo is ConstructorInfo ctor && expectedParams is not null)
        {
            var parameters = ctor.GetParameters();
            return CompareParameters(expectedParams, parameters);
        }

        return true;

        static bool CompareParameters(List<Type> types, ParameterInfo[] parameters)
        {
            if (parameters.Length == types.Count)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    var expected = types[i];
                    var actual = parameters[i].ParameterType;
                    if (expected is null && (actual.IsGenericParameter || actual.GenericTypeArguments.Any(x => x.IsGenericParameter)))
                    {
                        // generic parameters, a bit of a pain to do properly, so this will do
                        continue;
                    }

                    if (actual != expected)
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }
    }
}
