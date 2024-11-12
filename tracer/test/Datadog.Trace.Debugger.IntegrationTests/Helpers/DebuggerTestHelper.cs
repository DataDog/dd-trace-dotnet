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
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Pdb;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
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

    public static ProbeTestDescription SpecificTestDescription(Type type)
    {
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

    internal static async Task<DebuggerSampleProcessHelper> StartSample(TestHelper helper, MockTracerAgent agent, string testName)
    {
        var listenPort = TcpPortProvider.GetOpenPort();

        var localHost = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "http://127.0.0.1" : "http://localhost";
        var listenUrl = $"{localHost}:{listenPort}/";

        var process = await helper.StartSample(agent, $"--test-name {testName} --listen-url {listenUrl}", string.Empty, aspNetCorePort: 5000);
        var processHelper = new DebuggerSampleProcessHelper(listenUrl, process);

        return processHelper;
    }

    internal static int CalculateExpectedNumberOfSnapshots(ProbeAttributeBase[] probes)
    {
        return probes.Aggregate(0, (accuNumOfSnapshots, next) => accuNumOfSnapshots + next.ExpectedNumberOfSnapshots);
    }

    internal static bool IsMetricProbe(ProbeAttributeBase probeAttr)
    {
        return probeAttr is MetricMethodProbeTestDataAttribute or MetricLineProbeTestDataAttribute;
    }

    internal static bool IsSpanProbe(ProbeAttributeBase probeAttr)
    {
        return probeAttr is SpanOnMethodProbeTestDataAttribute;
    }

    internal static bool IsSpanDecorationProbe(ProbeAttributeBase probeAttr)
    {
        return probeAttr is SpanDecorationMethodProbeTestDataAttribute;
    }

    internal static bool IsLogProbe(ProbeAttributeBase probeAttr)
    {
        return probeAttr is LogMethodProbeTestDataAttribute or LogLineProbeTestDataAttribute;
    }

    internal static (ProbeAttributeBase ProbeTestData, ProbeDefinition Probe)[] GetAllProbes(Type type, string targetFramework, bool unlisted, DeterministicGuidGenerator guidGenerator)
    {
        return GetAllMethodProbes(type, targetFramework, unlisted, guidGenerator)
              .Concat(GetAllLineProbes(type, targetFramework, unlisted, guidGenerator))
              .ToArray();
    }

    internal static IEnumerable<(ProbeAttributeBase ProbeTestData, ProbeDefinition Probe)> GetAllLineProbes(Type type, string targetFramework, bool unlisted, DeterministicGuidGenerator guidGenerator)
    {
        var snapshotLineProbes = type.GetCustomAttributes<LineProbeTestDataAttribute>()
                                     .Where(att => att.Skip == false && att.Unlisted == unlisted && att.SkipOnFrameworks.Contains(targetFramework) == false)
                                     .Select(att => (att.As<ProbeAttributeBase>(), CreateLogLineProbe(type, att, guidGenerator)))
                                     .ToArray();

        return snapshotLineProbes;
    }

    internal static IEnumerable<(ProbeAttributeBase ProbeTestData, ProbeDefinition Probe)> GetAllMethodProbes(Type type, string targetFramework, bool unlisted, DeterministicGuidGenerator guidGenerator)
    {
        var snapshotMethodProbes = GetAllTestMethods<MethodProbeTestDataAttribute>(type, targetFramework, unlisted)
           .Select(m =>
            {
                var probes = new List<(ProbeAttributeBase ProbeTestData, ProbeDefinition Probe)>();
                var testAttributes = m.GetCustomAttributes<MethodProbeTestDataAttribute>().OfType<ProbeAttributeBase>().ToArray();
                for (var testIndex = 0; testIndex < testAttributes.Length; testIndex++)
                {
                    var testAttribute = testAttributes[testIndex];

                    if (IsMetricProbe(testAttribute))
                    {
                        probes.Add((testAttribute, CreateMetricMethodProbe(m, guidGenerator, testIndex)));
                    }
                    else if (IsSpanProbe(testAttribute))
                    {
                        probes.Add((testAttribute, CreateSpanMethodProbe(m, guidGenerator, testIndex)));
                    }
                    else if (IsLogProbe(testAttribute))
                    {
                        probes.Add((testAttribute, CreateLogMethodProbe(m, guidGenerator, testIndex)));
                    }
                    else if (IsSpanDecorationProbe(testAttribute))
                    {
                        probes.Add((testAttribute, CreateSpanDecorationMethodProbe(m, guidGenerator, testIndex)));
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unknown probe type: {testAttribute.GetType()}");
                    }
                }

                return probes;
            });

        return snapshotMethodProbes.SelectMany(p => p);
    }

    internal static T CreateBasicProbe<T>(string probeId)
    where T : ProbeDefinition, new()
    {
        return new T
        {
            Id = probeId,
            Language = TracerConstants.Language,
        };
    }

    internal static ProbeDefinition CreateDefaultLogProbe(string typeName, string methodName, DeterministicGuidGenerator guidGenerator, MethodProbeTestDataAttribute probeTestData = null)
    {
        return CreateBasicProbe<LogProbe>(probeTestData?.ProbeId ?? guidGenerator.New().ToString()).WithSampling().WithDefaultTemplate().WithCapture(probeTestData?.CaptureSnapshot).WithMethodWhere(typeName, methodName, probeTestData: probeTestData);
    }

    internal static ProbeDefinition CreateLogLineProbe(Type type, LineProbeTestDataAttribute line, DeterministicGuidGenerator guidGenerator)
    {
        return CreateBasicProbe<LogProbe>(line?.ProbeId ?? guidGenerator.New().ToString()).WithCapture(line?.CaptureSnapshot).WithSampling().WithTemplate(line).WithWhen(line).WithLineProbeWhere(type, line);
    }

    private static ProbeDefinition WithMethodWhere(this ProbeDefinition snapshot, string typeName, string methodName, MethodBase method = null, MethodProbeTestDataAttribute probeTestData = null)
    {
        var @where = new Where
        {
            TypeName = typeName,
            MethodName = methodName
        };

        if (probeTestData != null)
        {
            var signature = string.Empty;
            if (probeTestData.ParametersTypeName?.Any() == true)
            {
                signature = (method != null && !method.IsStatic) ? method.DeclaringType.FullName + "," : string.Empty;
                signature += string.Join(",", probeTestData.ParametersTypeName);
            }

            @where.Signature = string.IsNullOrEmpty(signature) ? null : signature;
        }

        snapshot.Where = where;
        return snapshot;
    }

    private static ProbeDefinition WithLineProbeWhere(this ProbeDefinition snapshot, Type type, LineProbeTestDataAttribute line)
    {
        using var reader = DatadogMetadataReader.CreatePdbReader(type.Assembly);
        var sequencePoints = reader?.GetMethodSequencePoints(type.GetMethods().First().MetadataToken);
        var filePath = sequencePoints?.First().URL;
        var where = new Where { SourceFile = filePath, Lines = new[] { line.LineNumber.ToString() } };
        snapshot.Where = where;
        return snapshot;
    }

    private static LogProbe WithCapture(this LogProbe snapshot, bool? captureSnapshot)
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
            MaxLength = int.MaxValue,
            MaxReferenceDepth = 3
        };

        snapshot.CaptureSnapshot = true;
        snapshot.Capture = capture;
        return snapshot;
    }

    private static LogProbe WithWhen(this LogProbe snapshot, ProbeAttributeBase att)
    {
        if (att == null || string.IsNullOrEmpty(att.ConditionJson))
        {
            return snapshot;
        }

        snapshot.When = new SnapshotSegment(string.Empty, att.ConditionJson, null);
        snapshot.EvaluateAt = ParseEnum<EvaluateAt>(att.EvaluateAt);
        return snapshot;
    }

    private static LogProbe WithSampling(this LogProbe snapshot, double snapshotsPerSeconds = 1000000)
    {
        snapshot.Sampling = new Configurations.Models.Sampling { SnapshotsPerSecond = snapshotsPerSeconds };
        return snapshot;
    }

    private static LogProbe WithDefaultTemplate(this LogProbe snapshot)
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

    private static LogProbe WithTemplate(this LogProbe snapshot, ProbeAttributeBase att)
    {
        if (att == null || (string.IsNullOrEmpty(att.TemplateJson) && string.IsNullOrEmpty(att.TemplateStr)))
        {
            return snapshot.WithDefaultTemplate();
        }

        var segments = new List<SnapshotSegment>();

        if (att.TemplateStr != null)
        {
            segments.Add(new SnapshotSegment(null, null, att.TemplateStr));
        }

        if (att.TemplateJson != null)
        {
            segments.Add(new SnapshotSegment(string.Empty, att.TemplateJson, null));
        }

        snapshot.Segments = segments.ToArray();
        snapshot.EvaluateAt = ParseEnum<EvaluateAt>(att.EvaluateAt ?? "Exit");
        return snapshot;
    }

    private static MetricProbe WithMetric(this MetricProbe probe, MetricMethodProbeTestDataAttribute probeTestData)
    {
        probe.Value = new SnapshotSegment(null, probeTestData.MetricJson, string.Empty);
        probe.Kind = ParseEnum<MetricKind>(probeTestData.MetricKind);
        probe.MetricName = probeTestData.MetricName;
        return probe;
    }

    private static SpanDecorationProbe WithSpanDecoration(this SpanDecorationProbe probe, SpanDecorationMethodProbeTestDataAttribute probeTestData)
    {
        probe.TargetSpan = TargetSpan.Active;
        probe.Decorations = JsonConvert.DeserializeObject<SpanDecorationProbe>(probeTestData.Decorations).Decorations;
        return probe;
    }

    private static ProbeDefinition CreateMetricMethodProbe(MethodBase method, DeterministicGuidGenerator guidGenerator, int testIndex)
    {
        var probeTestData = GetProbeTestData<MetricMethodProbeTestDataAttribute>(method, testIndex, out var typeName) as MetricMethodProbeTestDataAttribute;

        if (probeTestData == null || string.IsNullOrEmpty(probeTestData.MetricKind))
        {
            throw new InvalidOperationException("This probe attribute has no metric information");
        }

        return CreateBasicProbe<MetricProbe>(probeTestData.ProbeId ?? guidGenerator.New().ToString()).WithMetric(probeTestData).WithMethodWhere(typeName, method.Name, method, probeTestData);
    }

    private static ProbeDefinition CreateSpanMethodProbe(MethodBase method, DeterministicGuidGenerator guidGenerator, int testIndex)
    {
        var probeTestData = GetProbeTestData<SpanOnMethodProbeTestDataAttribute>(method, testIndex, out var typeName);
        if (probeTestData == null)
        {
            throw new InvalidOperationException("Probe attribute is null");
        }

        return CreateBasicProbe<SpanProbe>(probeTestData.ProbeId ?? guidGenerator.New().ToString()).WithMethodWhere(typeName, method.Name, method, probeTestData);
    }

    private static ProbeDefinition CreateSpanDecorationMethodProbe(MethodBase method, DeterministicGuidGenerator guidGenerator, int testIndex)
    {
        var probeTestData = GetProbeTestData<SpanDecorationMethodProbeTestDataAttribute>(method, testIndex, out var typeName) as SpanDecorationMethodProbeTestDataAttribute;
        if (probeTestData == null)
        {
            throw new InvalidOperationException("Probe attribute is null");
        }

        return CreateBasicProbe<SpanDecorationProbe>(probeTestData.ProbeId ?? guidGenerator.New().ToString()).WithSpanDecoration(probeTestData).WithMethodWhere(typeName, method.Name, method, probeTestData);
    }

    private static bool IsOptimized(Assembly assembly)
    {
        var debuggableAttribute = (DebuggableAttribute)Attribute.GetCustomAttribute(assembly, typeof(DebuggableAttribute));
        var isOptimized = !debuggableAttribute.IsJITOptimizerDisabled;
        return isOptimized;
    }

    private static ProbeDefinition CreateLogMethodProbe(MethodBase method, DeterministicGuidGenerator guidGenerator, int testIndex)
    {
        var probeTestData = GetProbeTestData<LogMethodProbeTestDataAttribute>(method, testIndex, out var typeName);
        if (probeTestData == null)
        {
            throw new InvalidOperationException("Probe attribute is null");
        }

        return CreateBasicProbe<LogProbe>(probeTestData.ProbeId ?? guidGenerator.New().ToString()).WithCapture(probeTestData.CaptureSnapshot).WithSampling().WithTemplate(probeTestData).WithWhen(probeTestData).WithMethodWhere(typeName, method.Name, method, probeTestData);
    }

    private static MethodProbeTestDataAttribute GetProbeTestData<T>(MethodBase method, int testIndex, out string typeName)
        where T : MethodProbeTestDataAttribute
    {
        var probeTestData = method.GetCustomAttributes<MethodProbeTestDataAttribute>().ElementAt(testIndex);
        if (probeTestData == null)
        {
            throw new Xunit.Sdk.SkipException($"{typeof(T).Name} has not found for method: {method.DeclaringType?.FullName}.{method.Name}");
        }

        typeName = probeTestData.UseFullTypeName ? method.DeclaringType?.FullName : method.DeclaringType?.Name;

        if (typeName == null)
        {
            throw new Xunit.Sdk.SkipException($"{nameof(CreateLogMethodProbe)} failed in getting type name for method: {method.Name}");
        }

        return probeTestData;
    }

    private static IEnumerable<MethodBase> GetAllTestMethods<T>(Type type, string targetFramework, bool unlisted)
        where T : ProbeAttributeBase
    {
        const BindingFlags allMask =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Static |
            BindingFlags.Instance;

        IEnumerable<MethodBase> GetMethodsRecursive(Type currentType)
        {
            var methods = currentType.GetMethods(allMask | BindingFlags.DeclaredOnly)
                                     .Concat(currentType.GetConstructors(allMask | BindingFlags.DeclaredOnly).As<IEnumerable<MethodBase>>());

            var nestedMethods = currentType.GetNestedTypes(allMask)
                                           .SelectMany(GetMethodsRecursive);

            return methods.Concat(nestedMethods);
        }

        var allMethods = GetMethodsRecursive(type);

        return allMethods.Where(m =>
        {
            var atts = m.GetCustomAttributes<T>();
            return atts.Any() && atts.All(att => att?.Skip == false && att.Unlisted == unlisted && !att.SkipOnFrameworks.Contains(targetFramework));
        });
    }

    private static T ParseEnum<T>(string enumValue)
        where T : struct
    {
#if NETFRAMEWORK
        return (T)Enum.Parse(typeof(T), enumValue, true);
#else
        return Enum.Parse<T>(enumValue, true);
#endif
    }
}
