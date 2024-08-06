// <copyright file="AssemblyAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Dataflow;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.System.Reflection;

/// <summary> System.Reflection MethodBase class aspect </summary>
[AspectClass("mscorlib,netstandard,System.Runtime", AspectType.Sink, VulnerabilityType.ReflectionInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class AssemblyAspect
{
    /// <summary>
    /// System.Reflection Assembly.Load aspects
    /// </summary>
    /// <param name="assemblyString"> args parameter </param>
    /// <returns> resulting args parameter </returns>
    [AspectMethodInsertBefore("System.Reflection.Assembly::Load(System.String)", 0)]
    [AspectMethodInsertBefore("System.Reflection.Assembly::Load(System.String,System.Security.Policy.Evidence)", 1)]
    [AspectMethodInsertBefore("System.Reflection.Assembly::LoadFrom(System.String)", 0)]
    [AspectMethodInsertBefore("System.Reflection.Assembly::LoadFrom(System.String,System.Byte[],System.Configuration.Assemblies.AssemblyHashAlgorithm)", 2)]
    [AspectMethodInsertBefore("System.Reflection.Assembly::LoadFrom(System.String,System.Security.Policy.Evidence,System.Byte[],System.Configuration.Assemblies.AssemblyHashAlgorithm)", 3)]
    [AspectMethodInsertBefore("System.Reflection.Assembly::LoadFrom(System.String,System.Security.Policy.Evidence)", 1)]
    [AspectMethodInsertBefore("System.Reflection.AssemblyName::.ctor(System.String)", 0)]
    public static string ReflectionAssemblyInjection(string assemblyString)
    {
        try
        {
            IastModule.OnReflectionInjection(assemblyString, IntegrationId.ReflectionInjection);
            return assemblyString;
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(AssemblyAspect)}.{nameof(ReflectionAssemblyInjection)}");
            return assemblyString;
        }
    }
}
