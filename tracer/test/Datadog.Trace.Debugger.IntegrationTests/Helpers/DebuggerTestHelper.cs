// <copyright file="DebuggerTestHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public static IEnumerable<object[]> AllTestDescriptions()
    {
        var assembly = typeof(IRun).Assembly;
        var isOptimized = IsOptimized(assembly);
        var testTypes = assembly.GetTypes()
                                .Where(
                                     t => t.GetInterface(nameof(IRun)) != null ||
                                          t.GetInterface(nameof(IAsyncRun)) != null);

        foreach (var testType in testTypes)
        {
            yield return new object[] { new ProbeTestDescription() { IsOptimized = isOptimized, TestType = testType } };
        }
    }

    public static ProbeTestDescription SpecificTestDescription<T>()
    {
        var type = typeof(T);
        var assembly = type.Assembly;
        var isOptimized = IsOptimized(assembly);

        return new ProbeTestDescription() { IsOptimized = isOptimized, TestType = type };
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

    internal static (ProbeAttributeBase ProbeTestData, LogProbe Probe)[] GetAllProbes(Type type, string targetFramework, bool unlisted, DeterministicGuidGenerator guidGenerator)
    {
        return GetAllMethodProbes(type, targetFramework, unlisted, guidGenerator)
              .Concat(GetAllLineProbes(type, targetFramework, unlisted, guidGenerator))
              .ToArray();
    }

    internal static IEnumerable<(ProbeAttributeBase ProbeTestData, LogProbe Probe)> GetAllLineProbes(Type type, string targetFramework, bool unlisted, DeterministicGuidGenerator guidGenerator)
    {
        var snapshotLineProbes = type.GetCustomAttributes<LineProbeTestDataAttribute>()
                                     .Where(att => att.Skip == false && att.Unlisted == unlisted && att.SkipOnFrameworks.Contains(targetFramework) == false)
                                     .Select(att => (att.As<ProbeAttributeBase>(), CreateLogLineProbe(type, att, guidGenerator)))
                                     .ToArray();

        return snapshotLineProbes;
    }

    internal static IEnumerable<(ProbeAttributeBase ProbeTestData, LogProbe Probe)> GetAllMethodProbes(Type type, string targetFramework, bool unlisted, DeterministicGuidGenerator guidGenerator)
    {
        var snapshotMethodProbes = GetAllTestMethods<MethodProbeTestDataAttribute>(type, targetFramework, unlisted)
           .Select(m =>
            {
                var testAttribute = m.GetCustomAttribute<MethodProbeTestDataAttribute>().As<ProbeAttributeBase>();
                var probe = CreateLogMethodProbe(m, guidGenerator);
                return (testAttribute, probe);
            });

        return snapshotMethodProbes;
    }

    internal static LogProbe CreateBasicLogProbe(DeterministicGuidGenerator guidGenerator)
    {
        return new LogProbe
        {
            Id = guidGenerator.New().ToString(),
            CaptureSnapshot = true,
            Language = TracerConstants.Language,
            Sampling = new Configurations.Models.Sampling { SnapshotsPerSecond = 1000000 }
        };
    }

    internal static LogProbe CreateDefaultLogProbe(string typeName, string methodName, DeterministicGuidGenerator guidGenerator, MethodProbeTestDataAttribute probeTestData = null)
    {
        return CreateBasicLogProbe(guidGenerator).WithWhere(typeName, methodName, probeTestData).WithSampling().WithDefaultTemplate().WithCapture(probeTestData?.CaptureSnapshot);
    }

    internal static LogProbe WithWhere(this LogProbe snapshot, string typeName, string methodName, MethodProbeTestDataAttribute probeTestData = null)
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

    internal static LogProbe WithCapture(this LogProbe snapshot, bool? captureSnapshot)
    {
        if (!captureSnapshot.HasValue || !captureSnapshot.Value)
        {
            snapshot.CaptureSnapshot = false;
            return snapshot;
        }

        var capture = new Capture
        {
            MaxCollectionSize = 1000,
            MaxFieldCount = 10000,
            MaxFieldDepth = 3,
            MaxLength = int.MaxValue,
            MaxReferenceDepth = 3
        };
        snapshot.Capture = capture;
        return snapshot;
    }

    internal static LogProbe WithWhen(this LogProbe snapshot, ProbeAttributeBase att)
    {
        if (att == null || string.IsNullOrEmpty(att.ConditionJson))
        {
            return snapshot;
        }

        snapshot.When = new SnapshotSegment(att.ConditionDsl, att.ConditionJson, null);
        snapshot.EvaluateAt = (EvaluateAt)att.EvaluateAt;
        return snapshot;
    }

    internal static LogProbe WithSampling(this LogProbe snapshot, double snapshotsPerSeconds = 1000000)
    {
        snapshot.Sampling = new Configurations.Models.Sampling { SnapshotsPerSecond = snapshotsPerSeconds };
        return snapshot;
    }

    internal static LogProbe WithDefaultTemplate(this LogProbe snapshot)
    {
        if (snapshot.Segments != null)
        {
            return snapshot;
        }

        snapshot.Template = "Test {1}";
        var json = @"{
    ""Ignore"": ""1""
}";
        snapshot.Segments = new SnapshotSegment[] { new(null, null, "Test"), new("1", json, null) };
        snapshot.EvaluateAt = EvaluateAt.Entry;
        return snapshot;
    }

    internal static LogProbe WithTemplate(this LogProbe snapshot, ProbeAttributeBase att)
    {
        if (att == null || string.IsNullOrEmpty(att.TemplateJson))
        {
            return snapshot.WithDefaultTemplate();
        }

        snapshot.Segments = new SnapshotSegment[] { new(null, null, att.TemplateStr), new(att.TemplateDsl, att.TemplateJson, null) };
        snapshot.EvaluateAt = (EvaluateAt)att.EvaluateAt;
        return snapshot;
    }

    private static bool IsOptimized(Assembly assembly)
    {
        var debuggableAttribute = (DebuggableAttribute)Attribute.GetCustomAttribute(assembly, typeof(DebuggableAttribute));
        var isOptimized = !debuggableAttribute.IsJITOptimizerDisabled;
        return isOptimized;
    }

    private static LogProbe CreateLogMethodProbe(MethodBase method, DeterministicGuidGenerator guidGenerator)
    {
        var probeTestData = method.GetCustomAttribute<MethodProbeTestDataAttribute>();
        if (probeTestData == null)
        {
            throw new Xunit.Sdk.SkipException($"{nameof(MethodProbeTestDataAttribute)} has not found for method: {method.DeclaringType?.FullName}.{method.Name}");
        }

        var typeName = probeTestData.UseFullTypeName ? method.DeclaringType?.FullName : method.DeclaringType?.Name;

        if (typeName == null)
        {
            throw new Xunit.Sdk.SkipException($"{nameof(CreateLogMethodProbe)} failed in getting type name for method: {method.Name}");
        }

        var logProbe = CreateDefaultLogProbe(typeName, method.Name, guidGenerator, probeTestData).WithWhen(probeTestData).WithTemplate(probeTestData);
        return logProbe;
    }

    private static LogProbe CreateLogLineProbe(Type type, LineProbeTestDataAttribute line, DeterministicGuidGenerator guidGenerator)
    {
        return CreateBasicLogProbe(guidGenerator).WithLineProbeWhere(type, line).WithCapture(line?.CaptureSnapshot).WithTemplate(line).WithWhen(line);
    }

    private static LogProbe WithLineProbeWhere(this LogProbe snapshot, Type type, LineProbeTestDataAttribute line)
    {
        using var reader = DatadogPdbReader.CreatePdbReader(type.Assembly);
        var symbolMethod = reader.ReadMethodSymbolInfo(type.GetMethods().First().MetadataToken);
        var filePath = symbolMethod.SequencePoints.First().Document.URL;
        var where = new Where { SourceFile = filePath, Lines = new[] { line.LineNumber.ToString() } };
        snapshot.Where = where;
        return snapshot;
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
                            return att?.Skip == false && att.Unlisted == unlisted && att.SkipOnFrameworks.Contains(targetFramework) == false;
                        });
    }
}
