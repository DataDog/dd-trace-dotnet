// <copyright file="DebuggerTestHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Pdb;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Samples.Probes.TestRuns;
using Xunit;

namespace Datadog.Trace.Debugger.IntegrationTests.Helpers;

internal static class DebuggerTestHelper
{
    public static IEnumerable<object[]> AllProbeTestTypes()
    {
        return typeof(IRun)
              .Assembly.GetTypes()
              .Where(t => t.GetInterface(nameof(IRun)) != null ||
                          t.GetInterface(nameof(IAsyncRun)) != null)
              .Select(t => new object[] { t });
    }

    public static Type FirstSupportedProbeTestType(string framework)
    {
        var type = typeof(IRun)
                  .Assembly.GetTypes()
                  .Where(t => t.GetInterface(nameof(IRun)) != null)
                  .FirstOrDefault(t => GetAllProbes(t, framework, unlisted: false, new DeterministicGuidGenerator()).Any());

        if (type == null)
        {
            throw new SkipException("No supported test types found.");
        }

        return type;
    }

    internal static DebuggerSampleProcessHelper StartSample(TestHelper helper, MockTracerAgent agent, string testName)
    {
        var listenPort = TcpPortProvider.GetOpenPort();

        var localHost = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "http://127.0.0.1" : "http://localhost";
        var listenUrl = $"{localHost}:{listenPort}/";

        var process = helper.StartSample(agent, $"--test-name {testName} --listen-url {listenUrl}", string.Empty, aspNetCorePort: 5000);
        var processHelper = new DebuggerSampleProcessHelper(listenUrl, process);

        return processHelper;
    }

    internal static int CalculateExpectedNumberOfSnapshots(ProbeAttributeBase[] probes)
    {
        return probes.Aggregate(0, (accuNumOfSnapshots, next) => accuNumOfSnapshots + next.ExpectedNumberOfSnapshots);
    }

    internal static (ProbeAttributeBase ProbeTestData, SnapshotProbe Probe)[] GetAllProbes(Type type, string targetFramework, bool unlisted, DeterministicGuidGenerator guidGenerator)
    {
        return GetAllMethodProbes(type, targetFramework, unlisted, guidGenerator)
              .Concat(GetAllLineProbes(type, targetFramework, unlisted, guidGenerator))
              .ToArray();
    }

    internal static IEnumerable<(ProbeAttributeBase ProbeTestData, SnapshotProbe Probe)> GetAllLineProbes(Type type, string targetFramework, bool unlisted, DeterministicGuidGenerator guidGenerator)
    {
        var snapshotLineProbes = type.GetCustomAttributes<LineProbeTestDataAttribute>()
                                     .Where(att => att?.Skip == false && att?.Unlisted == unlisted && att?.SkipOnFrameworks.Contains(targetFramework) == false)
                                     .Select(att => (att.As<ProbeAttributeBase>(), CreateSnapshotLineProbe(type, att, guidGenerator)))
                                     .ToArray();

        return snapshotLineProbes;
    }

    internal static IEnumerable<(ProbeAttributeBase ProbeTestData, SnapshotProbe Probe)> GetAllMethodProbes(Type type, string targetFramework, bool unlisted, DeterministicGuidGenerator guidGenerator)
    {
        var snapshotMethodProbes = GetAllTestMethods<MethodProbeTestDataAttribute>(type, targetFramework, unlisted)
           .Select(m =>
            {
                var testAttribute = m.GetCustomAttribute<MethodProbeTestDataAttribute>().As<ProbeAttributeBase>();
                var probe = CreateSnapshotMethodProbe(m, guidGenerator);
                if (testAttribute is ExpressionProbeTestDataAttribute expression)
                {
                    probe = probe.WithWhen(expression.Dsl, expression.Json, (EvaluateAt)expression.EvaluateAt);
                }

                return (testAttribute, probe);
            });

        return snapshotMethodProbes;
    }

    internal static SnapshotProbe WithWhere(this SnapshotProbe snapshot, string typeName, string methodName, MethodProbeTestDataAttribute probeTestData = null)
    {
        var @where = new Where
        {
            TypeName = typeName,
            MethodName = methodName
        };

        if (probeTestData != null)
        {
            var signature = probeTestData.ReturnTypeName;
            if (probeTestData.ParametersTypeName?.Any() == true)
            {
                signature += "," + string.Join(",", probeTestData.ParametersTypeName);
            }

            @where.Signature = signature;
        }

        snapshot.Where = where;
        return snapshot;
    }

    internal static SnapshotProbe WithWhen(this SnapshotProbe snapshot, string dsl, string json, EvaluateAt evaluateAt)
    {
        snapshot.When = new DebuggerExpression(dsl, json);
        snapshot.EvaluateAt = evaluateAt;
        return snapshot;
    }

    internal static SnapshotProbe WithSampling(this SnapshotProbe snapshot, double snapshotsPerSeconds = 1000000)
    {
        snapshot.Sampling = new Configurations.Models.Sampling { SnapshotsPerSecond = snapshotsPerSeconds };
        return snapshot;
    }

    internal static SnapshotProbe CreateSnapshotProbe(DeterministicGuidGenerator guidGenerator)
    {
        return new SnapshotProbe
        {
            Id = guidGenerator.New().ToString(),
            Language = TracerConstants.Language,
            Active = true,
        };
    }

    internal static SnapshotProbe CreateDefaultSnapshotProbe(string typeName, string methodName, DeterministicGuidGenerator guidGenerator)
    {
        return CreateSnapshotProbe(guidGenerator).WithWhere(typeName, methodName).WithSampling();
    }

    private static IEnumerable<MethodBase> GetAllTestMethods<T>(Type type, string targetFramework, bool unlisted)
        where T : ProbeAttributeBase
    {
        const BindingFlags allMask =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Static |
            BindingFlags.Instance;

        return type.GetNestedTypes(allMask)
                   .SelectMany(
                        nestedType =>
                            nestedType.GetMethods(allMask | BindingFlags.DeclaredOnly)
                                      .Concat(nestedType.GetConstructors(allMask | BindingFlags.DeclaredOnly).As<IEnumerable<MethodBase>>()))
                   .Concat(
                        type.GetMethods(allMask | BindingFlags.DeclaredOnly)
                            .Concat(type.GetConstructors(allMask | BindingFlags.DeclaredOnly).As<IEnumerable<MethodBase>>()))
                   .As<IEnumerable<MethodBase>>()
                   .Where(
                        m =>
                        {
                            var att = m.GetCustomAttribute<T>();
                            return att?.Skip == false && att?.Unlisted == unlisted && att?.SkipOnFrameworks.Contains(targetFramework) == false;
                        });
    }

    private static SnapshotProbe CreateSnapshotLineProbe(Type type, LineProbeTestDataAttribute line, DeterministicGuidGenerator guidGenerator)
    {
        return CreateSnapshotProbe(guidGenerator).WithLineProbeWhere(type, line);
    }

    private static SnapshotProbe WithLineProbeWhere(this SnapshotProbe snapshot, Type type, LineProbeTestDataAttribute line)
    {
        using var reader = DatadogPdbReader.CreatePdbReader(type.Assembly);
        var symbolMethod = reader.ReadMethodSymbolInfo(type.GetMethods().First().MetadataToken);
        var filePath = symbolMethod.SequencePoints.First().Document.URL;
        var where = new Where { SourceFile = filePath, Lines = new[] { line.LineNumber.ToString() } };
        snapshot.Where = where;
        return snapshot;
    }

    private static SnapshotProbe CreateSnapshotMethodProbe(MethodBase method, DeterministicGuidGenerator guidGenerator)
    {
        var probeTestData = method.GetCustomAttribute<MethodProbeTestDataAttribute>();
        if (probeTestData == null)
        {
            throw new Xunit.Sdk.SkipException($"{nameof(MethodProbeTestDataAttribute)} has not found for method: {method.DeclaringType?.FullName}.{method.Name}");
        }

        var typeName = probeTestData.UseFullTypeName ? method.DeclaringType.FullName : method.DeclaringType.Name;
        return CreateSnapshotProbe(guidGenerator).WithWhere(typeName, method.Name, probeTestData).WithSampling();
    }
}
