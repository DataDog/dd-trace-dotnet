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
using Samples.Probes;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Debugger;

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
                  .FirstOrDefault(t => DebuggerTestHelper.GetAllProbes(t, framework, unlisted: false, new DeterministicGuidGenerator()).Any());

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
        const BindingFlags allMask =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Static |
            BindingFlags.Instance;

        var snapshotMethodProbes = type.GetNestedTypes(allMask)
                                       .SelectMany(nestedType =>
                                                       nestedType.GetMethods(allMask | BindingFlags.DeclaredOnly)
                                                                 .Concat(nestedType.GetConstructors(allMask | BindingFlags.DeclaredOnly).As<IEnumerable<MethodBase>>()))
                                       .Concat(type.GetMethods(allMask | BindingFlags.DeclaredOnly)
                                                   .Concat(type.GetConstructors(allMask | BindingFlags.DeclaredOnly).As<IEnumerable<MethodBase>>()))
                                       .As<IEnumerable<MethodBase>>()
                                       .Where(
                                            m =>
                                            {
                                                var att = m.GetCustomAttribute<MethodProbeTestDataAttribute>();
                                                return att?.Skip == false && att?.Unlisted == unlisted && att?.SkipOnFrameworks.Contains(targetFramework) == false;
                                            })
                                       .Select(m => (m.GetCustomAttribute<MethodProbeTestDataAttribute>().As<ProbeAttributeBase>(), CreateSnapshotMethodProbe(m, guidGenerator)))
                                       .ToArray();

        var snapshotLineProbes = type.GetCustomAttributes<LineProbeTestDataAttribute>()
                                     .Where(att => att?.Skip == false && att?.Unlisted == unlisted && att?.SkipOnFrameworks.Contains(targetFramework) == false)
                                     .Select(att => (att.As<ProbeAttributeBase>(), CreateSnapshotLineProbe(type, att, guidGenerator)))
                                     .ToArray();

        return snapshotLineProbes.Concat(snapshotMethodProbes).ToArray();
    }

    private static SnapshotProbe CreateSnapshotLineProbe(Type type, LineProbeTestDataAttribute line, DeterministicGuidGenerator guidGenerator)
    {
        var where = CreateLineProbeWhere(type, line);
        return CreateSnapshotProbe(where, guidGenerator);
    }

    private static Where CreateLineProbeWhere(Type type, LineProbeTestDataAttribute line)
    {
        using var reader = DatadogPdbReader.CreatePdbReader(type.Assembly);
        var symbolMethod = reader.ReadMethodSymbolInfo(type.GetMethods().First().MetadataToken);
        var filePath = symbolMethod.SequencePoints.First().Document.URL;
        return new Where() { SourceFile = filePath, Lines = new[] { line.LineNumber.ToString() } };
    }

    private static SnapshotProbe CreateSnapshotMethodProbe(MethodBase method, DeterministicGuidGenerator guidGenerator)
    {
        var probeTestData = method.GetCustomAttribute<MethodProbeTestDataAttribute>();
        var where = CreateMethodProbeWhere(method, probeTestData);
        return CreateSnapshotProbe(where, guidGenerator);
    }

    private static SnapshotProbe CreateSnapshotProbe(Where where, DeterministicGuidGenerator guidGenerator)
    {
        return new SnapshotProbe
        {
            Id = guidGenerator.New().ToString(),
            Language = TracerConstants.Language,
            Active = true,
            Where = where,
            Sampling = new Trace.Debugger.Configurations.Models.Sampling { SnapshotsPerSecond = 1000000 }
        };
    }

    private static Where CreateMethodProbeWhere(MethodBase method, MethodProbeTestDataAttribute probeTestData)
    {
        var @where = new Where();
        @where.TypeName = probeTestData.UseFullTypeName ? method.DeclaringType.FullName : method.DeclaringType.Name;
        @where.MethodName = method.Name;
        var signature = probeTestData.ReturnTypeName;
        if (probeTestData.ParametersTypeName?.Any() == true)
        {
            signature += "," + string.Join(",", probeTestData.ParametersTypeName);
        }

        @where.Signature = signature;
        return @where;
    }
}
