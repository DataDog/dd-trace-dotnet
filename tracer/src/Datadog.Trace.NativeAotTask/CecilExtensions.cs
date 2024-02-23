// <copyright file="CecilExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Datadog.Trace.NativeAotTask;

internal static class CecilExtensions
{
    public static Cursor OpenCursor(this ILProcessor ilProcessor, SeekOrigin origin = SeekOrigin.Begin)
    {
        return new Cursor(ilProcessor, origin);
    }

    public static Cursor OpenCursor(this ILProcessor ilProcessor, Instruction position)
    {
        return new Cursor(ilProcessor, position);
    }

    public static MethodReference MakeGenericMethod(this MethodReference method, TypeReference genericDeclaringType)
    {
        var genericReference = new MethodReference(method.Name, method.ReturnType, genericDeclaringType)
        {
            CallingConvention = method.CallingConvention,
            HasThis = method.HasThis,
            ExplicitThis = method.ExplicitThis,
        };

        foreach (var parameter in method.Parameters)
        {
            genericReference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));
        }

        foreach (var genericParameter in method.GenericParameters)
        {
            genericReference.GenericParameters.Add(new GenericParameter(genericParameter.Name, genericReference));
        }

        return genericReference;
    }

    public static FieldReference MakeGenericField(this FieldReference field, TypeReference genericDeclaringType)
    {
        var genericReference = new FieldReference(field.Name, field.FieldType, genericDeclaringType);
        return genericReference;
    }

    public static void MakePublic(this TypeDefinition type)
    {
        if (type.IsNested)
        {
            type.IsNestedPublic = true;
        }
        else
        {
            type.IsPublic = true;
        }
    }

    public static void MakePublic(this MethodDefinition method)
    {
        method.IsPublic = true;
    }
}
