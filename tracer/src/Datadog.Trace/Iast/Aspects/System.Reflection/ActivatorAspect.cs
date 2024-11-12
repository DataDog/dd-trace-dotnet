// <copyright file="ActivatorAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Dataflow;

#nullable enable

namespace Datadog.Trace.Iast.Aspects;

/// <summary> System.Activator class aspect </summary>
[AspectClass("mscorlib,netstandard,System.Runtime", AspectType.Sink, VulnerabilityType.ReflectionInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class ActivatorAspect
{
    /// <summary>
    /// System.Activator aspects
    /// </summary>
    /// <param name="param"> string parameter </param>
    /// <returns> resulting string parameter </returns>
    [AspectMethodInsertBefore("System.Activator::CreateInstance(System.String,System.String)", 1, 0)]
    [AspectMethodInsertBefore("System.Activator::CreateInstance(System.String,System.String,System.Object[])", 2, 1)]
    [AspectMethodInsertBefore("System.Activator::CreateInstance(System.AppDomain,System.String,System.String)", 1, 0)]
    [AspectMethodInsertBefore("System.Activator::CreateInstance(System.AppDomain,System.String,System.String,System.Boolean,System.Reflection.BindingFlags,System.Reflection.Binder,System.Object[],System.Globalization.CultureInfo,System.Object[])", 7, 6)]
    [AspectMethodInsertBefore("System.Activator::CreateInstance(System.String,System.String,System.Boolean,System.Reflection.BindingFlags,System.Reflection.Binder,System.Object[],System.Globalization.CultureInfo,System.Object[])", 7, 6)]
    [AspectMethodInsertBefore("System.Activator::CreateInstance(System.String,System.String,System.Boolean,System.Reflection.BindingFlags,System.Reflection.Binder,System.Object[],System.Globalization.CultureInfo,System.Object[],System.Security.Policy.Evidence)", 8, 7)]
    [AspectMethodInsertBefore("System.Activator::CreateInstance(System.AppDomain,System.String,System.String,System.Boolean,System.Reflection.BindingFlags,System.Reflection.Binder,System.Object[],System.Globalization.CultureInfo,System.Object[],System.Security.Policy.Evidence)", 7, 6)]
    [AspectMethodInsertBefore("System.Activator::CreateInstanceFrom(System.String,System.String,System.Object[])", 2, 1)]
    [AspectMethodInsertBefore("System.Activator::CreateInstanceFrom(System.String,System.String)", 1, 0)]
    [AspectMethodInsertBefore("System.Activator::CreateInstanceFrom(System.AppDomain,System.String,System.String)", 1, 0)]
    [AspectMethodInsertBefore("System.Activator::CreateInstanceFrom(System.AppDomain,System.String,System.String,System.Boolean,System.Reflection.BindingFlags,System.Reflection.Binder,System.Object[],System.Globalization.CultureInfo,System.Object[])", 7, 6)]
    [AspectMethodInsertBefore("System.Activator::CreateInstanceFrom(System.String,System.String,System.Boolean,System.Reflection.BindingFlags,System.Reflection.Binder,System.Object[],System.Globalization.CultureInfo,System.Object[])", 7, 6)]
    [AspectMethodInsertBefore("System.Activator::CreateInstanceFrom(System.String,System.String,System.Boolean,System.Reflection.BindingFlags,System.Reflection.Binder,System.Object[],System.Globalization.CultureInfo,System.Object[],System.Security.Policy.Evidence)", 8, 7)]
    [AspectMethodInsertBefore("System.Activator::CreateComInstanceFrom(System.String,System.String)", 1, 0)]
    [AspectMethodInsertBefore("System.Activator::CreateComInstanceFrom(System.String,System.String,System.Byte[],System.Configuration.Assemblies.AssemblyHashAlgorithm)", 3, 2)]
    public static string ReflectionInjectionParam(string param)
    {
        try
        {
            IastModule.OnReflectionInjection(param, IntegrationId.ReflectionInjection);
            return param;
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(ActivatorAspect)}.{nameof(ReflectionInjectionParam)}");
            return param;
        }
    }
}
