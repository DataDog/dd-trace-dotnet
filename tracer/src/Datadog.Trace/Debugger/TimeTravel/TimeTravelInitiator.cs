using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Vendors.dnlib.DotNet;
using Datadog.Trace.Vendors.dnlib.DotNet.Emit;

namespace Datadog.Trace.Debugger.TimeTravel;

public class TimeTravelInitiator
{
    public static void InitiateTimeTravel(MethodInfo method)
    {
        var alreadyVisited = new HashSet<MethodInfo> { };
        InitiateTimeTravel(method, alreadyVisited);
    }

    private static void InitiateTimeTravel(MethodInfo method, HashSet<MethodInfo> alreadyVisited)
    {
        if (!alreadyVisited.Add(method)) return;

        // use dnlib to find the file path and line numbers of the method
        var lineProbes = GetLineProbeLocationsWithDnlib(method);
        foreach (var lineProbe in lineProbes)
        {
            FakeProbeCreator.CreateAndInstallLineProbe("TimeTravelLine", lineProbe);
        }
        
        // use dnlib to find all the methods that are directly called by this method,
        // and create probes for them, and then call this method recursively
        var calledMethods = GetCalleesWithDnlib(method);
        foreach (var callee in calledMethods)
        {
            FakeProbeCreator.CreateAndInstallMethodProbe("TimeTravel", callee);
            InitiateTimeTravel(callee, alreadyVisited);
        }
    }

    private static List<MethodInfo> GetCalleesWithDnlib(MethodInfo methodInfo)
    {
        var result = new List<MethodInfo>();

        var targetMethod = FindMethod(methodInfo);

        if (targetMethod is { HasBody: true })
        {
            // Analyze the method body for call instructions
            foreach (Instruction instruction in targetMethod.Body.Instructions)
            {
                if (instruction.OpCode.Code == Code.Call || instruction.OpCode.Code == Code.Callvirt)
                {
                    // Resolve the called method
                    IMethod calledMethod = instruction.Operand as IMethod;
                    if (calledMethod != null)
                    {
                        try
                        {
                            {
                                MethodDef resolvedMethod = calledMethod.ResolveMethodDefThrow();
                                // Convert the dnlib MethodDef to a Reflection MethodInfo
                                var resolvedMethodInfo = (MethodInfo) methodInfo.DeclaringType.Assembly.ManifestModule.ResolveMethod(resolvedMethod.MDToken.ToInt32());
                                result.Add(resolvedMethodInfo);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Could not resolve method " + targetMethod.FullName);
                        }
                    }
                }
            }
        }
        return result;
    }

    
    private static NativeLineProbeDefinition[] GetLineProbeLocationsWithDnlib(MethodInfo methodInfo)
    {
        var targetMethod = FindMethod(methodInfo);

        var mvid = methodInfo.DeclaringType.Assembly.ManifestModule.ModuleVersionId;
        if (targetMethod is { HasBody: true })
        {
            return targetMethod.Body.Instructions
                               .Where(i => i.SequencePoint?.StartLine is not null && i.SequencePoint?.StartLine != 0)
                               .GroupBy(i => i.SequencePoint?.StartLine)
                               .Select(g => new NativeLineProbeDefinition(
                                           $"{methodInfo.Name}, line {g.Key.Value}", 
                                                mvid, 
                                                methodInfo.MetadataToken, 
                                                (int)g.Min(i => i.Offset), 
                                                g.Key.Value, 
                                                g.First().SequencePoint.Document.Url))
                               .ToArray();
        }

        return Array.Empty<NativeLineProbeDefinition>();
    }
    
    private static MethodDef FindMethod(MethodInfo methodInfo)
    {
        ModuleDefMD module = ModuleDefMD.Load(methodInfo.DeclaringType.Assembly.Location);


        // Resolve the method you're interested in
        MethodDef targetMethod = null;
        foreach (TypeDef type in module.GetTypes())
        {
            foreach (MethodDef method in type.Methods)
            {
                // find the method by token
                if (method.MDToken.ToInt32() == methodInfo.MetadataToken)
                {
                    targetMethod = method;
                    break;
                }
            }

            if (targetMethod != null) break;
        }

        return targetMethod;
    }
}
