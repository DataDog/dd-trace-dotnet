// <copyright file="TypeAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Dataflow;

#nullable enable

namespace Datadog.Trace.Iast.Aspects;

/// <summary> System.Type class aspect </summary>
[AspectClass("mscorlib,netstandard,System.Runtime", AspectType.Sink, VulnerabilityType.ReflectionInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class TypeAspect
{
    /// <summary>
    /// System.Type GetType, GetMethod and InvokeMember aspects
    /// </summary>
    /// <param name="param"> string parameter </param>
    /// <returns> resulting string parameter </returns>
    [AspectMethodInsertBefore("System.Type::GetType(System.String)", 0)]
    [AspectMethodInsertBefore("System.Type::GetType(System.String,System.Boolean)", 1)]
    [AspectMethodInsertBefore("System.Type::GetType(System.String,System.Boolean,System.Boolean)", 2)]
    [AspectMethodInsertBefore("System.Type::GetType(System.String,System.Func`2<System.Reflection.AssemblyName,System.Reflection.Assembly>,System.Func`4<System.Reflection.Assembly,System.String,System.Boolean,System.Type>)", 2)]
    [AspectMethodInsertBefore("System.Type::GetType(System.String,System.Func`2<System.Reflection.AssemblyName,System.Reflection.Assembly>,System.Func`4<System.Reflection.Assembly,System.String,System.Boolean,System.Type>,System.Boolean)", 3)]
    [AspectMethodInsertBefore("System.Type::GetType(System.String,System.Func`2<System.Reflection.AssemblyName,System.Reflection.Assembly>,System.Func`4<System.Reflection.Assembly,System.String,System.Boolean,System.Type>,System.Boolean,System.Boolean)", 4)]
    [AspectMethodInsertBefore("System.Type::GetMethod(System.String,System.Int32,System.Reflection.BindingFlags,System.Reflection.Binder,System.Reflection.CallingConventions,System.Type[],System.Reflection.ParameterModifier[])", 6)]
    [AspectMethodInsertBefore("System.Type::GetMethod(System.String,System.Reflection.BindingFlags,System.Reflection.Binder,System.Reflection.CallingConventions,System.Type[],System.Reflection.ParameterModifier[])", 5)]
    [AspectMethodInsertBefore("System.Type::GetMethod(System.String,System.Int32,System.Reflection.BindingFlags,System.Reflection.Binder,System.Type[],System.Reflection.ParameterModifier[])", 5)]
    [AspectMethodInsertBefore("System.Type::GetMethod(System.String,System.Reflection.BindingFlags,System.Reflection.Binder,System.Type[],System.Reflection.ParameterModifier[])", 4)]
    [AspectMethodInsertBefore("System.Type::GetMethod(System.String,System.Int32,System.Type[],System.Reflection.ParameterModifier[])", 3)]
    [AspectMethodInsertBefore("System.Type::GetMethod(System.String)", 0)]
    [AspectMethodInsertBefore("System.Type::GetMethod(System.String,System.Reflection.BindingFlags,System.Type[])", 2)]
    [AspectMethodInsertBefore("System.Type::GetMethod(System.String,System.Int32,System.Type[])", 2)]
    [AspectMethodInsertBefore("System.Type::GetMethod(System.String,System.Type[])", 1)]
    [AspectMethodInsertBefore("System.Type::GetMethod(System.String,System.Reflection.BindingFlags)", 1)]
    [AspectMethodInsertBefore("System.Type::GetMethod(System.String,System.Type[],System.Reflection.ParameterModifier[])", 2)]
    [AspectMethodInsertBefore("System.Type::InvokeMember(System.String,System.Reflection.BindingFlags,System.Reflection.Binder,System.Object,System.Object[])", 4)]
    [AspectMethodInsertBefore("System.Type::InvokeMember(System.String,System.Reflection.BindingFlags,System.Reflection.Binder,System.Object,System.Object[],System.Globalization.CultureInfo)", 5)]
    [AspectMethodInsertBefore("System.Type::InvokeMember(System.String,System.Reflection.BindingFlags,System.Reflection.Binder,System.Object,System.Object[],System.Reflection.ParameterModifier[],System.Globalization.CultureInfo,System.String[])", 7)]
    public static string ReflectionInjectionParam(string param)
    {
        try
        {
            IastModule.OnReflectionInjection(param, IntegrationId.ReflectionInjection);
            return param;
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(TypeAspect)}.{nameof(ReflectionInjectionParam)}");
            return param;
        }
    }
}
