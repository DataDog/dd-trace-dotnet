using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.PInvoke;


public static class FakeProbeCreator
{
    private static readonly HashSet<string> CreatedProbes = new();
    
    public static void CreateAndInstallMethodProbe(string displayName, MethodInfo methodInfo)
    {
        var probeName = $"{displayName}_{methodInfo.DeclaringType.FullName}_{methodInfo.Name}";
        if (!CreatedProbes.Add(probeName))
        {
            return; // Probe was already created
        }

        var method = new NativeMethodProbeDefinition(probeName, methodInfo.DeclaringType.FullName, methodInfo.Name, targetParameterTypesFullName: null);

        var where = new Where { MethodName = method.TargetMethod, TypeName = method.TargetType };
        
        CreateProbeProcessor(probeName, where, method.ProbeId);
        
        DebuggerNativeMethods.InstrumentProbes(
            new[] { method },
            Array.Empty<NativeLineProbeDefinition>(),
            Array.Empty<NativeSpanProbeDefinition>(),
            Array.Empty<NativeRemoveProbeRequest>());
    }

    public static void CreateAndInstallLineProbe(string displayName, NativeLineProbeDefinition lineProbeDefinition)
    {
        var probeName = $"{displayName}_{lineProbeDefinition.ProbeId}";
        if (!CreatedProbes.Add(probeName))
        {
            return; // Probe was already created
        }
        
        var where = new Where { SourceFile = lineProbeDefinition.ProbeFilePath, Lines = new[] { lineProbeDefinition.LineNumber.ToString() } };
        
        CreateProbeProcessor(probeName, where, lineProbeDefinition.ProbeId);
        
        DebuggerNativeMethods.InstrumentProbes(
            Array.Empty<NativeMethodProbeDefinition>(),
            new[] { lineProbeDefinition },
            Array.Empty<NativeSpanProbeDefinition>(),
            Array.Empty<NativeRemoveProbeRequest>());
    }
        
    
    

    private static void CreateProbeProcessor(string probeName, Where where, string probeId)
    {
        var templateStr = probeName;
        var template = templateStr;
    
        var segments = new SnapshotSegment[] { new(null, null, templateStr) };

        var methodProbeDef = new LogProbe
        {
            CaptureSnapshot = true,
            Id = probeId,
            Where = where,
            EvaluateAt = EvaluateAt.Entry,
            Template = template,
            Segments = segments,
            Sampling = new Sampling { SnapshotsPerSecond = 1000000 }
        };
        ProbeExpressionsProcessor.Instance.AddProbeProcessor(methodProbeDef);
    }
}
