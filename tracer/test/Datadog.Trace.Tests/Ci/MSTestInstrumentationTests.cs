// <copyright file="MSTestInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;
using Datadog.Trace.ClrProfiler.CallTarget;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class MSTestInstrumentationTests
{
    [Fact]
    public void CapturedExecutionContextDoesNotRetainCompletedResults()
    {
        var execution = new MsTestExecution(CallTargetState.GetDefault(), parent: null);
        var result = ObserveResult(execution);
        GC.Collect();
        result.IsAlive.Should().BeTrue();

        execution.CloseTests();
        GC.Collect();
        result.IsAlive.Should().BeFalse();
        GC.KeepAlive(execution);
    }

    [Theory]
    [InlineData("4.3.3")]
    [InlineData("4.4.0")]
    public void EachMethodHasOneInstrumentationOwner(string packageVersion)
    {
        var version = new Version(packageVersion);
        var definitions = new List<string>();
        var integrationType = typeof(UnitTestRunnerRunSingleTestAsyncIntegrationV4_4);
        foreach (var type in integrationType.Assembly.GetTypes())
        {
            if (type.Namespace != integrationType.Namespace)
            {
                continue;
            }

            foreach (var attribute in type.GetCustomAttributes<InstrumentMethodAttribute>())
            {
                if (version < new Version(attribute.MinimumVersion) || version > new Version(attribute.MaximumVersion.Replace("*", "65535")))
                {
                    continue;
                }

                foreach (var assembly in attribute.AssemblyNames)
                {
                    foreach (var targetType in attribute.TypeNames)
                    {
                        definitions.Add($"{assembly}:{targetType}:{attribute.MethodName}:{attribute.ReturnTypeName}:{string.Join(",", attribute.ParameterTypeNames ?? [])}:{attribute.CallTargetIntegrationKind}");
                    }
                }
            }
        }

        definitions.Should().NotBeEmpty().And.OnlyHaveUniqueItems("two owners can silently replace each other's callbacks");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ObserveResult(MsTestExecution execution)
    {
        var result = new object();
        execution.ObserveResults(new[] { result });
        return new WeakReference(result);
    }
}
