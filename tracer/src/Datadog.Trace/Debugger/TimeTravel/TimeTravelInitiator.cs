using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;
using Datadog.Trace.Vendors.dnlib.DotNet;
using Datadog.Trace.Vendors.dnlib.DotNet.Emit;

namespace Datadog.Trace.Debugger.TimeTravel;

public class TimeTravelInitiator
{
    public static void InitiateTimeTravel(MethodInfo method)
    {
        // use dnlib to find all the methods that are directly called by this method
        var calledMethods = GetCalledMethodsWithDnlib(method);
        foreach (var callee in calledMethods)
        {
            FakeProbeCreator.CreateAndInstallProbe("callee-" + callee.Name, callee);
        }
    }

    private static List<MethodInfo> GetCalledMethodsWithDnlib(MethodInfo methodInfo)
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
