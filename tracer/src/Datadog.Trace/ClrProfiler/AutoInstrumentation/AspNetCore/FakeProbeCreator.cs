using System;
using System.Reflection;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.PInvoke;


public static class FakeProbeCreator
{
    public static void CreateAndInstallProbe(string displayName, MethodInfo methodInfo)
    {
        var probeName = $"{displayName}_{methodInfo.DeclaringType.FullName}_{methodInfo.Name}";
        var method = new NativeMethodProbeDefinition(probeName, methodInfo.DeclaringType.FullName, methodInfo.Name, targetParameterTypesFullName: null);
        var templateStr = probeName;
        var template = templateStr + "{1}";
        var json = @"{
    ""Ignore"": ""1""
}";
        var segments = new SnapshotSegment[] { new(null, null, templateStr), new("1", json, null) };

        var methodProbeDef = new LogProbe
        {
            CaptureSnapshot = true,
            Id = method.ProbeId,
            Where = new Where { MethodName = method.TargetMethod, TypeName = method.TargetType },
            EvaluateAt = EvaluateAt.Entry,
            Template = template,
            Segments = segments,
            Sampling = new Sampling { SnapshotsPerSecond = 1000000 }
        };
        ProbeExpressionsProcessor.Instance.AddProbeProcessor(methodProbeDef);
        // Install probes
        DebuggerNativeMethods.InstrumentProbes(
            new[] { method },
            Array.Empty<NativeLineProbeDefinition>(),
            Array.Empty<NativeSpanProbeDefinition>(),
            Array.Empty<NativeRemoveProbeRequest>());
    }
}
